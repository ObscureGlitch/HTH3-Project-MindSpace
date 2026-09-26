# Clouds, weather, and day/night

## In-game controls

Press **V**, then select **Sky & weather**. This uses the existing voice/settings panel; opening weather controls never starts the microphone.

- **Auto / Clear / Cloudy / Rain** chooses automatic weather or a manual override. Manual choices fade over roughly 20 seconds.
- **R**, outside the panel, switches to manual rain or clear skies. Choose **Auto** to return to the weather loop.
- The **time-of-day slider** and Morning / Noon / Sunset / Night buttons adjust the clock. Uncheck **Advance day and night automatically** to hold a time.
- Adjust full-day duration (4–30 real minutes), weather-loop duration (2–20 minutes), and rain strength separately.

Defaults are a **12-minute full day**, starting at **15:00**, and an **8-minute weather loop**: clear for 2½ minutes, cloudy for 1½ minutes, rain for 2 minutes, clearing for 2 minutes. Each weather phase blends from the last instead of switching abruptly. Sunrise is around 06:00 and sunset around 18:00. Weather continues when the time-of-day clock is held; manual weather pauses the automatic weather sequence until Auto is selected. Neither cycle catches up while the game is unfocused. These preferences are session-only, not a real-world clock or persistent save.

## Implementation

`Sky and Weather` under `TherapyRoom` contains a Play-only `WellnessSkyCycle` and 16 drifting cloud meshes. Each cloud uses the same 480-triangle cluster: **7,680 cloud triangles total**, with no colliders, shadow casting, light probes or reflection probes. Cloud cover increases and cloud colors darken during rainy weather; clouds taper at distant wrapping boundaries.

A simple texture-free URP sky shader draws a gradient, warm horizon, moving sun, moon, and sparse static stars. There is no volumetric rendering, raymarching, lightning, bloom pass, new camera, reflection capture, or GI update. The cycle reuses the scene's existing directional light for sun/moon illumination. Night retains a readable ambient floor, while the room's existing warm lights and baked lighting remain intact. Baked room illumination does not dynamically rebake or simulate changing indirect sunlight.

Outdoor haze, sky/cloud tint, ambient light, reflections and the existing pond's color respond to time and weather. Runtime material copies keep the authored assets intact. Disabling the component or leaving Play mode restores the borrowed lighting, sky, pond material, cloud transforms and rain override. Existing 30 FPS and low-load URP settings are not changed.

The existing rain controller retains the 600-particle cap, indoor filtering, voice ducking, and roof exclusion. Weather supplies a rain multiplier; rain strength remains a user control. The ElevenLabs agents, consent flow, mute/end controls, garden geometry, pond access and seating are preserved.

## Installation and verification

The one-use `WeatherCycleRequest.txt` installer runs after a normal Unity refresh, only outside Play/baking. If it defers, choose **Therapy Game → Install Weather and Sky Cycle**. It saves a copy of the open scene—including unsaved changes—at `Weather/Backups/TherapyRoom_BeforeWeatherCycle.unity` before adding the group. A separate delivery ZIP preserves the previously saved scene and changed runtime scripts.

**Therapy Game → Validate Weather Cycle (CPU Only)** checks midnight wrap, sunrise/sunset, noon/midnight contrast, 1,441 samples of sun direction and weather values, continuity at every weather boundary, clear/rain periods, and the rain override/cleanup math. The installer additionally checks mesh budgets, shader import errors, scene references, one directional light, retained seating, and an idle voice session. Its result is written to `Weather/WeatherCycleCheck.txt`.

Compiler, math, and saved-scene checks do not prove rendered appearance or performance on this laptop. No automated Play session, microphone session, lighting bake, or GPU preview is needed for this installation. A short user-controlled visual check is still needed when the laptop has sufficient headroom.
