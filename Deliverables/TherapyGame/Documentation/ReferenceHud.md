# Reference-inspired HUD and shared captions

The room now uses warm cream, rounded, translucent cards with muted sage accents. There is no heart-rate widget or fabricated biometric value. Transparency is a small cached UI texture, not scene blur or another camera.

- Top left: optional **When you're ready** checklist. It counts settling into the room, a real user transcript or typed message, trying the existing breathing orb, and noticing three distinct grounding objects (journal, stone, plant or light). The wording says “in the room” because the existing targets are not all on the shelf. No progress is invented or persisted. J collapses the card.
- Top right: **Settings / F1** and a microphone-state indicator that remains visible even with captions hidden. Display settings include caption visibility, larger text, checklist visibility, panel opacity and music volume. Voice and weather controls remain available.
- Centre: contextual interaction pill and unobtrusive aiming dot, linked to the existing player's seat, door and object focus. Existing seating fades and reflection notices are retained.
- Bottom left: current track name, actual playlist position, elapsed/duration bar and pause/resume. **P** controls music; **M** remains microphone mute, so the reference's music shortcut is intentionally adapted.
- Bottom centre: a shared caption card labelled **Julien/Camille · AI COMPANION** or **You · YOUR VOICE**. Actual ElevenLabs response/user-transcript events feed the text. Corrections replace the matching response; interruptions and disconnects clear stale captions. The waiting state is exactly **...**. VAD shows “Listening…” before the service supplies words. This SDK supplies user-turn transcripts, not guaranteed word-by-word partial transcription; no extra microphone or paid STT integration is added.

Long text is split into readable pages with automatic reading-time advancement and an overflow scroll area. The full recent transcript remains in V controls. Paging is not word-level audio alignment. Captions are plain text: provider content cannot apply rich-text markup. No transcript/caption content is written to disk by this update.

**V** voice controls; **T** opens/focuses typed input when connected; **Enter** sends typed text; **Tab** or **X** pauses/ends voice (“Not now”); **M** mutes. **F1** opens settings. Game movement is blocked while a modal is open; closing settings leaves voice unchanged. The existing per-play-session consent and whole-room/open-door distance behavior are preserved.

## Verification

The installer backs up the scene, links one HUD, disables only the replaced legacy banners, validates six resolutions, tests caption states and reruns all room-voice assertions. A standalone CPU caption test exercises corrections, player/bot turns, waiting, mute, interruption, pagination, Unicode and reset. A CPU layout preview is an approximation of the layout, not a Unity screenshot.

No Play mode, microphone, remote session, rendering or bake is started by installation. In a user-controlled play check, inspect the HUD, J/F1/P/T controls, real interaction progress, both companion/user captions, **...** while waiting, and interruption/door-exit resets. Check consent and microphone status before starting any external voice test.
