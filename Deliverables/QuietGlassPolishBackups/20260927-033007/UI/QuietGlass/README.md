# Quiet Glass — approved Design A

The pause menu uses the approved 1672 × 941 composition, centered and uniformly scaled for other window sizes. The background is the actual game, with cached translucent ivory layers instead of a live-blur render pass. Headings use Source Serif 4; helpers use Carlito. Their redistribution licenses are in `Fonts`.

## Controls

- Esc: open Audio & voice; Esc or Resume: return to the game.
- Sidebar: Audio & voice, Display & comfort, Sky & weather, Controls.
- Music: live track title/artist, previous, play/pause, next. Hover truncated credits to see their full value.
- Audio: music level, nature/rain level, companion playback level. Existing distance-based voice attenuation stays active.
- Microphone: review consent when permission is absent; otherwise resume/mute/unmute. “Choose companion & voice controls” opens the existing companion selection, transcript, typed input, permission withdrawal, and optional wellness-camera controls.
- Display: reminders, captions, larger captions, reduced menu motion, HUD backdrop.
- Sky: existing time, weather, rain, clouds, shooting stars and aurora controls.
- Reset defaults: confirmed, page-specific; never grants voice/camera permission or starts a session.
- Exit game: confirmed; ends the play session (stops Play in the Editor).

## Safety and verification

Opening the menu keeps existing conversation behavior and blocks player movement. No new microphone reader, shader, rendering camera, reflection capture, or post-processing volume is created. Preferences are per play session, as before; consent is never persisted.

`Therapy Game → Install Quiet Glass Pause Menu` installs only the menu flag/font references on the existing voice controller and saves TherapyRoom. A one-time request runs the same guarded installer after Refresh. Scene backups go outside Assets to `TherapyBackups/QuietGlass`, avoiding duplicate scene imports.

The CPU-only checker is `Deliverables/CheckQuietGlass.ps1`: 168 reference/layout checks, 278 existing caption/playlist checks, 78 doorway-conversation checks, plus runtime/editor compilation against current project sources.

After import, verify manually with Play: choose voice or no voice, press Esc, compare the main page to Design A, drag all three sliders, try music controls, browse pages, test confirmations/cancel, return to the room. Verify microphone controls only if you want to enable voice. A Unity screenshot is needed for final visual acceptance; the code checks do not claim pixel-identical live rendering.
