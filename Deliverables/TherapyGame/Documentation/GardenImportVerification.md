# Initial garden import verification — 2026-09-26, before refinements

This is the historical initial-import report. See `GardenRefinements.md` for the later water, window, trail-stone and butterfly changes and current asset counts.

Status: imported and saved in `Scenes/TherapyRoom.unity`. File-based checks passed. Live Unity walking and rendering checks remain pending.

## Confirmed

- The saved scene contains an enabled OutdoorGarden with 50 static mesh batches and 13 animated butterfly objects.
- All 53 native mesh assets are referenced; garden material references resolve to the 36 imported materials.
- Terrain and continuous bridge-deck MeshColliders are present, together with 196 solid BoxColliders.
- The importer successfully checked an upward-facing terrain surface in Unity physics.
- The oak door hinge is open 105 degrees. The previous painted window backdrop is disabled, not deleted.
- All five indoor sitting spots on four pieces of furniture are unchanged from the pre-garden scene snapshot. All seven interaction targets remain.
- Fall recovery, outdoor haze transition, and a Play-mode frame-rate cap are attached.
- The scene references the separate low-load URP profile: 85% render scale, 2× MSAA, 1024px main shadows, 512px additional-light shadows, two cascades, 30m shadow distance, Forward rendering, and screen-space AO disabled.
- The original RealismURP and RealismRenderer assets match the pre-garden ZIP backup by SHA-256.
- The imported garden source matches the Three.js-authored export by SHA-256.
- The one-use import request has been consumed, so another asset refresh will not rebuild the garden automatically.
- The Unity import log records success, and the checked log contains no C# compiler-error entries.

## Deliberately not run

No Unity preview rendering, lighting bake, or Play mode was started during final verification after the earlier graphics-device reset. Saved collider presence is not proof that the complete walking route is unobstructed. Live doorway exit, loop walking, bridge crossing, return indoors, and animation appearance still need a manual test once the laptop is stable.

The low-load profile reduces rendering work but cannot guarantee that a graphics-driver reset will not recur. The original scene is retained in `Exterior/Backups/TherapyRoom_BeforeGarden.unity` and in the external `BeforeGarden.zip` backup.
