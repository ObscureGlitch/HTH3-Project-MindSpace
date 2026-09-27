# MindSpace Presage bridge

This local Node.js sidecar emits newline-delimited JSON to Unity and uses Presage SmartSpectra 3.3.0 for on-device pulse, breathing, and HRV measurement. In the game, Unity owns the webcam and sends original RGBA frames over a current-user-only Windows named pipe (`--frame-pipe MindSpacePresage-<32 hex chars>`). The SDK uses custom input, not a second camera. Unity renders its own mirrored original-colour preview: SDK-processed video can become grayscale during calibration. Without `--frame-pipe`, standalone diagnostics retain the SDK-owned camera path. Video is never saved or sent to ElevenLabs.

## Local setup

1. Rotate the key that was shared in chat.
2. Install dependencies in this directory with `pnpm install --frozen-lockfile` (or `npm install`). The checked-in `pnpm-workspace.yaml` permits native build scripts only for SmartSpectra's exact pinned `koffi` and `protobufjs` dependency versions.
3. Set `TLW_PRESAGE_API_KEY` to the replacement key in the environment that launches Unity. `SMARTSPECTRA_API_KEY` is also accepted.
4. If `node` is not on `PATH`, set `TLW_NODE_PATH` to a Node.js 20+ executable.

Do not create a `.env` file inside the Unity project and do not store the key in an asset, scene, script, command-line argument, or build.

Run `node bridge.mjs --self-test` to verify the JSON protocol without opening a camera, contacting Presage, or consuming credits.
