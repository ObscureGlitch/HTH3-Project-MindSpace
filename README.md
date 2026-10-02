# MindSpace

MindSpace is a wellness-focused exploration game set in a calm therapy room and living garden. Players can talk with optional AI companions, interact with the environment, photograph quiet moments, watch the weather and seasons change, and unwind beside an interactive koi pond with a complete fishing and collection loop.

MindSpace is designed as a reflective game experience. It is not a medical device, a replacement for professional care, or a crisis service.

## Current experience

- **Therapy room and garden:** Explore a handcrafted indoor space and a large outdoor sanctuary with interactive seating, doors, plants, wildlife, ambient audio, weather, and a day/night sky.
- **Optional AI companions:** Talk with configured ElevenLabs companions and use the optional Presage coaching integration. Consent, camera, microphone, and biometric features remain explicit and optional.
- **Living weather and seasons:** Switch between Spring, Summer, Fall, and Winter, or let seasons advance automatically. Rain, snow, clouds, stars, northern lights, fireflies, foliage, and seasonal details respond independently.
- **Interactive pond:** Runtime waves react to rain, footsteps, wading, and moving objects. Bounded scene reflections, shoreline-aware ripples, buoyancy for small props, and performance-aware sleep behavior keep the water responsive.
- **Koi ecosystem:** Each session creates a balanced school of 15–30 ambient koi using shared, optimized models and safe underwater routes.
- **Fishing collection:** Cast from the bank or bridge, hook and track fish through distinct movement patterns, build Flow, earn Perfect catches, and collect 24 species across Common through Godly tiers.
- **Rare catch presentation:** Legendary and Godly koi use species-specific effects, including dragonfire, thunder, frost, nebula, lunar, solar, void, and prismatic themes.
- **Photo mode:** Carry a camera, zoom from 1–4×, capture HUD-free images, and browse a persistent local gallery.
- **Performance work:** The latest update reduces reflection work, sleeps settled pond waves, reuses HUD measurements, bounds effect update rates, and keeps expensive previews active only while visible.

## Open the project

1. Clone or download this repository.
2. Open Unity Hub and add the repository folder as a project.
3. Use **Unity 6000.6.3f1** with the Windows Build Support module.
4. Let Package Manager restore the versions recorded in `Packages/manifest.json`.
5. Open `Assets/_Project/Scenes/Bootstrap.unity` and enter Play mode.

The root project targets 64-bit Windows and uses **URP 17.6**. `Bootstrap` is build scene 0 and loads `MainMenu` through the development configuration.

The repository root contains the baseline Unity project. The newest integrated gameplay source is staged under `Deliverables/TherapyGame`, while folders such as `Deliverables/FishingPantheon`, `Deliverables/Seasons`, and `Deliverables/PhotoCamera` contain scoped payloads, installers, checks, backups, and verification artifacts. Review a feature folder's README and checks before applying it to a different Unity project state.

## Core controls

| Action | Control |
| --- | --- |
| Move | WASD |
| Look | Mouse |
| Interact / notice | E |
| Release cursor / close | Esc |
| Resume | Enter |
| Cast fishing line | Left click while facing open water |
| Hook and reel | Space or left click |
| Recast after a round | Enter |
| Open fish collection | I |
| Hold or put away photo camera | C |
| Camera zoom | Mouse wheel |
| Capture photo | Left click or Space while holding the camera |
| Open photo gallery | G |

Weather, seasons, cloud types, and related presentation controls are available through **Settings → Sky & weather**.

## Therapy and wellness room

Open `Assets/_Project/Scenes/TherapyRoom.unity` and press Play to explore the baseline room directly.

The room is approximately 7 × 6 × 3 metres, with an oak floor, sage accent wall, rounded upholstered seating, plants, a grounding shelf, breathing orb, and window reflection seat. All models and materials are generated locally; no paid or downloaded art assets are needed.

See [the room guide](Documentation/Wellness/RoomGuide.md) for asset locations, lighting, interaction hooks, and audio details.

## Latest feature guides

- [Koi Pantheon fishing and collection](Deliverables/FishingPantheon/README.md)
- [Fishing interface, save recovery, and presentation polish](Deliverables/FishingRefinement/README.md)
- [Interactive pond water](Deliverables/TherapyGame/Documentation/PondWater.md)
- [Randomized koi school and pond bed](Deliverables/KoiRandomPopulation/Payload/Documentation/KoiPond.md)
- [Legendary and Godly koi effects](Deliverables/KoiSignatureVfx/README.md)
- [Four seasons and snow](Deliverables/Seasons/README.md)
- [Photo camera and local gallery](Deliverables/PhotoCamera/README.md)
- [Rendering and smoothness changes](Deliverables/TherapyGame/Documentation/Performance.md)
- [Presage biometric coaching](Deliverables/TherapyGame/Documentation/PresageBiometricCoaching.md)

## Required packages

| Package | Version | Purpose |
| --- | --- | --- |
| Input System | 1.20.0 | Project-owned input actions and device bindings |
| Universal Render Pipeline | 17.6.0 | PBR materials, lighting, weather, water, and post-processing |
| Test Framework | 1.8.0 | Edit-mode validation |
| Visual Studio Editor | 2.0.26 | Visual Studio integration |
| JetBrains Rider Editor | 3.0.38 | Rider integration |

The foundational scenes require only Unity modules and URP dependencies. The richer therapy-game deliverable under `Deliverables/TherapyGame` optionally integrates the bundled ElevenLabs Agents SDK and Presage SmartSpectra through a local Node.js 20+ bridge.

## Configuration and privacy

Development and release settings are ScriptableObject assets under `Assets/_Project/Config`. They contain non-sensitive behavior flags only. The bootstrap scene currently references `GameConfiguration.Development.asset`; select the bootstrap object and change this reference when preparing a release build.

Never store credentials in Unity assets, scenes, prefabs, scripts, tracked JSON, or command-line arguments.

- Presage reads `TLW_PRESAGE_API_KEY` from the environment that launches Unity.
- ElevenLabs configuration tools read `ELEVENLABS_API_KEY` or `TLW_ELEVENLABS_API_KEY` from the process environment.
- Public companion agent IDs may be stored in the client, but private API keys must not be.
- Photos and fishing saves remain local under `Application.persistentDataPath`; the photo system does not upload images.

See [Presage biometric coaching](Deliverables/TherapyGame/Documentation/PresageBiometricCoaching.md) for the consent model, local data flow, install command, confidence filtering, personal-baseline logic, and live-verification checklist.

## Validation status

The project includes Unity editor validators, isolated compile checks, deterministic simulation checks, offscreen renders, reload checks, and saved-scene verification for the major systems. Current automated coverage includes pond propagation and stability, bounded reflections, koi route clearance and population balance, fishing persistence and collection behavior, seasonal restoration, photo encoding and reload, and effect/rendering budgets.

Automated checks do not replace a hands-on Play-mode pass. Input feel, audio balance, live AI services, camera/microphone consent, sustained gameplay frame rate, and final visual quality should be verified interactively on the target machine before release.

Use **The Last Watch → Validate Milestone 2** for the baseline project. Feature-specific commands and recorded results are documented in the linked guides and their `Checks` folders.

See [Architecture](Documentation/Architecture.md) and [Conventions](Documentation/Conventions.md) before extending the project.
