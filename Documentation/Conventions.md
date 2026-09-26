# Naming and folder conventions

- Use the root namespace `TheLastWatch`; add a feature suffix such as `TheLastWatch.Input`.
- Use PascalCase for C# types, assets, prefabs, and scenes; camelCase for locals and private serialized fields.
- Prefix private fields with `_`, except Unity-serialized fields which use readable camelCase names in the Inspector.
- Keep one primary public type per C# file and match the filename to that type.
- Put first-party content under `Assets/_Project`. Keep imported third-party packages or assets outside `_Project`.
- Keep runtime code out of `Editor` folders. Editor-only generation and validation belongs in `Scripts/Editor`.
- Use interfaces at the dialogue, voice, biometric, and input boundaries. External SDK types must not leak into core game logic.
- Scenes are PascalCase and live in `Assets/_Project/Scenes`. `Bootstrap` must remain build index 0.
- Prefabs are grouped by ownership (`Environment`, `Gameplay`, `UI`), not by a third-party package name.
- Configuration assets may contain tunable values, never credentials or tokens.
