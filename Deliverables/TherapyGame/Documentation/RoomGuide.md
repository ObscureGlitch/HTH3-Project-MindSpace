# A quiet place — therapy room

## Explore

In the destination project, use **Therapy Game > Open Therapy Room**, or open `Assets/TherapyGame/Scenes/TherapyRoom.unity`, then press Play. The original authoring copy is at `Assets/_Project/Scenes/TherapyRoom.unity`. The standalone scene owns its input adapter and needs only URP 17.6 and Input System 1.20.

| Control | Action |
| --- | --- |
| WASD | Walk at a relaxed pace |
| Mouse | Look around; no head bob or camera shake |
| E | Notice the object under the reticle |
| Esc | Release / recapture the cursor |
| Enter | Resume after releasing the cursor |

The player starts just inside the entrance at an eye height of 1.65 m. Furniture, walls, window, and interactive objects have simple colliders. The entrance door is decorative and closed; this is a single-room environment.

## Scene and assets

The scene follows the requested `TherapyRoom` hierarchy: Architecture, Furniture, Decorations, Plants, InteractiveObjects, Lighting, Audio, and PlayerSpawn. Dimensions are in metres, with +Z toward the sage wall and the window on +X.

- `Assets/TherapyGame/Prefabs`: reusable upholstered seating, pillows, plants, lamps, cup, candle, and ottoman.
- `Assets/TherapyGame/Models`: reusable rounded meshes, planter profiles, leaves, and cloth.
- `Assets/TherapyGame/Materials`: URP Lit materials and subtle procedural oak, linen, plaster, and rug textures.
- `Assets/TherapyGame/Settings`: URP renderer, pipeline, volume profile, and baked-lighting settings.
- `Assets/TherapyGame/Runtime`: standalone exploration and interaction components.
- `Assets/TherapyGame/Textures/QuietLake.png`: generated window landscape. See `BackdropProvenance.md` for the complete prompt and method.

The imported room lives entirely inside `Assets/TherapyGame`. Its menu opens the room only when existing scenes have no unsaved edits. In Play mode the room temporarily selects its own URP asset, restoring the host pipeline on exit. It does not replace the host project's saved pipeline, package manifest, existing scenes, or build settings. To make a standalone build, add TherapyRoom to a Unity Build Profile and place it first.

## Three interaction areas

**GroundingShelf** has individually collidable stone, plant, steady LED candle, and journal targets. Each displays a short observation prompt. Shelf boards and uprights have their own colliders so they do not block access to the objects.

**BreathingOrb** expands and contracts gently over ten seconds without flickering or changing light intensity. E pauses or resumes the motion. `WellnessBreathingOrb` exposes cycle duration and scale expansion in the Inspector.

**ReflectionSeat** is beside the window, with a lamp and closed journal. The chair and journal provide reflection prompts; sitting animations and journal text entry are future features.

Each target has a `WellnessInteraction` component with an area, display name, reflection text, and an Inspector `onInteract` UnityEvent for later integration.

## Lighting and performance

The room uses Forward+, 4× MSAA, a single real-time shadow-casting daylight source, soft ambient occlusion, and shadow-free practical/fill lights. Warm practical colors are balanced by neutral window light. The volume has slight warm white balance, mild desaturation, neutral tone mapping, and very low bloom. There is no motion blur, depth of field, vignette, flashing, or camera shake.

The completed bake contains one lightmap and 45 light probes. Lighting settings use Progressive CPU, 8 texels/metre, a 512 atlas limit, and two bounces. To rebake after moving furniture, use Unity's Lighting window > Generate Lighting. Geometry is marked for static batching and interior GI. A restrained custom cubemap supplies room reflections because the local Unity reflection baker crashed; diffuse GI baked successfully.

The exterior uses one softly painted lake backdrop beyond a real window opening, while gameplay stays in focus. Optional simple scenery is retained as inactive objects outside the window. The shelf diffuser uses at most sixteen faint particles.

## Audio

Seven supplied background tracks load automatically from `Resources/TheLastWatch/BackgroundMusic`. The persistent `BackgroundMusicPlayer` shuffles them without replacement, so every track plays exactly once before the playlist reshuffles. A new cycle is adjusted when necessary so its first track never matches the previous cycle's last track. Music is streamed, non-spatial, and mixed at 10% volume. Separate streamed daytime-nature and nighttime ambience loops follow the sky cycle: they crossfade through dawn and dusk, stay silent indoors, fade in as the player crosses outside the room, and fade out again on re-entry. The scene Audio children remain available for other room-tone and weather layers.

## Verification outputs

`Validation.txt` records scene checks and the geometry budget. Preview images are rendered directly by Unity and are stored alongside this guide when rendering completes. The overhead preview temporarily hides the ceiling and front/left walls for inspection; the saved playable scene remains enclosed.
