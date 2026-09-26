# Warm realism pass

The therapy room has been upgraded in place without changing its floor plan, interaction targets or movement code. This is a more detailed real-time treatment of the existing room, not a replacement with photogrammetry furniture.

## What changed

- Major surfaces use 2K-resolution color, normal and roughness-derived smoothness maps. Fine linen and plaster keep authored solid colors and use scanned normal/roughness detail. The sofa blanket uses scaled knit maps with material-level albedo compensation so its stitches remain visible. Texture import uses mipmaps, high-quality compression and 8x anisotropic filtering. Round wooden surfaces have planar cap UVs to avoid radial grain stretching.
- Upholstered box-based meshes have denser, rounded edge sampling. Their new lightmap UVs are regenerated before the lighting bake. Colliders retain the original comfortable walking routes.
- The room's own URP asset uses 4x MSAA, 115% render scale, a 4096-pixel main shadow atlas, four cascades and high-quality full-resolution contact shadows. These cost more GPU time than the original draft; frame rate depends on hardware and display resolution.
- Color grading uses ACES, modest positive contrast and saturation, and restrained warmth. The main sunlight and lamps are more amber/orange, balanced by a cooler window fill and a broad baked ceiling bounce. Bloom stays low, with no motion blur or depth of field.
- Baked lighting is configured for 24 texels/metre (previously 8), a 2048 atlas cap, 128 indirect samples and three bounces. `Realism/UpgradeReport.txt` records the actual bake result.
- The first-person camera has `WellnessAtmosphere`: a subtle warm distance haze, density 0.032. It applies only while that camera renders and restores the prior fog settings afterward. This is simple distance fog, not expensive volumetric light scattering.

## Adjustments

Select `TherapyRoom/PlayerSpawn/First person camera` to change `WellnessAtmosphere > Density`, `Haze Color`, or disable `Haze Enabled`. Use roughly 0.02–0.04 for a restrained indoor effect.

The visual assets are under `Assets/TherapyGame/Realism`. Lower `Settings/RealismURP > Render Scale` from 1.15 to 1.0 first if performance is poor. `Settings/RealismVolume` controls warmth, contrast, saturation and bloom.

The saved original room state is `Realism/Backups/TherapyRoom_BeforeRealism.unity`. A separate pre-upgrade archive of the original assets is at `D:/Hack the Hill/Deliverables/TherapyBackups/BeforeRealism.zip`. The scene snapshot includes edits that were present in Unity when the pass began.

Material provenance and license links are in `RealismMaterials.md`. Audio remains placeholder-only. The existing models are still lightweight, purpose-built geometry; the upgrade does not claim photorealism or a measured frame rate.

## Verification

The completed bake produced two lightmaps. Validation found zero missing scripts/materials and all seven interaction targets. The scene was also visually checked in the open Unity Editor's Play mode. Matched 2560x1440 renders with and without haze measured an average channel difference of 1.821/255; fog settings were restored after camera rendering. Preview PNGs are exported in display-referred sRGB.

No standalone build or frame-rate benchmark was run. Before shipping, check runtime fog shader-variant stripping and test the haze in the standalone build as well as in the Editor.
