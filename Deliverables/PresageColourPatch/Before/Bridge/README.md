# MindSpace Presage bridge

This local Node.js sidecar owns the webcam and emits newline-delimited JSON to Unity. It uses Presage SmartSpectra 3.3.0 for on-device pulse, breathing, and HRV measurement. During camera setup, a small mirrored preview is sent locally to Unity memory, then disabled on entry. Video is never saved or sent to ElevenLabs.

## Local setup

1. Rotate the key that was shared in chat.
2. Install dependencies in this directory with `pnpm install --frozen-lockfile` (or `npm install`). The checked-in `pnpm-workspace.yaml` permits native build scripts only for SmartSpectra's exact pinned `koffi` and `protobufjs` dependency versions.
3. Set `TLW_PRESAGE_API_KEY` to the replacement key in the environment that launches Unity. `SMARTSPECTRA_API_KEY` is also accepted.
4. If `node` is not on `PATH`, set `TLW_NODE_PATH` to a Node.js 20+ executable.

Do not create a `.env` file inside the Unity project and do not store the key in an asset, scene, script, command-line argument, or build.

Run `node bridge.mjs --self-test` to verify the JSON protocol without opening a camera, contacting Presage, or consuming credits.
