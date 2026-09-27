# Living HUD and garden update

## Changes

- Actual clock/weather glyphs align 12 logical pixels from the right and 8 from the bottom.
- Vinyl rotates about its own center under every HUD scale; notes stay inside the card's left slot.
- Optional checklist starts expanded each play run.
- Microphone status is in Esc → Display & audio; existing permission, mute and pause controls remain in Companions. Left Alt frees the cursor for music buttons; Enter resumes exploring.
- Seven clusters / 56 tiny fireflies: one renderer, 224 vertices / 112 triangles, no lights or collisions. Soft independent flashes every 4.6–8.4 seconds; nighttime only, dimmer in rain.
- Pond samples the same directional sky function, stars and sixteen photographic cloud cards. Reuses the existing atlas, with no reflection camera, render texture, GI update or capture. Water tint, shallow bed, shore fade, ripples and per-pixel fog remain.
- Location-triggered goodbye / welcome-back use the selected AI's current conversation. Debounced crossings, ten-second per-direction cooldown, short expiry, and no interrupting player/AI speech. Declined voice, manual pause, mute, focus loss and closed/out-of-range acoustic paths still prevent automatic voice behavior. Leaving the full hearing zone ends the mic session; returning reconnects only if voice remains enabled.
- Environmental prompts are explicitly labeled as game events and excluded from player captions. Exact generated wording and audio depend on the configured ElevenLabs agent and network.

## Verification and import

`CheckLivingHud.ps1` compiles only the changed files against current live scripts, preserving unrelated biometric work. Pure doorway and caption/playlist checks run without Unity or network. The guarded `Therapy Game → Finish Living HUD and Fireflies` installer backs up the scene and pond material, checks layout/matrix/voice/sky references, creates one mesh, and saves the open TherapyRoom. It never starts Play, recording, a voice-provider session or baking.

After Assets → Refresh, inspect `Assets/TherapyGame/Exterior/Fireflies/LivingHudCheck.txt`. Live visual/audio verification must be user-controlled.

## User-controlled smoke check

1. Begin a fresh Play run: checklist expanded, compact vinyl stays left while rotating, clock/weather hugs the bottom-right; no separate Mic off label.
2. Esc opens settings; Esc closes them. Left Alt releases cursor. Verify previous/pause/next music buttons and Enter to return to walking.
3. Use Sky & weather to compare noon, dusk, midnight and rain. Pond should show sky/cloud movement without becoming an opaque mirror; fireflies appear around garden clusters only after dusk. The old water checker bands must not return.
4. If you choose to enable voice, let a selected companion become idle, then walk slowly outside the open door. Expect a brief goodbye; return after a few seconds for a warm greeting. Continue talking near the open door; going farther away or closing the door outside must silence/stop the mic. Returning must never override an explicit mute/pause or declined consent.

## Sources

- Unity documents that rotation modifies GUI.matrix; the implementation now composes local pivot rotation after the HUD canvas transform: https://docs.unity.com/en-us/engine/6000.6/script-reference/unityengine/guiutility/rotatearoundpivot
- ElevenLabs documents that contextual updates alone do not trigger an agent response, whereas user-message events request a turn. Game-generated turns are clearly tagged and never displayed as player speech: https://elevenlabs.io/docs/eleven-agents/libraries/java-script
