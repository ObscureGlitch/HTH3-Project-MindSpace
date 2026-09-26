# Nighttime fog, pond and entrance door fixes

- Removed the fixed orange camera fog indoors (density zero). Outdoor haze still follows the sky/weather color, including the blue night horizon, and blends in gradually beyond the room boundary. Room lamps and baked lighting are not changed.
- Removed the pond shader's multiplied crossing sine brightness bands, which formed a checker/lattice that stood out against the dark nighttime tint. Ripples now perturb the sheen in similar, gently warped directions. Fog is calculated per pixel instead of interpolated over the water fan's large triangles. Added URP soft-shadow variants and shadow-distance fading, with reduced glint contrast. No render textures, new textures, extra cameras, water geometry changes or higher quality settings.
- Look at either face of the existing entrance door within 2.6 metres and press **E** to open/close. The prompt describes the available action. The solid leaf collider moves with the door; the room starts in its existing open pose. Stand clear of its swing: the action refuses if the player occupies the arc, and rechecks while moving. Press E again to reverse an unobstructed swing. The original pose is restored on leaving Play mode.

## Safe installation

With Play mode stopped and TherapyRoom open, choose **Assets → Refresh**. A one-use gate installs the component references and removes the indoor fog. If deferred, use **Therapy Game → Fix Night Fog Pond and Door**. An editor backup of the current scene (including unsaved edits) is kept at `Exterior/Backups/TherapyRoom_BeforeNightDoorFix.unity`; the existing room/garden is not rebuilt.

The installer checks shader import, fog samples, door references and CPU-only collision/raycast math, preserves five seat spots and weather links, and writes `Exterior/NightDoorCheck.txt`. It does not start Play mode, a microphone session, a bake or a GPU preview. Saved-state/compile checks do not verify actual appearance or frame rate.

## Short manual check

When the laptop has enough headroom, use **V → Sky & weather → Night**. View the pond from outside and from the room window: no orange wash should appear on entering the room, and no repeating crossed brightness grid should appear on the water. Check the same pond in daylight. Use E on the door from both sides, approach the closed leaf to confirm it blocks walking, reopen it and walk through. Stand in its swing arc and verify it asks you to step clear. Seating, weather controls and voice consent should work as before.

## Verification — 26 September 2026

Runtime/editor C# compilation passed (only the existing unrelated deprecated lightmapper warning). Unity imported and saved the update at 17:49:59. Pond shader import and all installer fog/door checks passed, including raycasts from both sides of the closed door and the open doorway clearance. All 16 read-only saved-scene audit checks passed: original transforms, colliders, renderers, lights, baked settings and unrelated scene data are unchanged; all seven earlier interactions plus the new door interaction are present. Live code matches the compiled delivery copy.

The nighttime screenshots identify the symptoms, not a rendered regression test. No automated Play session or GPU preview was used, so actual nighttime appearance and moving-door interaction are still pending the short manual check above.
