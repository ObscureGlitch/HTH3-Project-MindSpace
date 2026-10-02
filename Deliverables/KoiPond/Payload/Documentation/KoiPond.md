# Swimming koi and pond bed

Prepared from the user's `D:/Downloads/koi-pond.glb`. The OBJ/MTL originals are unchanged. No third-party fish or textures were substituted.

## Install

With TherapyRoom open and Play mode stopped, choose **Therapy Game > Install Koi Pond**. The installer checks the existing pond and terrain, backs up the open scene outside Assets, creates the mesh library and bed, then saves TherapyRoom. Running it again validates the existing installation without duplicating fish or decoration.

Read `Assets/TherapyGame/Documentation/KoiPondCheck.txt` for the installation result. **Therapy Game > Validate Koi Pond** repeats scene checks. Runtime appearance and frame rate must still be checked in Play mode.

## Behavior

Six koi are selected without replacement from eight supplied varieties each play session: Kohaku, Sanke, Showa, Tancho, Ogon, Asagi, Benigoi and Kigoi. Markings stay fixed for each fish. Small size variations, randomized headings and phases, gentle tail/fins, and independent gliding/rest intervals keep the group varied without sudden respawning or recoloring.

Fish remain on six separated underwater routes inside the authored pond. The installer checks the complete body/fin envelope against the actual surface triangles and terrain collider. No new colliders or physics bodies obstruct the pond or its shallow entry. Small submerged stone clusters, sand mounds and short aquatic plants are combined into one static mesh.

The existing water gains an optional `_BedVisibility` parameter. Its default of zero preserves the prior shader appearance. The koi controller applies 0.85 while enabled and restores the previous override when disabled. Ripples, rain impacts, sky reflections and local scene reflections are retained. Underwater renderers are excluded by the existing reflection capture's height filter.

## Budget and checks

The provided eight-fish model had 1,269,536 triangles. Optimized derivatives retain the original vertex colors, color-boundary geometry and separate tail/pectoral parts, totaling 66,936 triangles across all eight templates. Six visible fish use at most 55,344 triangles and 24 renderers. All fish and bed parts share one existing Garden Life material; no new shader family, texture maps, mesh collider, reflection camera or per-frame mesh updates.

Raw preparation JSON is under Editor and excluded from player builds. No runtime GLB loading, external model package, npm dependency or network access is added to the game. Offline mesh preparation uses meshoptimizer only.

Offline checks: current runtime/editor C# compile; 9,720 source-terrain route-envelope samples; 128 random seeds × six lanes × 400 seconds for deterministic motion, bounded speeds, rest/glide transitions, pause and long-frame handling. The in-editor installer repeats geometry checks against the loaded scene before saving.

## Tuning and recovery

Hierarchy: `TherapyRoom/OutdoorGarden/Koi fish and pond bed`. The controller exposes fish count (1–6) and bed visibility. Disable this whole group to hide both fish and decorations. Do not increase route radius or scale without revalidating clearance.

Scene backups are placed in the project's `TherapyBackups/KoiPond/<timestamp>` folder. The deployment backup also holds the previous Quiet Pond shader. Existing scene, water simulation, terrain, safety fences, sky and consent settings are not rebuilt.
