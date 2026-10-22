# AndroidLiquidGlass attribution

Copyright 2025 Kyant. Licensed under Apache-2.0; full license in
`LICENSE.AndroidLiquidGlass` beside this file.

Source: https://github.com/Kyant0/AndroidLiquidGlass

Pinned revision: `65ab177e90e5c1d8c62e70cf7755841982da65f6` (`kmp`).
Reference release: `2.0.0-alpha03` (`978adadd219d3602d47e0671dc99faa0005537f6`).

`LiquidGlass.hlsl` adapts the rounded rectangle distance/gradient, circular lens
displacement and directional highlight from `backdrop/src/commonMain/kotlin/com/kyant/backdrop/internal/Shaders.kt`.
Changes: HLSL ps_3_0 conversion; guarded normalization; WPF coordinate/gutter mapping;
three-channel lightweight dispersion; composited tint; combined light/shadow rim.
It does not embed Compose, Skiko or the upstream Kotlin runtime.

The bottom-tabs material now uses the upstream seven-band dispersion weights,
8 DIP blur, 24/24 DIP container lens and progress-driven 10/14 DIP thumb lens.
`UI/Effects/LiquidTabMotion.cs` ports the spring specifications, press/release
sequencing, normalized drag and velocity deformation from
`catalog/utils/DampedDragAnimation.kt` and `catalog/components/LiquidBottomTabs.kt`.
`Controls/LiquidDockController.cs` in the launcher adapts those pointer/layer
contracts to WPF. The parameters are listed in `tools/fluent-ui-smoke/DOCK-PARITY.md`.

The spring-duration estimation in `LiquidTabMotion.cs` also adapts AndroidX
`compose/animation/animation-core/.../SpringEstimation.kt`, Copyright 2020 The
Android Open Source Project, under Apache-2.0 (same license text included here).
Reference: https://github.com/androidx/androidx/blob/androidx-main/compose/animation/animation-core/src/commonMain/kotlin/androidx/compose/animation/core/SpringEstimation.kt

Run `CompileLiquidGlass.ps1` after editing the shader. The compiled `.ps` is
embedded by PCL.Core so normal builds do not require an installed Windows SDK.
