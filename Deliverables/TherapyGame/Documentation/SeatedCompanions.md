# Seated voice companions

Julien uses the existing **Voice 1** ElevenLabs agent; Camille uses **Voice 2**. Neither remote agent configuration nor its public identifier is changed.

Approach the green-pillow end of the sofa and look at the character, then press **E**, or open **V → Companions**. Choose Julien or Camille. Only the selected character occupies that cushion. Agree to microphone use and press **Start conversation**. Switching characters requires ending the current call first.

Stay within **3 metres** and with a clear line of sight. Moving away or behind a solid obstacle ends the session; it does not silently reconnect. **M** mutes, **X** ends, and closing the panel with Escape or losing application focus also ends the conversation. Simply approaching or selecting a character never starts the microphone. The other sofa cushion and the three chairs remain available to the player.

Audio comes from the selected character's head. Conversation audio/messages are sent to ElevenLabs and the agent's configured services only after explicit Start. The game does not persist transcripts; provider retention may differ. These are AI companions, not clinicians or emergency services.

## Models and performance

Original GLBs remain unchanged in Downloads. A CPU-only Three.js GLTFLoader conversion preserves every triangle, all 30 material slots per model, the embedded notebook image, and 57 articulated transforms. The supplied assets have **rigid joint hierarchies, not weighted humanoid skins or animation clips**. The importer assigns one rigid bone influence per vertex and creates native Unity meshes/materials/prefabs, with no new package. Julien: 10,776 triangles; Camille: 15,214. Two skinned renderers per model (opaque body and transparent glasses); only one character renders at a time. Each body still has multiple material draw calls.

The author-time seated pose is supplemented by restrained breathing, head tracking, and a small speech cue; this is not phoneme-accurate lip sync. No runtime IK, cloth, additional lights, shadows, reflection capture, or automatic microphone activation. Units are metres.

The one-use installer backs up the open scene before adding characters. Menu fallback: **Therapy Game → Add Seated Voice Companions**. It refuses duplicate installs and checks existing agent IDs before changing links. Source code compiles and geometry/rig data are CPU-checked separately. **Live voice, rendered pose/material appearance, and performance require a user-controlled Play-mode check.** They are deliberately not exercised automatically on the laptop after its earlier GPU reset.
