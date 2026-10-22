# LiquidBottomTabs motion port

Reference: Kyant0/AndroidLiquidGlass, `kmp` revision
`65ab177e90e5c1d8c62e70cf7755841982da65f6`:

- [LiquidBottomTabs.kt](https://github.com/Kyant0/AndroidLiquidGlass/blob/65ab177e90e5c1d8c62e70cf7755841982da65f6/app/src/commonMain/kotlin/com/kyant/backdrop/catalog/components/LiquidBottomTabs.kt)
- [DampedDragAnimation.kt](https://github.com/Kyant0/AndroidLiquidGlass/blob/65ab177e90e5c1d8c62e70cf7755841982da65f6/app/src/commonMain/kotlin/com/kyant/backdrop/catalog/utils/DampedDragAnimation.kt)
- [InteractiveHighlight.kt](https://github.com/Kyant0/AndroidLiquidGlass/blob/65ab177e90e5c1d8c62e70cf7755841982da65f6/app/src/commonMain/kotlin/com/kyant/backdrop/catalog/utils/InteractiveHighlight.kt)

The former 480ms spline/leading-edge animation has been removed. The C# motion
model uses the original unit-mass spring specifications and gesture state machine.

| Behavior | Upstream parameters / WPF port |
|---|---|
| Equal tab width | `(containerWidth - 8) / visibleTabsCount`; container 64 DIP, thumb 56 DIP, inset 4 DIP |
| Position | damping ratio 1, stiffness 1000, displacement threshold .001 |
| Drag velocity | damping .5, stiffness 300, threshold .01; quadratic recent-position velocity fit |
| Press progress | damping 1, stiffness 1000, threshold .001 |
| Horizontal press scale | damping .6, stiffness 250, 1 → 78/56 |
| Vertical press scale | damping .7, stiffness 250, 1 → 78/56 |
| Velocity deformation | X divides by `1-clamp(velocity/10*.75,-.2,.2)`; Y multiplies by `1-clamp(velocity/10*.25,-.2,.2)` |
| Release | wait a frame, then until distance < `(tabsCount-1)*.025`; release press/scales, decay velocity |
| Drag | horizontal movement / tab width, bounded to first/last visible tab, mirrored for RTL |
| Snap / cancel | nearest tab, no fling; same finish path on capture cancellation |
| Container movement | ±4 DIP × EaseOut(normalized accumulated drag), return spring damping 1 / stiffness 300 |
| Container press scale | `1 + 16/containerWidth * pressProgress` |
| Interactive light | damping .5 / stiffness 300; uniform .08 + radial .15 additive white |
| Hidden label layer | content scale 1 → 1.2; source accent #0088FF / #0091FF; revealed by thumb sampling |
| Container glass | 8 DIP blur, vibrancy 1.5, 24/24 DIP refraction, #FAFAFA/#121212 at 40% |
| Thumb glass | 10×progress / 14×progress refraction, original seven-band dispersion weights |
| Thumb surface | light black / dark white at .1×(1-progress); black .03×progress |
| Thumb shadow | 24 DIP, offset (0,4), opacity .1×progress |
| Inner shadow | radius 8×progress, strength .15×progress |

`LiquidSpring` solves critical and underdamped springs analytically using elapsed
time instead of a duration/easing approximation. Retargeting preserves current
position and velocity. Like Compose, it samples at integer milliseconds and uses
the AndroidX envelope/Newton duration estimation, rather than an arbitrary
"small enough" stopping condition. Rendering is subscribed only while motion/gesture state is
active and is unsubscribed on settlement or unloading. Reduced motion/high contrast
snap directly to the selected geometry.

Tests in `LiquidTabMotionTest` compare the critical/underdamped equations, frame-rate
independence, retarget continuity, drag normalization, snapping, velocity deformation,
release sequencing and complete rest. Run them with the navigation tests:

```powershell
dotnet test PCL.Core.Test/PCL.Core.Test.csproj -p:IsTestProject=true --filter 'FullyQualifiedName~LiquidTabMotionTest|FullyQualifiedName~LauncherNavigationTest'
```

The isolated `dock-only.flag` pass drives the same WPF gesture adapter with synthetic
coordinates, verifies selection after drag/capture cancellation, checks interrupted
clicks and idle settlement, and saves hardware screenshots in both themes. Synthetic
gesture input avoids moving the user's actual pointer.

The math, constants and interaction sequencing are ported from the pinned source.
This is a WPF/HLSL rendering port: the launcher's horizontal icon/text arrangement,
five destinations, WPF Gaussian rasterization, inner-shadow rasterization and glyph
antialiasing are platform adaptations. No claim of cross-engine pixel identity or
original-Compose-versus-WPF video/frame comparison is made.
