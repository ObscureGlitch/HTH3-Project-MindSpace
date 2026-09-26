# Quiet Garden

Low-poly exterior authored in Three.js 0.185.1 for the existing Unity URP therapy room. One unit is one metre. Deterministic seed: 260926.

The model contains meadow terrain, a pond with lily pads and reeds, a shallow-arched bridge with handrails, a wide loop path, pines and broadleaf trees, faceted mountains, mixed flower beds, a cabin exterior and 13 animated butterflies.

## Rebuild

1. Run `node export.mjs` here to regenerate `generated/GardenModel.json`.
2. Copy that data to `Assets/TherapyGame/Exterior/Source/GardenModel.json` in Unity.
3. In the existing TherapyRoom scene, choose **Therapy Game → Install Outdoor Garden**. The installer preserves the indoor furniture, backs up the scene and disables the old window backdrop without deleting it.
4. Only when the laptop is stable and has sufficient memory available, **Therapy Game → Verify Garden Walk in Play Mode** can test the route with the actual player's movement input and colliders. This test has not been run after the garden import; do not start it immediately after a graphics-device reset.

The JSON is an authoring interchange file, not a runtime parser. Unity converts it into native saved mesh and material assets, so the game does not need JavaScript or an extra package.

## Performance and verification

The refined static model uses 53 material/category batches and 76,673 triangles, no new texture downloads, no lighting bake and no realtime reflection camera. The butterflies share one wing mesh and one body/head mesh. Terrain, bridge, tree trunks, meadow boulders and the garden boundary have collision. The old invisible pond-edge barriers have been removed for shallow wading.

Use the supplied threejs skill's `serve.mjs` and `verify_scene.mjs` against `preview/?webgl&steps=120`. The WebGL2 fallback passed the initial three-angle check. This laptop's WebGPU preview reported a lost device, so WebGL is the default preview backend. That browser issue does not change Unity's rendering backend.

The original room is also preserved in `Deliverables/TherapyBackups/BeforeGarden.zip`; the in-project snapshot lives in `Assets/TherapyGame/Exterior/Backups` after installation. No unrelated project files are modified.

The garden was imported into the actual Unity project and the scene saved on 2026-09-26. A CPU-only saved-scene audit passed: all native mesh/material references resolve, terrain and bridge colliders exist, the doorway is open, all 13 butterflies are configured, and the five indoor seat spots are unchanged. Run `node audit-unity-import.cjs` to repeat these file checks without starting Unity or rendering. An optional first argument selects a different `Assets/TherapyGame` folder.

The scene uses a separate low-load URP profile: 85% render scale, 2× MSAA, 1024px main shadows, two cascades, Forward rendering, and screen-space AO disabled. Play mode is capped at 30 FPS. The original realism profiles are preserved unchanged. No Unity preview render, lighting bake, or Play-mode test was started during final import verification. Live route verification remains pending.

## Refinement revision (export version 2)

Imported into Unity on 2026-09-26 at 15:18 local time. The water is a single surface with a lightweight Unity ripple shader and a 48 cm maximum depth; an unobtrusive frog rests on a lily pad. The cabin has a true front-window opening, the path has 64 irregularly scattered low stones, and butterflies have roughly 7–9 cm wingspans, three pastel colours, faster wingbeats and corrected forward-facing flight. The browser authoring preview uses an approximate standard transparent water material; the animated water shader is Unity-specific.

`node audit-refinements.mjs` checks geometry, depth, bank slope, siding aperture and butterfly direction entirely on the CPU. An optional path to the previous `GardenModel.json` additionally checks preservation of unrelated scenery. Unity's one-time installer also passed edit-mode pond-depth, bank-clearance and butterfly-direction tests. `node audit-unity-import.cjs` checks the saved Unity references and settings. The browser-render verification screenshots predate this refinement and are not validation of its new appearance; no new GPU verification was attempted after the device resets.
