# Voice companions and gentle rain

This adds the two existing ElevenLabs agents from [ObscureGlitch/Hackathon26](https://github.com/ObscureGlitch/Hackathon26) to the existing TherapyRoom. It does not replace the room, garden, player, or seating.

## Controls

- **V:** open voice/weather controls. Movement and seat shortcuts are paused while the panel is open.
- Choose **Companion 1 · Yellow** or **Companion 2 · Red**, read the microphone disclosure, tick consent, then click **Start conversation**.
- **M:** mute/unmute outside the panel; **X:** end outside the panel. The panel also has Mute and End buttons.
- **R:** toggle rain outside the panel. The panel includes a rain intensity slider.
- **Continue walking** closes the panel but keeps an active conversation alive, with a visible microphone indicator.
- Closing the panel with **Esc** or **Close & end conversation**, leaving game focus, disabling the component, or reaching the ten-minute session limit ends the conversation.

No microphone or remote conversation starts on import, on entering Play mode, or on approaching furniture. Mute sends silence while the input device remains open; End releases it. Typed messages are available during the active voice session, not as a separate microphone-free chat mode. In-session transcripts are limited to 12 entries in memory and cleared on end; they are not written to files by this integration. ElevenLabs and the configured agent services may have their own retention policies. These are AI companions, not clinicians or an emergency service.

## Configuration

`Assets/TherapyGame/Integrations/Settings/VoiceCompanions.asset` holds public agent IDs, their color dynamic variable, a 15-second native connection timeout, and a ten-minute session limit. The two IDs are taken from the repository's committed `TalkingBoxAgentConfig` assets. Server-side agent prompts, voices, tools, and account settings are left unchanged.

Windows microphone permission and Internet access are required for a conversation. Agent availability, account credits, and public-agent access have not been tested by initiating a billable session. An agent that requires authentication needs a trusted server to supply a signed session URL; **do not put an ElevenLabs API key in Unity or change agent security just to bypass an error**. See [ElevenLabs authentication](https://elevenlabs.io/docs/eleven-agents/customization/authentication).

The integration targets the current Windows Editor/standalone project. WebGL/mobile permission flows and the local native cancellation patch are not claimed to be verified on those platforms.

## Lightweight weather

Rain particles spawn around the player only, avoid the cabin roof footprint, and expire just above a CPU-raycast surface. There is a hard limit of 600 particles, no particle collision simulation, no rain shadows, no extra render cameras, and no postprocessing or lighting bake. Audio fades and is low-pass filtered indoors, and becomes quieter during voice conversations. The source audio is imported as mono 22.05 kHz streaming Vorbis. Existing 30 FPS and low-load URP settings stay unchanged. This is a deliberately gentle effect, not a storm or GPU-heavy weather system.

## Sources and licensing

- Source game: `ObscureGlitch/Hackathon26`, commit `c9a8f8643ec79cc48080191824867a372b2e8a40`. Adapted the two agent configurations and indoor/outdoor rain concept; did not copy the sample scene or its automatic trigger-to-microphone behavior.
- Official [ElevenLabs Unity SDK](https://github.com/elevenlabs/unity): version 0.1.0, commit `375e152dda6f7c29cb291525d7faa2872dad6e60`, matching the source game's package lock. Embedded at `Packages/io.elevenlabs.agents`; upstream MIT license retained. `THERAPY_PATCH.md` records the small native startup cancellation/cleanup change. No network package installation is needed during normal project opening.
- Rain audio: the repository's `Assets/Sounds/boons_freak-rain-sound-188158.mp3`, recovered from Git LFS and copied as `Integrations/Audio/GardenRain.mp3`. Verified SHA-256: `CA25C3C85F4FD56ABF29EC7A59FC75D243BFBE12160F4F0870FC4F18C5E2C63E`. The source repository does not supply a separate license for this recording. Confirm redistribution rights before publishing the game.

## Installation and checks

Keep TherapyRoom open and stop Play/baking before Assets → Refresh. The one-use `VoiceRainRequest.txt` importer creates a `Voice and Rain` scene group, settings, and rain material, then saves the existing scene. If the marker says `manual-only`, choose **Therapy Game → Install Voice and Rain**. It checks references, rain budget, indoor/outdoor math, idle microphone state, and the retained room/garden/seating. Results are written to `Integrations/VoiceRainCheck.txt`.

The pre-integration scene is preserved at `Integrations/Backups/TherapyRoom_BeforeVoiceAndRain.unity`; a separate delivery ZIP also preserves the original scene, player script, runtime assembly definition, and package manifests.

Verification deliberately avoids Play mode, GPU rendering, microphone capture, and paid/network voice sessions because of this laptop's previous GPU resets. CPU compilation and installation checks do not prove live audio quality, account access, or visual appearance; those need a short, user-controlled play check when the laptop has sufficient headroom.

### Verified on 2026-09-26

- All six C# assemblies compiled using the installed Unity compiler; Unity subsequently compiled and loaded them during the normal refresh. Only upstream SDK/old project warnings were reported in the CPU check.
- Unity's one-time installer finished at 15:52:05 local time and saved TherapyRoom. Its reference, roof exclusion, indoor blend, rain lifetime, and retained-room checks passed.
- `Deliverables/AuditVoiceRain.cjs` passed all 15 saved-scene/package checks: correct agent IDs, audio/material/component references, particle budget, stream settings, and embedded package resolution. All 2,173 pre-existing non-transform scene objects were unchanged from the immediate pre-integration backup. Four new GameObjects were added.
- The existing garden audit passed all 20 checks, including five seating positions, 13 butterflies, pond access, front window, 57 native mesh references, and the low-load rendering configuration.
- Live microphone, agent/account access, spoken response quality, and rain appearance remain untested. No conversation or paid voice request was initiated by the implementation checks.
