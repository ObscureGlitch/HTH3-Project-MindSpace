# Explore the garden

Press Play, walk through the open oak door, and follow the flower-lined path. The path loops around a small pond; the wooden bridge crosses the water. You can return through the same door. WASD and mouse controls are unchanged, and the five indoor sitting spots remain available.

The garden includes real low-poly terrain, trees, rocks, a pond, reeds, lily pads, flowers, a small lookout, distant mountains and 13 gently flying butterflies. It is a bounded garden, not an open-world map: the low fence marks its edge and rocks protect the waterline.

The refined pond is now open for shallow wading (maximum 48 cm deep); use the clear bank openings, especially the tested south-bank entrance. A small frog rests on a lily pad. The front of the cabin now has a real window beside the door, and trail stones are scattered irregularly across the path. Butterflies are much smaller, vary subtly in size and pastel colour, flap faster, and face their direction of travel. See `GardenRefinements.md` for the verification results.

The new geometry uses shared materials and mesh batches. No heavy textures, reflection cameras or rebaking were added. Warm indoor haze transitions to a lighter outdoor haze as you leave the room. A separate low-load URP profile uses 85% render scale, 2× MSAA, 1024px main shadows, two cascades and no screen-space AO. Play mode is capped at 30 FPS; the cap and host frame-pacing settings are restored on exit. The original realism assets remain unchanged.

The old painted window backdrop is disabled but retained. The installer preserves a scene snapshot in `Exterior/Backups` before making changes.

Installation never starts previews or Play-mode tests. After the laptop is stable, **Therapy Game → Verify Garden Walk in Play Mode** tests the doorway / garden loop / bridge / return route. It has not yet been run. Avoid rendering previews after a graphics-device reset. The latest installation or test result is recorded in `Exterior/GardenCheck.txt`.
