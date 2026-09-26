# Architecture overview

`GameBootstrap` is the composition root. It survives scene changes, creates the small set of core services, and owns their lifetime through `GameServices`. Runtime code depends on project interfaces rather than external providers or Unity input lookups.

## Boundaries

- **Core** owns initialization, game state, pause behavior, scene transitions, logging, and non-secret configuration access.
- **Input** owns action names and translates the Unity Input System asset into `IGameInput`.
- **Dialogue**, **Voice**, and **Biometrics** expose provider interfaces. Their null providers are deterministic, offline, and device-free.
- **Player**, **Interaction**, **Environment**, **Audio**, and **UI** are reserved presentation/gameplay boundaries for later milestones.
- **Editor** creates and validates generated Unity assets. Runtime assemblies do not reference editor APIs.

## Runtime flow

1. Build scene 0 creates `GameBootstrap`.
2. The bootstrap reads a non-secret `GameConfiguration` asset and creates `GameServices`.
3. Input, state, logging, scene transitions, pause handling, and null external providers become available.
4. The configured initial scene loads asynchronously.
5. `SceneContext` records the high-level state for the loaded scene.

The static `GameServices` container is intentionally small and acts only as the project composition root. Feature code should receive the narrow interface it needs whenever practical; it should not add unrelated global state.

## Future integrations

Replace one null provider at the bootstrap boundary while keeping its interface stable. Provider SDK code should live outside `_Project` when third-party-owned, and project adapters should remain in the matching `_Project/Scripts` folder. No provider may assume credentials, network access, microphone access, or a connected biometric device.
