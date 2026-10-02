# Interactive pond water

The existing rain controller installs `WellnessPondWater` on the scene's Quiet Pond renderer when Play begins. No scene replacement is needed. The changes are also installed in `D:/Unity/HTH3 Project/Assets/TherapyGame`.

- A 128 × 128 height field solves a damped wave equation at 60 fixed steps per real second. Rain, wading, and moving rigidbodies displace the surface. Waves propagate, overlap, reflect off the authored shoreline, and decay. Balanced impact impulses conserve the mean water level; exceptional impacts are bounded without local height clipping.
- The original pond outline is retained and tessellated at runtime. Heights move actual vertices, and the same field supplies shading normals. Small wind waves add continuous movement.
- Rain raycasts choose the first obstruction. Drops above water terminate at the surface and queue their ripple for the landing time, with three small rebound droplets. Roofs and bridges block impacts. Rain intensity and the existing weather controls determine the number of drops; stopping rain lets existing waves settle.
- A 512 × 512 planar scene reflection updates up to 20 times per second while the pond is in view. It reflects the landscape and sky, with surface-normal distortion, angle-dependent water reflectance, and sun highlights. The existing procedural sky/cloud/night reflection remains the fallback if render requests are unsupported.
- Dynamic rigidbodies touching the pond receive approximate displaced-volume buoyancy and linear/angular drag. This uses the first collider's world bounds per body, with bounded acceleration. It is suitable for small props, not a precision boat or compound-hull model. The existing player remains a walking/wading controller and generates wakes.

This is a bounded real-time surface simulation, not a full 3D fluid solver: no flooding, breaking waves, volumetric currents, physically accurate refraction, or accumulation of rain volume. The pond stays at its authored level. Rendering and simulation follow the existing rain system's unscaled clock.

`WellnessPondWater.sceneReflections` can be disabled for lower GPU cost; `reflectionStrength` controls the sheen. Resources, original mesh, reflection camera, and splash particles are cleaned up on disable. Saved assets and the original mesh are never modified by the runtime effect.

Use **Therapy Game → Validate Pond Water** with the therapy scene open to compile the shader, run stability/propagation/volume checks, and render an isolated preview. The preview does not enter Play, start voice, or save the scene. Results are written to `Documentation/PondWaterCheck.txt` and the preview to `D:/Hack the Hill/Deliverables/PondWaterValidation/PondPreview.png`.

For the interactive check, enter Play, approach/wade into the pond, and select Rain under weather controls. Check expanding rings at landing points, settling after switching to Clear, bridge occlusion, reflected scenery while turning the camera, and frame time with scene reflections on/off. Full gameplay and frame-rate measurements still need this check.
