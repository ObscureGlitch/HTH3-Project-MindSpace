# Koi signature VFX

Only Legendary and Godly catches create rarity VFX. Common, Uncommon, Rare and Epic fish have none. Physical water splashes on catches remain for all fish. Species meshes, skins, sizes, odds, inventory and save records are unchanged.

Legendary themes:

| Koi | Signature | Colors and motion |
| --- | --- | --- |
| Tancho | Crimson crane | White feather arcs, red crest and drifting crimson petals |
| Ryujin | Dragonfire helix | Red and gold counter-wound fire currents and embers |
| Raijin | Thunderstorm coils | Cyan and violet traveling electric coils |
| Hōō | Phoenix plumage | Orange and golden feather fans and rising embers |
| Yuki | Crystal frostfall | Silver and icy cyan snow crystals and falling shards |
| Abyss | Bioluminescent depths | Teal glowing tendrils and rising deep-water motes |
| Nebula | Spiral nebula | Violet and magenta galaxy arms and orbiting stardust |
| Kitsune | Nine spirit flames | Nine sapphire and pearl wisps with flowing flame tails |

Godly themes:

| Koi | Signature | Colors and motion |
| --- | --- | --- |
| Amaterasu | Solar ascension | Gold and orange solar corona, rays and looping flares |
| Tsukuyomi | Lunar procession | Pearl and periwinkle crescents, comet trails and constellations |
| Void | Event horizon | A dark lens behind the fish, violet accretion streams and inward spirals |
| Genesis | Prismatic creation | Spectrum feather wings, braided aurora ribbons, celestial orbits and crystal constellations |

Every Godly theme has more persistent geometry than the most detailed Legendary theme, wider silhouettes, extra stars and three staggered reveal waves instead of two. Historical stored Godly fish keep their rarity and receive a matching fallback signature.

Effects appear on caught fish, equipped fish and the selected spinning inventory model. Held effects are smaller and softer. Preview framing expands for Legendary and Godly only. No global flashes, camera shake, extra lights, particle objects or extra effect cameras. Each effect uses one shader/material/mesh, up to 1024 quads, capped at 30 Hz, with full close/hide cleanup.

Checks and actual Unity model/catch renders are in Deliverables/KoiSignatureVfx/Checks. No Play mode, webcam or microphone is used by verification. Visual renders are reviewed before handoff; hands-on motion/audio feel and sustained FPS remain manual checks.
