# Conversation-driven companion animations

Julien and Camille keep the corrected standing meshes, materials, interaction colliders, 57-bone rigs, and five live vowel blend shapes already installed in the Therapy Room. The supplied animated GLBs match those rigs, so this upgrade reconstructs their body/eye clips on the existing characters instead of replacing the visible models.

## Deterministic state mapping

| Game event | Clip |
| --- | --- |
| Companion is activated | `Wave` once |
| No voice conversation is connected | `Idle` loop |
| Connected, waiting for the user, or VAD detects the user speaking | `Listen` loop |
| A completed user transcript arrives | `Nod` once |
| A completed user turn is waiting for an agent response | `Think` loop |
| The ElevenLabs conversation reports agent speech | `Talk` loop |
| Agent speech ends | `Write` once, unless the user has already started speaking |

`Walk` and `Sit` are imported for completeness but are not selected while the companions remain stationary and standing. There are no probability-based body-animation choices.

The animated GLBs also contain generic morph-target motion. Those tracks are intentionally excluded: `WellnessSpeechAnalysis` continues to drive A/I/U/E/O face shapes from the actual companion audio during `Talk`, so mouth timing remains tied to what the user hears.

## Supplied source verification

- Julien: `1c0f781e31bf70756c8b5b4775ca3d8ee3301efb43d7d1dece78db4eb53c5545`
- Camille: `69130fbf9e5d7e6308b730f495c17f19b48c80a1cf81f7e45a61ca1bfd2e0c85`
- Each pack contains `Idle`, `Listen`, `Talk`, `Nod`, `Write`, `Think`, `Wave`, `Walk`, and `Sit`, with 33 retained transform tracks per clip.

The importer backs up the open scene, verifies every animated transform path, refuses a partial installation, and confirms that renderer meshes, triangle counts, materials, transforms, colliders, and live vowel shapes did not change.

## Installed verification

The live Therapy Room import completed on 2026-09-27. Offline Unity runtime/editor compilation passed, all 18 generated clip assets passed serialized curve and duration checks, and the scene audit found exactly two new `Animation` components. After accounting for those component references and the two `bodyAnimation` links, every pre-existing serialized scene document was byte-identical to the automatic pre-install backup.

Play mode, microphone capture, and a remote AI session were deliberately not started during installation. A short user-controlled conversation remains the final visual/timing check for the authored gestures against real provider latency.
