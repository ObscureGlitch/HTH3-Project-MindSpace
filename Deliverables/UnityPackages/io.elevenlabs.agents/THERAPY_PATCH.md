# Therapy-game native startup safety patch

Upstream: https://github.com/elevenlabs/unity
Commit: 375e152dda6f7c29cb291525d7faa2872dad6e60 (SDK 0.1.0, MIT).

This embedded copy retains the upstream runtime, editor integration, WebGL plugins and license. Samples/tests/development tooling are omitted from the installed copy.

Two source files differ from the pinned SDK:

- `Runtime/Core/ConversationOptions.cs` exposes `StartupCancellationToken`.
- `Runtime/Native/NativeSessionLauncher.cs` passes it to the existing cancellable WebSocket handshake, checks cancellation before acquiring microphone/output devices, and closes already acquired devices if startup is cancelled. It also forwards the configured input device.

The therapy UI uses this only for explicit cancellation and a 15-second native connection timeout. It does not change server agents, use API keys, connect automatically, or add analytics. This patch is native-specific; WebGL startup cancellation is not verified.
# Packaging note

Unneeded upstream samples/tests are omitted from this embedded copy; `package.json` no longer advertises their missing directories. All runtime, editor, plugin code and the upstream MIT license are retained.
