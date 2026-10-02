# Swimming koi and pond bed

Prepared from the user's `D:/Downloads/koi-pond.glb`. The OBJ/MTL originals are unchanged. No third-party fish or textures were substituted.

## Install

With TherapyRoom open and Play mode stopped, choose **Therapy Game > Install Koi Pond**. The installer checks the existing pond and terrain, backs up the open scene outside Assets, creates the mesh library and bed, then saves TherapyRoom. Running it again validates the existing installation without duplicating fish or decoration.

Read `Assets/TherapyGame/Documentation/KoiPondCheck.txt` for the installation result. **Therapy Game > Validate Koi Pond** repeats scene checks. Runtime appearance and frame rate must still be checked in Play mode.

For an existing installation, choose **Therapy Game > Randomize Koi School** with Play mode stopped. This backs up the current scene, checks the nine shared routes, enables the randomized population and saves. It reuses the existing library and decorations. The separate **Therapy Game > Install Natural Pond Floor** command applies the prepared 88-piece decoration redesign if it has not yet been installed, preserving the population settings.

## Behavior

A fresh count of **15 to 30 koi, inclusive**, is chosen once when the pond starts (the controller enables in Play mode). All sixteen counts are equally eligible; consecutive sessions may happen to choose the same count. The count stays fixed while playing and does not reroll on pause/resume. No timer spawns or removes fish in view.

Fish are selected in shuffled bags from eight supplied varieties: Kohaku, Sanke, Showa, Tancho, Ogon, Asagi, Benigoi and Kigoi. Variety counts differ by at most one. Sizes vary subtly between 90% and 108% of the supplied model. Shuffled size bands guarantee a mixture of slightly smaller and larger fish, independently of their color and swimming route. Each fish keeps its size and markings throughout the session. Each route has a random heading, starting phase and gliding/rest schedule. Members are evenly spaced around a shared route clock so they do not catch one another. Tail phases differ between members.

Fish remain on nine separated underwater routes inside the authored pond, with a balanced distribution of up to four fish per route. Which routes receive extra members is shuffled. The installer checks the complete body/fin envelope against the actual surface triangles and terrain collider at the maximum fish size. The nearest tested same-route center spacing is over 1.28 metres, including four-fish groups on the innermost route. No new colliders or physics bodies obstruct the pond or its shallow entry. Pond decorations are combined into one static mesh.

The existing water gains an optional `_BedVisibility` parameter. Its default of zero preserves the prior shader appearance. The koi controller applies 0.85 while enabled and restores the previous override when disabled. Ripples, rain impacts, sky reflections and local scene reflections are retained. Underwater renderers are excluded by the existing reflection capture's height filter.

## Budget and checks

The provided eight-fish model had 1,269,536 triangles. Optimized derivatives retain the original vertex colors, color-boundary geometry and separate tail/pectoral parts, totaling 66,936 triangles across all eight templates. Thirty visible fish have a conservative cap of 276,720 triangles and 120 renderers. Meshes and materials are shared, including repeated varieties. All fish and bed parts share one existing Garden Life material; no new shader family, texture maps, mesh collider, reflection camera or per-frame mesh updates. Population randomization does not change any meshes, shaders, textures or pond decorations. Actual frame rate must be assessed in Play mode.

Raw preparation JSON is under Editor and excluded from player builds. No runtime GLB loading, external model package, npm dependency or network access is added to the game. Offline mesh preparation uses meshoptimizer only.

Offline checks: current runtime/editor C# compile; 14,580 source-terrain route-envelope samples; 32 seeds on nine routes with groups of two, three and four fish for 400 seconds, checking deterministic motion, separation, bounded speeds, rest/glide transitions, pause and long-frame handling. Population tests cover 65,536 count rolls (all counts 15 through 30 reached), and 4,096 seeds for every supported fixed count from one to thirty, checking safe model indices, balanced colors and route occupancy, and subtle size variation. The in-editor installer repeats geometry checks against the loaded scene before saving. Rendering and Play-mode behavior still require in-editor confirmation.

## Tuning and recovery

Hierarchy: `TherapyRoom/OutdoorGarden/Koi fish and pond bed`. **Randomize Count** enables the 15-30 range. **Fish Count** is the legacy fixed-count fallback (1-30), ignored while randomization is on. `ActiveFishCount` reports the current runtime school. Bed visibility is unchanged. Disable the whole group to hide fish and decorations; re-enabling it during play creates a fresh school. Do not increase route radius or scale without revalidating clearance.

Scene backups are placed in the project's `TherapyBackups/KoiPond/<timestamp>` folder. The deployment backup also holds the previous Quiet Pond shader. Existing scene, water simulation, terrain, safety fences, sky and consent settings are not rebuilt.
