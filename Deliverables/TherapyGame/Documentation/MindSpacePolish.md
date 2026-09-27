# MindSpace exterior and compact HUD

The update changes only the therapy house and its HUD. Three original cabin batches (siding, corner trim and gables) are preserved but disabled. The roof, porch, existing windows, room furnishings, door behavior, garden, NPCs, voice consent and microphone logic are retained.

## Exterior

- Continuous cedar backing and evenly spaced planks around the actual door/window openings. Above-door siding now spans the lintel. Equal corner posts overlap the two adjacent faces symmetrically.
- Full-depth oak doorway reveals cover the cut siding ends without entering the existing door opening.
- Broad triangular attic glazing with a real opening, three slim mullions and an oak frame. No image backdrop or new reflection camera.
- Sage sign to the right of the entrance reading MINDSPACE / COUNSELING / CENTER. The lettering is actual mesh geometry, with no external font dependency.
- Small lantern, window flower box, two planters and restrained climbing greenery. No extra real-time lights, physics bodies or particle systems.
- Original warm plaster material and UV layout transferred to all four replacement interior wall panels. Original baked lighting is reused when available; interior probe fallback is reported explicitly if not.
- A small ExecuteAlways component retains the original atlas and restores those four assignments after loading. The importer tests the restore path by clearing and restoring the indices without Play mode. Unity reference: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Renderer-lightmapIndex.html

## HUD

- Music widget is 236 × 78 logical pixels (31% less area than the previous 252 × 106), with a 43%-opaque dark rounded backing.
- Song title and original creator/uploader handle. The seven bundled MP3s have no embedded title/artist tags in their headers; credits preserve the prefixes supplied in their filenames: alex-morgan, andriig-soft, andriih-soft, apalonbeats. No artist identity is invented.
- Previous / pause / next remain clickable after Esc releases the cursor, with [ / P / ] shortcuts. Cached 64px vinyl rotates slowly only while playing. Three tiny note sprites drift within the compact card; there is no Unity ParticleSystem.
- Existing game clock and weather indicators moved to bottom-right.
- Subtitles have a 0.3-second eased fade/slide per speaker turn and gently animated waiting dots. Existing streamed text and character-timed reveal remain; no additional transcription delay or speech service is added. Caption visibility still follows the talking zone.

## Verification and recovery

CheckMindSpaceGeometry.ps1 numerically tests the same geometry constructor without Unity, including 322 doorway clearance samples, coverage, and a 7,000-triangle cap. The CPU preview omits existing room interiors and Unity lighting, so it is not a live game screenshot. The installer repeats geometry checks in Unity, validates HUD layout/voice regressions, saves the scene, and writes Exterior/MindSpacePolish/PolishCheck.txt.

Before import, the previous scene and three changed runtime files are zipped outside the Unity project. A second scene copy is saved under Exterior/MindSpacePolish/Backups before editing. No GPU preview, Play mode, microphone, service connection or bake is started by the update.
