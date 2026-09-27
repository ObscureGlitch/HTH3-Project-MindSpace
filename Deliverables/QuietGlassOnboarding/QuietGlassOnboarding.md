# Design A — Quiet Glass consent and camera check

Recreates the approved 1182 × 665 per-screen reference: centered cream translucent rounded surface, Source Serif 4 headings, Carlito supporting copy, three permission rows, quiet gray/sage switches, line icons, matching footer buttons, and a camera preview beside four status rows. Uses the existing live game view, not the generated background. The `A / QUIET GLASS` presentation label is intentionally omitted.

This is a code-native UI replacement, not a static mockup overlay. The paper-like surface uses transparency and subtle borders without adding a blur shader, second camera, scene clone, or capture pass. Camera video retains its original RGB rendering and aspect ratio. The existing positioning guide is preserved. Exact in-game appearance still requires a user-controlled Play screenshot; generated background scenery and lighting are not replicated.

## Permissions

- All draft choices are off on each play run. Changing a switch does not start any device or service. Continue is the affirmative confirmation.
- Voice, camera and proactive guidance are separate choices. The existing companion-camera integration requires voice; proactive guidance requires both. Removing a prerequisite clears dependent choices without re-enabling them later.
- Play without voice or camera clears all choices. The camera check offers continue without camera (voice retained), back (camera stopped), retry/resume, and camera switching.
- Opening a permission review stops the prior conversation/capture and resets drafts. Returning to gameplay does not repeatedly request consent for an already consented session.
- Detailed provider/data-sharing/retention disclosures and the AI-not-clinician explanation are accessible under Details & privacy. Companion choice is retained there and via its compact shortcut.

## Camera check

Camera frames, position, pulse and breathing statuses use the existing provider, not illustrative values. The provider's fresh/stable signal checks and 1.5-second final confirmation are unchanged. Camera-enabled entry rechecks them at click time. The small confirmation bar never pretends to represent overall acquisition progress. Errors/paused capture disable camera-enabled entry; retry, fallback and back remain available. Diagnostics retain separate pulse/breathing confidence, SDK state, full positioning advice and troubleshooting. No measured values are logged by this UI.

## Verification

`Check.ps1`: default-off/dependency/session-reset checks; live PresageCameraCheck readiness regressions; 63 scaled-control bounds checks; scoped full runtime/editor compilation against current live sources; static guards against device calls from draft controls; RGB preview preservation. `CheckTypography.mjs`: 47 real-font text-fit checks (headless approximation, not a Unity render).

No Play, camera, microphone, biometric session, AI connection, GPU preview, shader compile, or lighting bake is started by these checks. Only four runtime C# files and this documentation are deployed. Original voice script and meta are backed up outside Assets. No scene changes or installer are needed.

## Manual Play checks

1. Enter MindSpace. Confirm all three switches are off, text stays dark on hover, all controls fit, and the game remains visible behind the panel.
2. Play without voice or camera. Neither device should start. Reopen voice permission from the controls when desired.
3. If you choose to test devices: enable voice, then camera, optionally grounding; click Continue. Only then should the camera test start, with voice connection held until entry.
4. Test back, switching cameras, retry, focus loss, and continue without camera. A missing/stale signal must not enable camera entry. Diagnostics show why acquisition is waiting.
5. After a successful camera check, Enter with camera returns to the game with the existing voice and camera behavior.
