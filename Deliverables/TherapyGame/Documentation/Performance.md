# Smoothness and rendering changes

The garden no longer forces 30 FPS. Desktop builds use display-synchronized presentation; the editor's Game view has a configurable 120 FPS ceiling because it ignores vSync. These are targets, not a promise that the hardware can sustain them. The previous frame settings are restored when the garden controller is disabled.

Pond scene reflections retain their 512 × 512 HDR texture but update at most 12 times per second while moving and 4 while stationary, rather than 20. The reflection camera skips secondary shadows, post-processing, and color/depth copies. This trades some reflected-scene motion detail for lower rendering cost. Ripple normals and wind motion still update with the main view. Turning off `WellnessPondWater.sceneReflections` now clears the last captured image and returns to the procedural sky reflection.

The 128 × 128 wave solver runs at its existing 60 Hz while active, then sleeps after displacement falls below one micrometre and velocity below ten micrometres per second. Rain and footsteps wake it again. Offscreen physics continues; only the texture upload waits until the water becomes visible. Visibility checks reuse a plane buffer.

HUD music/status truncation and caption wrapping reuse measurements until their text, style, or available width changes. Labels use one small shadow instead of a double outline for a lighter appearance and fewer glyph draws. Buttons and input events keep their existing behavior.

## Validation

- Runtime and editor assemblies compile against Unity 6000.6.3f1 / URP 17.6.
- `Deliverables/PerformanceChecks.cs` checks ripple propagation, conserved mean water level, dry boundaries, sleep, wake-up, and small-pond storm stability. Run with PowerShell `Add-Type` alongside `Runtime/PondWaveField.cs`, then call `[PerformanceChecks]::Run()`.
- The isolated pond validator can be run from **Therapy Game → Validate Pond Water** to check the actual reflection camera and shader without entering Play.
- For a comparable gameplay FPS measurement, use the same resolution, view, weather, and hardware before/after. Check movement in the garden, stationary water, rain/wading, night sky, captions, and settings. Gameplay FPS has not yet been measured for this change.

The installation script backs up the exact live files before copying. No scene, material, texture, or saved graphics-quality asset needs replacement.
