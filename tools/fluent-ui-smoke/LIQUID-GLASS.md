# Liquid Glass implementation and validation

The launcher keeps WPF and adapts the rounded lens math from AndroidLiquidGlass
`kmp` commit `65ab177e90e5c1d8c62e70cf7755841982da65f6`. Attribution and the
Apache-2.0 license are under `PCL.Core/UI/Assets/Shaders/` and copied to published output.

The bottom bar additionally ports the upstream spring/gesture model and press-driven
material. See [DOCK-PARITY.md](DOCK-PARITY.md) for source links, exact parameters,
validation and platform-specific rendering boundaries.

## Rendering contract

`LiquidGlassSurface` extends `Border`, so existing padding, child layout, background
animations and hit targets remain compatible. Foreground content is drawn by WPF
after the background lens. The shared card, button, editor, dropdown, popup and
instance-tile implementations use this surface. Ordinary `Content`, `Chrome`, `Action`
and `Input` instances now use the lightweight material described below; the dock and
floating popups keep the full lens pipeline.

- Set the inheritable attached `BackdropSource` to a **separate background visual**.
  Never use a visual containing the glass surface. The main window registers
  `LiquidBackdrop`, containing the default anime wallpaper (gradient while loading/offline) and existing image/video canvas.
  The topmost navigation instead samples its sibling `SceneBackdrop`, containing
  that background **and the actual page content**, so scrolling text remains visible
  through its glass. The sampled scene never contains the navigation itself.
- `Material` selects Content, Chrome, Action, Input or Floating treatment.
  `RefractionStrength` accepts 0–2; zero is useful for comparison with a flat material.
  `CornerRadius`, `Background`, `BorderBrush`, `Padding` retain their usual meaning.
- `BlurRadius` accepts 0–64 DIP. The launcher theme connects it to the existing blur
  preference, with a 32 DIP application limit. Inputs share the card blur to avoid sharp wallpaper cutouts. The
  dedicated bottom-tabs presets use the upstream fixed 8 DIP blur; OS accessibility
  preferences still select the opaque fallback.
- The pipeline samples an inflated source region, blurs, applies the rounded lens,
  composites bounded neutral/semantic tint and directional highlights. Foreground contrast uses explicit tint, with no per-pixel brightness clamp or general-card saturation boost.
  Sampling uses public WPF visual transforms and brushes; popup coordinates cross
  HWND boundaries through screen coordinates.
- Static source visuals are retained by WPF. Layout changes invalidate only when
  source coordinates or dimensions actually change. Video/animated backgrounds
  update through the retained visual brush without a per-surface frame timer.
  Unloading removes layout subscriptions and releases surface-held visual caches.
- High contrast, disabled Windows transparency, unsupported shader hardware and
  absent sources use an opaque fallback. `GlassEnabled=false` exercises this path
  without changing Windows preferences. Reduced animation follows existing controls'
  `ClientAreaAnimation` checks.

Compile shader changes with:

```powershell
& PCL.Core/UI/Assets/Shaders/CompileLiquidGlass.ps1
```

The committed `.ps` is embedded in PCL.Core; ordinary builds do not need FXC.

## Lightweight page material

`FormMain` sets the inheritable `UseLightweightMaterial` attached property to `true`.
When a surface requests it and uses `Content`, `Chrome`, `Action` or `Input`, the surface
draws a rounded theme tint and border directly, without a `VisualBrush`, `BlurEffect`,
`LiquidGlassEffect` or coordinate `LayoutUpdated` subscription. Light theme raises the
minimum tint so text stays readable without local blur; dark theme keeps more transparency.
`Floating` popups and both `BottomTabs`/`BottomTabIndicator` materials never enter this
path, even when the inherited value is set. Pages can opt out locally with
`UseLightweightMaterial="False"` if a surface genuinely needs the full lens.
The dock continues to sample `SceneBackdrop`; it is not frozen or replaced by a snapshot.

## Layout changes

`ResponsiveColumnsPanel` gives the launch action area, recent/tasks cards and
theme/accent settings independent rows when their columns cannot fit. The navigation
overlays the full-height page at the highest sibling Z order. No bottom spacer or
navigation clearance is added to page viewports, fixed actions or the sidebar.
The home header no longer receives a second full-width category glass layer.

## Reproduce validation

Build the isolated executable using `Inject.targets` as described in `README.md`.
Create `glass-only.flag` beside it for the material pass; remove the flag for the
full navigation, localization, toolbar and settings pass. Configuration and test
instances remain under `%TEMP%/pcl-ui-efficiency-integration`.

Optional video fixture (FFmpeg):

```powershell
ffmpeg -f lavfi -i testsrc2=size=640x360:rate=30 -t 3 -c:v libx264 -pix_fmt yuv420p "$env:TEMP/pcl-liquid-fixture.mp4"
```

Material checks compare hardware screenshots with refraction enabled and disabled,
away from text/highlights; check idle invalidation; open a separate-HWND popup;
exercise asymmetric corners, light/dark themes, narrow/wide windows, a high-contrast
wallpaper and an optional real MediaElement video. CPU/private memory and WPF
composition callback intervals are diagnostics, **not GPU presentation benchmarks**.

`RenderTargetBitmap` does not execute WPF ps_3_0 effects. The full harness retains
its 100/150/200% raster layout captures and additionally saves `material-*.png`
HWND captures for glass appearance. Actual monitor DPI, high contrast, disabled
system transparency and reduced motion still need OS-level checks on target machines.
Real Microsoft sign-in/game launch/download completion require suitable account
and instance data; layout fixtures do not establish those end-to-end outcomes.

Build outputs, screenshots and the session validation report are saved under the
ignored `artifacts/liquid-glass/` directory; they are not part of the source patch.
