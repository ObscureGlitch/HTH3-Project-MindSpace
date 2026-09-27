# Character repair and speech animation

This update repairs the first import, not the original Downloads files. Before applying it, the installer backs up the current scene and the deployment script archives the original character assets/code.

## Geometry and seating

- Triangle directions now agree with outward normals; reflected source parts are handled separately. The first exporter incorrectly reversed every triangle without reflecting coordinates, causing back-face culling to hide exterior surfaces.
- Camille's source GLB contains a second, coplanar `beard_short` primitive over the `head_skin`. Its 1,260 duplicate triangles are omitted to remove the brown mask and z-fighting. Other intended geometry remains. Julien: 10,776 triangles; Camille: 13,954.
- The attempted seated repair correctly failed its fit check and rolled back (60 vertices intersected the cushion by up to 3.1 cm). Following the user's updated direction, both rigs now keep their **original standing proportions**. The selected character stands on checked, clear floor space beside the sofa. No leg stretching or forced seated bend remains. Both sofa cushions are available to the player again.
- Old meshes are retained. New body, face and glasses meshes are stored in `Characters/Refinement/Meshes`. Three renderers per character; only the selected character is active, and there are no extra lights or shadow casters.

## Lip sync

The previous time-based talking oscillation is removed. Five articulated mouth blend shapes (`A`, `I`, `U`, `E`, `O`) are driven by MFCC analysis of **the selected chatbot's actual AudioSource playback**, sampled at up to 30 Hz. Silence closes the mouth; interruption/end/character changes cannot leave a talking loop running. No microphone input or extra remote service is used by the analysis.

This is real audio-driven vowel lip sync, **not guaranteed word-perfect phoneme alignment**. Default male/female calibration profiles provide a starting point; the ElevenLabs voices may benefit from per-voice calibration. Consonants and language-specific phonemes are not individually authored in these supplied rigs. Live accuracy and latency need a user-started conversation test; no paid request or microphone session is started automatically.

The small MIT-licensed MFCC core is from [hecomi/uLipSync](https://github.com/hecomi/uLipSync), pinned to commit `060587907655235dba86dc7052d0b395c8ecd840`. Only Algorithm, Common, LipSyncJob, Profile and two sample profiles are included, with the MIT license. No microphone script, editor tooling, samples, neural model, or package-manager changes are included. Existing Unity Collections/Mathematics/Burst libraries are used.

## Idle behavior

Gentle breathing, shoulder movement, low-amplitude posture shifts, small head motion, front-facing attention tracking, and irregular 3–7-second blinks. E/V selection, Voice 1 → Julien and Voice 2 → Camille are retained. The later [room voice update](RoomVoice.md) replaces the initial three-metre range and per-conversation consent with room-wide/open-door reach and explicit per-play-session consent. M still mutes; X pauses/ends.

## Verification and controls

Use **Assets → Refresh** with Play stopped. The one-use importer refuses active Play/baking/voice sessions; fallback menu: **Therapy Game → Repair Companion Faces and Animation**.

CPU-only software previews inspect geometry from front/three-quarter angles and with different mouth shapes; they do not verify URP lighting/performance. Import checks geometry counts, winding, actual seat clearance, feet, blend shape names and the MFCC/blink math. No GPU preview, second Unity editor, Play mode, microphone, voice call, bake or reflection capture is started by the setup.

After importing, visually check both characters in Play mode. Enable voice only if you want to use the microphone and external voice service; otherwise choose Play without voice. Verify mouth closure during silence and interruptions, and end with X before switching characters. If classification or timing looks off, capture a short non-sensitive test and tune that voice profile rather than claiming word-perfect synchronization.
