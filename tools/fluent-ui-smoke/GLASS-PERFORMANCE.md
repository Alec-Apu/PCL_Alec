# Liquid Glass retained rendering performance

> Update 2026-10-23: ordinary page `Content`/`Chrome`/`Action`/`Input` surfaces now use the
> lightweight material by default and no longer execute the per-surface sampling, blur and
> shader path measured below. The dock and `Floating` popups keep full glass. Current state,
> Release A/B results and limitations are in `docs/HANDOFF.zh-CN.md` §2.3 and
> `artifacts/liquid-glass/global-lightweight-2026-10-23/SUMMARY.md`. The numbers here remain
> historical evidence for the full-glass path, not the current page rendering cost.

2026-10-22, local WPF test instance: rendering tier 2, ps_3_0 supported,
process/window render modes Default, DisableHardwareAcceleration=False.
These establish hardware capability/configuration, not a GPU trace of each operation.

Previously each surface OnRender constructed new VisualBrushes, BlurEffect,
LiquidGlassEffect and clip geometry, and reopened intermediate DrawingVisuals.
The graph now persists until unload; source/viewbox, shader constants and blur
properties update in place. Intermediate drawing commands are rebuilt only when
bounds or fallback brush changes. Native foreground text remains separate.
The dock retains its tab overlay and per-tab brushes/drawing transforms, rebuilding
the tab drawing only when content geometry, direction or container width changes.
Upstream spring parameters, lens math, highest-layer placement and full page height
are unchanged.

Controlled UI-thread benchmark (`glass-perf.flag` in the isolated smoke build):
20 warmup + 600 press-state surface render recordings against a loaded source.
Before: 50.51 ms CPU, 8,290,192 managed bytes (13,817 bytes/render).
After: 11.29 ms CPU, 625,088 managed bytes (1,042 bytes/render).
This is about 78% less recording time and 92% less allocation in this local run.
It excludes GPU execution, presentation, full-window layout and other launcher work;
it does not establish a proportional FPS gain. Single-run timings are diagnostic.

Validation: normal and isolated builds pass; 19 motion/navigation tests pass;
dock click/drag/interruption/capture loss/idle tests pass in light/dark modes;
glass grid refraction, static render count (6 -> 6), popup sampling, Gaussian/Box,
bottom text-through, responsive layouts and live MediaElement sampling pass.
Source/background changes remain live through retained VisualBrushes.

Raw evidence lives in artifacts/liquid-glass/perf-before.txt, perf-after.txt,
performance-dock-report.txt, performance-glass-report.txt and performance-build.log.
Normal executable for the current package: artifacts/release-2026-10-23-global-lightweight/app/Plain Craft Launcher 2.exe.
The smoke harness is injected only into isolated builds, never the normal launcher.
