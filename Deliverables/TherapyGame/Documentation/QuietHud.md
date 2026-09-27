# Quiet HUD refinement

Gameplay panels have no background by default. Ivory outlined text preserves contrast; F1 settings offers an optional dark backdrop and larger captions. Consent and settings dialogs deliberately retain readable backgrounds. Checklist starts collapsed (J expands).

- Bottom-left: previous song, pause/resume, next song. Press Esc to free the cursor and click, or use [ / P / ]. Skipping while paused stays silent; resuming starts the selected song. A stable shuffled cycle makes Previous/Next reversible.
- Top-right: actual game HH:mm, sun/moon, rain/cloud indicator. Actual rain intensity takes precedence while weather transitions. Clock uses the existing sky cycle, not system time.
- Captions: no timed pages. Text follows audio character timestamps when available, or streaming AI text chunks. A brief soft reveal and continuous line scrolling use elapsed time, not a per-frame character counter. Final-message fallback waits 0.2 seconds for alignment then displays available text. Idle is '...'.
- The entire caption region disappears outside the existing room/open-door talking zone, or when voice is not connected/connecting. Mic status remains visible. Voice consent, door logic, attenuation and reconnection rules are unchanged.

Provider limits: user_transcript is a finalized utterance, not partial speech. Player text is displayed immediately on receipt. The current ElevenLabs SDK exposes audio chunk alignment but not a public exact audible playback cursor; timing is scheduled from ordered chunk receipt, so real network/audio latency still needs an actual play check. agent_chat_response_part must already be enabled for voice conversations to receive streaming text. No remote agent configuration, extra recorder, new STT provider or microphone session was created.

Sources: https://elevenlabs.io/docs/eleven-agents/customization/events/client-events and the installed ElevenLabs Unity SDK's typed event definitions.

Verification: CheckQuietHud.ps1 runs pure caption/playlist tests. TherapyQuietHudSetup also checks layout, zone visibility, game-time weather icons and room-wide voice regressions during the one-time import, then saves panelOpacity=0 in TherapyRoom. No GPU render, second editor, Play mode, microphone or network session is needed for those checks.
