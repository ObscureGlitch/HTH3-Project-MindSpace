# Swimming koi and pond bed

Prepared from the user's `D:/Downloads/koi-pond.glb`. The OBJ/MTL originals are unchanged. No third-party fish or textures were substituted.

## Install

With TherapyRoom open and Play mode stopped, choose **Therapy Game > Install Koi Pond**. The installer checks the existing pond and terrain, backs up the open scene outside Assets, creates the mesh library and bed, then saves TherapyRoom. Running it again validates the existing installation without duplicating fish or decoration.

Read `Assets/TherapyGame/Documentation/KoiPondCheck.txt` for the installation result. **Therapy Game > Validate Koi Pond** repeats scene checks. Runtime appearance and frame rate must still be checked in Play mode.

For an existing installation, choose **Therapy Game > Update Koi School** with Play mode stopped. This backs up the current scene, checks the nine paired routes, changes only the controller's saved fish count to eighteen, and saves. It reuses the existing library and decorations.

## Behavior

Eighteen koi are selected in shuffled bags from eight supplied varieties each play session: Kohaku, Sanke, Showa, Tancho, Ogon, Asagi, Benigoi and Kigoi. Every variety appears twice, with two randomly selected varieties appearing a third time. Sizes vary subtly between 90% and 108% of the supplied model. Shuffled size bands guarantee a mixture of slightly smaller and larger fish, independently of their color and swimming route. Each fish keeps its size and markings throughout the session. Each route has a random heading, starting phase and gliding/rest schedule. Paired fish start half a lap apart and share that route's speed schedule so they do not catch up to one another. Tail phases differ between partners.

Fish remain on nine separated underwater routes inside the authored pond, with two fish per route. The installer checks the complete body/fin envelope against the actual surface triangles and terrain collider at the maximum fish size. The nearest paired-fish center spacing is over 1.8 metres. No new colliders or physics bodies obstruct the pond or its shallow entry. Small submerged stone clusters, sand mounds and short aquatic plants are combined into one static mesh.

The existing water gains an optional `_BedVisibility` parameter. Its default of zero preserves the prior shader appearance. The koi controller applies 0.85 while enabled and restores the previous override when disabled. Ripples, rain impacts, sky reflections and local scene reflections are retained. Underwater renderers are excluded by the existing reflection capture's height filter.

## Budget and checks

The provided eight-fish model had 1,269,536 triangles. Optimized derivatives retain the original vertex colors, color-boundary geometry and separate tail/pectoral parts, totaling 66,936 triangles across all eight templates. Eighteen visible fish have a conservative cap of 166,032 triangles and 72 renderers. Meshes and materials are shared, including repeated varieties. All fish and bed parts share one existing Garden Life material; no new shader family, texture maps, mesh collider, reflection camera or per-frame mesh updates. The school expansion does not change any meshes, shaders, textures or pond decorations. Actual frame rate must be assessed in Play mode.

Raw preparation JSON is under Editor and excluded from player builds. No runtime GLB loading, external model package, npm dependency or network access is added to the game. Offline mesh preparation uses meshoptimizer only.

Offline checks: current runtime/editor C# compile; 14,580 source-terrain route-envelope samples; 128 random seeds × nine paired routes × 400 seconds for deterministic motion, pair separation, bounded speeds, rest/glide transitions, pause and long-frame handling. Population tests cover 4,096 seeds for every count from one to eighteen, checking safe model indices, balanced color selection, pair setup, and subtle size variation. The in-editor installer repeats geometry checks against the loaded scene before saving.

## Tuning and recovery

Hierarchy: `TherapyRoom/OutdoorGarden/Koi fish and pond bed`. The controller exposes fish count (1–18) and bed visibility. Disable this whole group to hide both fish and decorations. Do not increase route radius or scale without revalidating clearance.

Scene backups are placed in the project's `TherapyBackups/KoiPond/<timestamp>` folder. The deployment backup also holds the previous Quiet Pond shader. Existing scene, water simulation, terrain, safety fences, sky and consent settings are not rebuilt.
