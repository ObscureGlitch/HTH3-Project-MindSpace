# Garden refinements — 2026-09-26

Imported and saved in the actual TherapyRoom scene at 15:18 local time.

## Changes

- **Pond:** one continuous translucent water surface, moving fine ripples, soft sky sheen and restrained sun highlights. A shallow sandy floor is at most 0.48 m below the surface. Invisible shoreline barriers were removed, with open banks for wading. A small, static green frog sits on a lily pad near the south bank.
- **Trail stones:** 64 low, embedded stones with seeded random spacing, lateral offsets, rotation and size. They no longer form a regularly spaced centre line or stand on edge.
- **House:** a real 1.8 m × 1.23 m window opening beside the door, through both the cedar siding and inner plaster wall, with a wooden frame, sill, centre mullion and clear glass. The previous solid entry-wall object is retained but disabled.
- **Butterflies:** 13 small butterflies, roughly 7–9 cm in nominal wingspan, with modest size variation and pale blue, muted gold and lilac wings. Wingbeats vary from 7.2 to 8.28 Hz. Their heads now follow the actual ellipse tangent instead of facing backward.

## Verified without Play mode

The CPU geometry tests and Unity edit-mode physics checks passed. Unity sampled the pond floor, checked capsule clearance along the south bank using the player's dimensions, and checked all butterfly headings at multiple times. No missing scripts or materials were reported. All seven interaction targets and five indoor sitting spots are preserved. Unrelated trees, mountain geometry and flower petals retain their original placements.

The saved-scene audit confirmed all 57 current native mesh references, the water shader assignment, the new window, the removed pond barriers, and the butterfly size/flap settings. The current exterior has 53 static batches and 76,673 triangles, compared with 77,317 before these refinements. It uses 150 box colliders and two ground/bridge mesh colliders.

No extra reflection camera, texture download, screen-space reflection, lighting bake or Play-mode test was added or started. The 30 FPS cap and existing low-load URP profile remain. The Three.js browser-render verifier was not rerun after the earlier graphics-device resets. The updated appearance and live player movement still need a visual/Play-mode check once the laptop is stable.

## Recovery

- `Exterior/Backups/TherapyRoom_BeforeGardenRefinements.unity` preserves the pre-refinement scene layout.
- `Deliverables/TherapyBackups/GardenBeforeRefinements-20260926.zip` also preserves the earlier scene, model export, installer, butterfly script, and Three.js authoring files. This is needed to restore earlier mesh contents, because shared native mesh assets are updated in place.
- Superseded mesh/material assets were retained rather than deleted. The single-use refinement request has been consumed, so normal future refreshes do not rebuild the scene.

Authoring checks: `node audit.mjs`, `node audit-refinements.mjs`, and `node audit-unity-import.cjs`. These do not launch a renderer. The custom water shader uses the installed URP lighting/shadow API; reference: [Unity custom shader shadows](https://docs.unity.com/en-us/engine/6000.5/manual/materials-and-shaders/shaders/writing-custom-shaders-urp/use-built-in-shader-methods/shadows).
