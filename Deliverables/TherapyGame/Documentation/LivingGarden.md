# Living garden additions

An additive layer under **TherapyRoom → OutdoorGarden → Living garden**. The existing garden, trees, room, door, pond, fog fix, clouds, weather, voice consent and seats are not rebuilt. Scale: one unit = one metre.

## Included

- **165 extra flowers:** daisies, bluebells, lavender, pink poppies and golden clover in irregular drifts with gaps, rather than straight rows.
- **12 trees:** pale-trunk birches, rowan-like berry trees, softly flowering trees and young conifers. Trunks have small solid colliders; foliage does not obstruct walking.
- **24 ferns, 12 mushroom clusters and three mossy fallen logs.** These are small scenery, not interaction obstacles.
- **Three rabbits:** the supplied detailed bunny model replaces the original procedural version. Its native transform rig plays Idle, Graze, Alert, Groom, Hop, Run and Binky clips while following the existing gentle routes.
- **Three squirrels:** the supplied detailed squirrel model replaces the original procedural version. Its native rig plays the supplied Idle, Nibble, LookAround, Alert, Hop, DropAndRun and Run clips while following the garden's gentle scampering routes.
- **Five songbirds:** small blue/brown bodies with warm chests; folded resting wings and occasional short flapping flights.

Wildlife remains close to quiet clearings, turns in its direction of travel, and reduces activity at night or during rain. Ground animals pause near the viewer and look toward them. Wildlife is decorative and does not block walking or support petting. No added sounds, sudden effects, combat, feeding prompts or camera motion.

## Laptop limits

The supplied animated GLBs are converted offline into native Unity assets, so the game does not need a runtime glTF package. Each character is consolidated into **one skinned renderer**: every squirrel preserves 107,104 source triangles and eight shared materials, while every bunny preserves 86,528 source triangles and six shared materials. The fully upgraded layer has **11 animals**, **33 renderers**, and 609,000 total added triangles when all wildlife is nearby. The squirrels account for 321,312 triangles and the bunnies for 259,584; both cast no shadows and use no reflection/light probes. These are geometry counts, not a measured frame-rate guarantee. No additional lights, cameras, reflection captures, textures, particles, NavMesh or physics simulation are added.

Animal animation/rendering is disabled beyond 38 metres and resumes without fast-forwarding. The bunnies and squirrels use GPU skinning and shared native clips; there are no runtime mesh rebuilds, runtime terrain raycasts or allocation-heavy animation loops. Existing 30 FPS pacing, URP render scale and shadow-map limits remain unchanged.

## Placement and verification

The authored routes sample the existing terrain triangle heights, rather than approximating the original noise surface. Placement reserves the main curved path, doorway/porch, lookout spur, bridge, shallow pond entrances, cabin and existing boulders/tree trunks. The source CPU audit measured a minimum animal-route distance of 1.53 m from path centre lines before accounting for the animals' small bodies. It checks export shapes, mesh/color data, closed routes, finite geometry and the 33,000-triangle ceiling.

Unity's one-time installer separately checks actual terrain raycasts, solid-obstacle clearance, pose bounds, references and geometry counts. It backs up the current open scene, including unsaved edits, at `Exterior/LivingGarden/Backups/TherapyRoom_BeforeGardenLife.unity`. Importing never starts Play mode, a microphone, a lighting bake or a GPU preview.

The Three.js skill's browser-rendered verification is intentionally not run because of the earlier GPU crash and the request to keep the laptop safe. CPU checks do not establish visual quality or real-time performance. A brief user-controlled walkthrough remains necessary when sufficient headroom is available.

## Find and adjust

Start near the meadow just past the doorway for the first rabbit; other wildlife is distributed near the outer clearings and flower patches. Birds spend more time resting than flying, so give them roughly 10–20 seconds. Use **V → Sky & weather** to compare daylight with night/rain. The `WellnessGardenLife` component on Living garden exposes the 38 m wildlife visibility distance. Disable that component to stop animation; disable the Living garden object to hide just these additions.

Authoring: `Deliverables/OutdoorGarden/life.mjs`, `export-life.mjs`, `export-squirrel.mjs` and `export-bunny.mjs`. Unity source exports: `Assets/TherapyGame/Exterior/LivingGarden/Source/GardenLife.json`, `Squirrel.json` and `Bunny.json`; both untouched supplied animated GLBs are retained beside them for provenance. Use **Therapy Game → Upgrade Animated Squirrels** or **Therapy Game → Upgrade Animated Bunnies** while Play mode is stopped to rerun the applicable one-shot upgrade manually.

## Completed checks — 26 September 2026

Runtime/editor compilation passed (only an existing unrelated deprecated lightmapper warning). Unity imported and saved the additive scene at **18:09:58**. Shader import, actual-terrain raycasts, collision clearance and bounded animation checks passed. All **15 saved-scene audit checks passed**, including exact preservation of all previous scene records except the new child link on OutdoorGarden, unchanged low-load URP settings, one new controller, four materials, 22 mesh assets and the expected wildlife counts. The imported model export matches the CPU-checked source hash.

No rendered preview or live performance test was run. Give the birds time to leave their resting poses during a short manual check, and stop Play mode if the laptop becomes unstable.

### Animated squirrel upgrade — 26 September 2026

The supplied `tiny-squirrel-animated (1).glb` replaced both original procedural squirrels and a third squirrel was added in a separate outer clearing. The source SHA-256 is `0926ff96726beb51ec5beb68f6028d0bf5259a06ff330784c7bb7b998807892e`. Unity rebuilt both runtime/editor assemblies and saved the upgraded scene at **21:09:05** after preserving the open scene in `Backups/TherapyRoom_BeforeAnimatedSquirrels.unity`.

The post-import audit passed all 18 checks: three squirrels, one skinned renderer per squirrel, 24 rig/animation bones, eight shared materials, seven clips, 57,802 vertices and 107,104 source triangles per squirrel, closed terrain-grounded routes, no wildlife physics, no added cameras/lights/particles, stable source hashes, and no unexpected unrelated scene-record changes. A concurrent reference-HUD update changed only its own `panelOpacity` value while the upgrade was running and was preserved. Windows UI automation could not start because of the local sandbox ACLs, so rendered appearance and live frame rate remain unverified; use a short manual Play-mode walkthrough for that final subjective check.
