# Night Sanctuary main menu

Recreates the approved C concept's left-hand ivory serif title, spaced subtitle, transparent navigation, fine rounded sage outline and small selection sliver. The concept's label is not part of the game. Text colors stay constant on hover. A small cached dark gradient preserves contrast in daylight without an opaque menu panel.

Main-menu interaction polish: moving the mouse anywhere inside an option's full row selects the same sage outline as arrows/Tab. Changing options plays one 240 ms scale pulse, peaking at 102.5% and settling to the original size. Text color stays ivory; there is no blinking or idle wobble. Click targets remain at their original bounds. A stationary pointer does not override keyboard navigation or continually restart the animation, and subpages/entry transitions reject hover selection. This only affects the main-menu navigation, not the pause menu or HUD.

The background is **the existing Unity garden**, not a generated still. The existing player camera temporarily views the pond and cabin from the east bank. Water ripples, sky reflections, clouds, nature ambience, fireflies and sky effects use their existing systems. No second camera, environment clone, custom render pass, or new shader is introduced. The reference image is an artistic interpretation of the game, so the existing cabin/landscape geometry is retained rather than claiming a pixel-identical background.

The menu opens at 01:30 with a six-real-minute day/night cycle and clear weather. A title-only aurora composition uses the existing runtime sky and pond materials. Upon entry, the game's normal random aurora system takes over; normal weather mode and cycle duration are restored while the current hour continues. Optional gentle camera drift is off by default. Main-menu Settings offers music, nature volume, camera drift, background time cycling, captions, and morning/noon/sunset/night previews.

Enter fades to the original player view, then restores the original per-session consent screen. Player input, HUD, voice chat, camera measurement provider and biometric coach are disabled while on the title screen. Settings does not request voice/camera consent or start any service. Consent remains affirmative, optional and separate for camera/voice. No saved consent is inferred. Main-menu Help, Credits and exit confirmation are functional; Esc returns from subpages, arrows/Tab select home actions, Enter activates. No new save/continue system is implied.

Installation adds one `WellnessMainMenu` component to TherapyRoom and places TherapyRoom first in build settings, retaining all other scene entries. It saves recoverable scene/build-settings backups outside Assets. Existing runtime files are not replaced. The original camera pose, FOV, scene geometry, material references and edit-mode sky settings are preserved.

Verification: pure state-machine checks, full scoped runtime/editor compilation, layout bounds at six display sizes, mathematical cabin framing, ground clearance, unchanged camera/renderer counts, scene GUID persistence and build entry. No automatic Play, GPU screenshot, lighting bake, reflection capture, microphone or camera session is performed. Visual scene framing, actual input and consent transition remain user-controlled Play checks.

Hover verification adds 300 inside/outside hit-target checks at six display sizes and bounded, non-retriggering animation checks at 30/60/120 FPS. In Play, sweep across all five rows (including their blank right-hand area), leave the pointer still and use arrows/Tab, then open/back out of Settings. Only the newly selected row should pulse once; moving inside the same row should not replay it.

Manual installer: **Therapy Game → Install Night Sanctuary Main Menu**.
Report: `Assets/TherapyGame/UI/MainMenuCheck.txt`.
