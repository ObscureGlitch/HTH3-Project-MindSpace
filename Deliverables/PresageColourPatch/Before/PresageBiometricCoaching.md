# Presage biometric coaching

MindSpace can use an ordinary webcam to estimate pulse rate, breathing rate, and HRV with Presage SmartSpectra, then provide stable numeric readings to the active ElevenLabs companion as private contextual updates.

## Data flow

1. The player explicitly enables voice and separately opts into camera-based wellness measurements for the current play session.
2. Unity launches `bridge.mjs` as a local Node.js sidecar. The sidecar owns the webcam and runs SmartSpectra locally.
3. The sidecar writes compact JSON readings to Unity over redirected standard output. During the camera test only, a low-resolution mirrored preview is sent locally to Unity, held in memory, and destroyed when the test closes. No preview or raw video is sent to ElevenLabs or saved to disk.
4. Unity ignores unstable pulse/breathing samples and samples below 60% confidence. It builds a personal session baseline before calculating a non-clinical relative activation heuristic.
5. At a low frequency, Unity sends stable values and the trend to ElevenLabs with `SendContextualUpdate`, so the information is background context rather than player speech.
6. If the player separately opted into proactive guidance and the trend remains elevated, Unity can request one spoken grounding exercise. Requests have an 18-second sustain requirement and a two-minute cooldown.

The coach never treats measurements as proof of anxiety, panic, deception, or danger. It avoids forced breath holds, uses one step at a time, and directs severe symptoms or immediate danger to appropriate emergency help. This feature is general wellness support, not medical monitoring, diagnosis, or treatment.

## Setup

1. Rotate any Presage key that has been pasted into chat or source control.
2. Run `Deliverables/InstallPresageCoaching.ps1 -InstallDependencies` from PowerShell. It stages the runtime, editor installer, documentation, and bridge into the live Unity project. It does not launch a camera or service session.
3. Set `TLW_PRESAGE_API_KEY` in Windows User Environment Variables. The Windows provider reads the saved user value on each camera start, then machine settings, then the inherited process environment. Do not place it in a scene, asset, `.env`, command-line argument, or build.
4. If `node` is not on `PATH`, set `TLW_NODE_PATH` to a Node.js 20+ executable.
5. Open `Assets/TherapyGame/Scenes/TherapyRoom.unity`. If the one-shot setup did not run automatically, choose **Therapy Game > Install Presage Biometric Coaching**.
6. In Play mode, enable voice and opt into the wellness camera. A required camera test blocks movement and voice connection until it passes. Keep the face and upper chest visible, well-lit, and reasonably still. The screen shows local preview and positioning guidance; entry requires fresh camera frames, Presage running, valid positioning, and stable pulse and breathing signals at 60% confidence for 1.5 seconds. It offers retry, camera switching, back, and continue without camera. Calibration can take a minute; failed or stale readings never unlock entry.

The Node package is pinned in `package.json` and `pnpm-lock.yaml`. Presage validates the API key/subscription over the network; metric computation remains local. The bridge opts out of aggregate SmartSpectra telemetry. Stable numeric metrics are sent to ElevenLabs only while the player has enabled both the wellness camera and voice for the current session.

## Environment variables

- `TLW_PRESAGE_API_KEY`: required replacement Presage key. `SMARTSPECTRA_API_KEY` is accepted as a fallback.
- `TLW_NODE_PATH`: optional full path to `node.exe`.
- `TLW_PRESAGE_BRIDGE_PATH`: optional full path to `bridge.mjs`, mainly for development.

The existing ElevenLabs integration uses public agent IDs and does not require an ElevenLabs API key in the Unity client.

## Camera troubleshooting

The preview includes a smooth white ID-check silhouette with a face oval, collar and curved shoulders. It keeps its proportions inside the video bounds and turns mint green when Presage accepts the position; the outline itself is a guide, not detected landmarks. The guide is one anti-aliased texture, avoiding rotated GUI lines inside the camera panel. Pulse and breathing confidence are shown separately. The bridge merges sparse SDK packets before throttling and expires each cached metric after eight seconds; waveform-only packets cannot erase fresh rates or extend their lifetime.

If the camera and Presage are running but pulse remains absent, check cardio/pulse authorization on the Presage subscription. The SDK can omit unauthorized metric fields without reporting a startup error; see the [official metrics guide](https://smartspectra.presagetech.com/docs/nodejs/metrics/). Missing or low-confidence signals still block camera-enabled entry.

- The camera starts after consent; numeric context is sent only during a connected voice session.
- A saved key alone is insufficient if Node is absent from Unity's PATH. Set `TLW_NODE_PATH` to the full installed `node.exe` path. Windows saved values are picked up when measurements start or resume.
- `Place more of the chest in view` means the camera opened but the breathing measurement needs a wider view. Include the face, shoulders, and upper chest; remain still in even lighting.
- The compact 202 × 108 pulse card above the music controls uses a dark red grid, luminous red scrolling trace, BPM value and signal status. It shows `--` and an empty grid while the signal is unreliable, stale, or paused. Its rate-driven trace is decorative, explicitly labeled `BPM animation · not an ECG`; it is not an electrical recording or a medical monitor.
- During setup, the local preview targets 12 fps (previously 4), drops frames under pipe backpressure, and uploads only the newest preview each Unity update. Metrics can update at 10 Hz in setup; readiness changes and regressions bypass throttling. Gameplay retains the normal 2 Hz delivery limit for unchanged readiness. Pulse and breathing acquisition run together; SDK stability flags, confidence thresholds, measurement resolution and freshness checks are unchanged. Only the additional app confirmation was shortened from 3 to 1.5 seconds. The UI shows elapsed time and which signal is still calibrating, not a fabricated acquisition percentage. Initial SDK acquisition time is not guaranteed to decrease.
- Losing game focus pauses capture. Use the voice controls' **Resume measurements** button after returning.
- `Deliverables/CheckPresageStartup.ps1` performs a bounded camera test using the saved user key. It prints status and event counts only, stops automatically, and saves no video or biometric values.
