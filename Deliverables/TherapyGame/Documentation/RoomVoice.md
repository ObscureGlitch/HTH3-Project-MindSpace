# Room-wide companion voice

At the beginning of each play run, movement is held behind a voice choice screen. **Enable voice & begin** explicitly permits microphone conversations with the selected companion for that run. **Play without voice** starts exploration without connecting or accessing microphone devices. Nothing is stored in PlayerPrefs or carried into the next run. The screen describes ElevenLabs/agent-provider processing and possible retention; it does not promise provider deletion.

After enabling, voice connects when the player is anywhere inside the therapy room, seated or standing. Furniture no longer breaks conversations. Outside, voice works within six metres of the entrance only while the actual door is open and the exterior path to the doorway is clear. It does not pass through the other walls. Closing the door while the player is inside does not interrupt indoor voice.

Voice volume decreases smoothly with distance, never abruptly cutting off at three metres indoors. Outside, distance follows the speaker-to-door-to-player sound path, the opening controls transmission, and the final 1.5 metres fade to silence. Exterior voice is positioned at the doorway. Only one short physics ray query runs at most ten times a second outside; no new lights, rendering, reverb simulation or geometry is added.

Leaving range or closing the door while outside cancels/ends the connection and silences audio. Returning reconnects only if voice remains enabled and unmuted. A persistent microphone status indicator is shown. **M** toggles mute; **X** ends and pauses voice; **V** opens controls to Resume, switch companions or withdraw permission. Close/Escape returns from controls without ending an ongoing call. Focus loss, network error, timeout and the existing maximum session duration pause automatic listening until Resume. They do not require repeating the consent screen. End the current call before switching companion. Muting is retained across a range exit and does not restart the microphone on return.

Permission is a runtime state; it cannot guarantee an external service's retention rules. No API keys, transcripts or microphone recordings are written by this update. No service calls occur in the installer/checks. Existing agent identities, characters, lip sync, weather and player seats remain intact.

## Verification

`Therapy Game → Configure Room Voice and Consent` runs CPU assertions and saves the existing room's links with a scene backup. The import report is `Assets/TherapyGame/VoiceAccess/RoomVoiceCheck.txt`. Checks cover all sampled room positions, seated height, extreme corners, open/closed/blocked/far exterior positions, monotonic attenuation, partial door opening, explicit consent, decline, mute, re-entry, pause/resume, withdrawal and new-run reset.

These are code/scene tests, not a live conversation test. In a user-controlled Play check, decline first and verify no mic/session, then enable via V. Walk around the room, out the open door, close it from outside, return, mute/re-enter, pause/re-enter, switch companions and revoke permission. Confirm the mic badge and audible fade. A new Play run must ask again. External voice tests use the selected agent service and microphone only after the user's consent click.
