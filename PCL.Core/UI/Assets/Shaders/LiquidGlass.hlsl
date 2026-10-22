// Adapted from Kyant0/AndroidLiquidGlass, commit
// 65ab177e90e5c1d8c62e70cf7755841982da65f6 (Copyright 2025 Kyant).
// Apache-2.0; see LiquidGlass.NOTICE.md and LICENSE.AndroidLiquidGlass.
sampler2D Backdrop : register(s0);
sampler2D Tabs : register(s1);
float4 Dimensions : register(c0); // expanded width/height, sampling gutter, reserved
float4 Corners : register(c1); // top left, top right, bottom right, bottom left
float4 Lens : register(c2); // edge width, displacement, depth, dispersion
float4 Light : register(c3); // x/y direction, strength, dark theme
float4 Tint : register(c4); // premultiplied surface tint
float4 Controls : register(c5); // saturation, reserved, upstream material (panel=1, thumb=2), press
float4 OverlayTint : register(c6);
float4 Interaction : register(c7); // highlight position.xy, radius, progress

float4 sampleContent(float2 uv) {
    float4 color=tex2Dlod(Backdrop,float4(saturate(uv),0,0));
    float gray=dot(color.rgb,float3(0.2126,0.7152,0.0722));
    color.rgb=saturate(lerp(gray.xxx,color.rgb,Controls.x));
    color=Tint+color*(1-Tint.a);
    if(Controls.z>1.5) {
        float4 tab=tex2Dlod(Tabs,float4(saturate(uv),0,0));
        color.rgb=lerp(color.rgb,OverlayTint.rgb,tab.a);
    }
    return color;
}

float distanceToEdge(float2 p, float2 halfSize, float r) {
    float2 q = abs(p) - (halfSize - r);
    return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
}
float2 edgeNormal(float2 p, float2 halfSize, float r) {
    float2 q = abs(p) - (halfSize - r);
    float2 outer = max(q, 0);
    float2 inner = float2(step(q.y, q.x), 1 - step(q.y, q.x));
    return sign(p) * (dot(outer, outer) > 0.00001 ? normalize(outer) : inner);
}
float4 PixelShaderFunction(float2 uv : TEXCOORD) : COLOR {
    float2 size = Dimensions.xy - 2 * Dimensions.z;
    float2 p = uv * Dimensions.xy - Dimensions.z - size * 0.5;
    float r = p.x >= 0 ? (p.y <= 0 ? Corners.y : Corners.z) : (p.y <= 0 ? Corners.x : Corners.w);
    r = min(r, min(size.x, size.y) * 0.5);
    float sd = distanceToEdge(p, size * 0.5, r);
    float h = max(0.01, min(Lens.x, max(r, 1)));
    // Most card pixels are flat interior: no refraction or rim contributes here.
    // Avoid normalisation, extra texture fetches and lighting for those pixels.
    [branch] if (Controls.z < .5 && sd < -max(h, 1.5)) {
        float4 interior = sampleContent(uv);
        interior.a = 1;
        return interior;
    }
    float t = saturate(1 + min(sd, 0) / h);
    float d = (1 - sqrt(max(0, 1 - t * t))) * -Lens.y;
    float2 n = edgeNormal(p, size * 0.5, min(r * 1.5, min(size.x, size.y) * 0.5));
    float2 radial = p / max(length(p), 0.001);
    float2 direction = n + Lens.z * radial;
    direction /= max(length(direction), 0.001);
    float2 shift = d * direction / Dimensions.xy;
    float2 refracted = saturate(uv + shift);
    float2 dispersion = shift * Lens.w * (p.x * p.y / max(1, size.x * size.y * 0.25));
    float4 color = sampleContent(refracted);
    if(Controls.z>1.5 && Lens.w>0) {
        // The upstream seven-band dispersion weights (not the former RGB shortcut).
        float4 red=sampleContent(refracted+dispersion);
        float4 orange=sampleContent(refracted+dispersion*(2.0/3.0));
        float4 yellow=sampleContent(refracted+dispersion*(1.0/3.0));
        float4 green=color;
        float4 cyan=sampleContent(refracted-dispersion*(1.0/3.0));
        float4 blue=sampleContent(refracted-dispersion*(2.0/3.0));
        float4 purple=sampleContent(refracted-dispersion);
        color.r=(red.r+orange.r+yellow.r)/3.5+purple.r/7;
        color.g=orange.g/7+(yellow.g+green.g+cyan.g)/3.5;
        color.b=(cyan.b+blue.b+purple.b)/3;
    } else if(Controls.z<.5) {
        color.r=sampleContent(refracted+dispersion).r;
        color.b=sampleContent(refracted-dispersion).b;
    }
    if(Controls.z>1.5) {
        float rest=.1*(1-Controls.w);
        color.rgb=lerp(color.rgb,Light.w>.5?1:0,rest);
        color.rgb*=1-.03*Controls.w;
        // Upstream inner shadow grows from 0 to 8 DIP with press progress.
        color.rgb*=1-.15*Controls.w*saturate(1+sd/max(.001,8*Controls.w));
    }
    float facing = dot(n, Light.xy);
    float rim = (Controls.z>.5 ? 1-smoothstep(.25,.75,-sd) : pow(saturate(1 + sd / 1.5), 1.4)) * Light.z;
    // Directional bright rim and a restrained opposing inner shadow.
    if(Controls.z>.5) color.rgb=saturate(color.rgb+rim*abs(facing));
    else {
        color.rgb += rim * max(facing, 0) * (1 - color.rgb);
        color.rgb *= 1 - rim * max(-facing, 0) * 0.22;
    }
    if(Controls.z>.5) {
        float dist=distance(uv*Dimensions.xy,Interaction.xy);
        float glow=1-smoothstep(Interaction.z*.5,Interaction.z,dist);
        color.rgb=saturate(color.rgb+Interaction.w*(.08+.15*glow));
    }
    // Keep the wallpaper's hue and tonal range. Readability comes from the
    // explicit surface tint, never from a per-pixel luminance clamp.
    color.a = 1;
    return color;
}
