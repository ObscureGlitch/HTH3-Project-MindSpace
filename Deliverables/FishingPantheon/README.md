# MindSpace: Koi Pantheon fishing

This update imports sixteen koi from the provided `koi-pantheon.glb`, retaining supplied markings, horns, whiskers, tails, moons, halos and orbiting bodies. The OBJ and MTL describe the same gallery and are not duplicated into Unity. They remain untouched in Downloads.

The new fishing catalog contains 24 species. The original eight fish and the ambient pond's 15 to 30 koi remain unchanged. Detailed pantheon models appear as catches, equipped fish and the selected inventory preview. They do not multiply the pond school's rendering cost.

## Controls and loop

- Stand near the water or on the bridge, face open water and left click to cast.
- When the float bites, press Space or click. A reaction within 0.65 seconds earns a clean hook and extra starting progress.
- Hold Space or left click to raise the mint bar. Release to lower it. Keep the fish inside the bar. Different species cruise, surge, sway, feint, sweep or perform a celestial dance.
- Sustained tracking builds Flow and slightly speeds progress. A catch with at least 90% tracking accuracy is marked Perfect. Neither escapes nor leaving the game remove existing fish or earned collection progress.
- A caught fish visibly lifts from the water, with its own themed effects. Press Enter or choose Cast again to recast without reopening menus.
- Press I for the collection. Filter catches by rarity, sort by newest, rarity or size, and equip a fish. The selected model slowly rotates.
- The Species journal shows all 24 fish, discovery counts, personal best length and exact current catch chance. Seek this koi is free and multiplies only that fish's selection weight by three, then normalizes all chances. It is not a guaranteed catch. Clear focus to restore the baseline table.
- Collection experience, level and cosmetic title are derived from saved catches. There are no daily streak penalties, purchases, timers or lost XP. New species, clean hooks and Perfect catches contribute to that progress.
- Gentle local fishing sounds can be toggled in the collection. Esc cancels or closes. Clicking outside the collection closes it. The transparent interface keeps the scenery visible.

## Baseline rarity table

These are game collection tiers, not claims about real koi breeding rarity. The supplied files contain names and visual themes but no rarity labels, so these tiers are authored for this game.

| Tier | Koi and per-catch baseline chance | Tier total |
| --- | --- | --- |
| Common | Kohaku 26%, Benigoi 18% | 44% |
| Uncommon | Sanke 14.5%, Showa 9.5% | 24% |
| Rare | Asagi 7%, Kujaku 5%, Kumonryu 4% | 16% |
| Epic | Ogon 2.8%, Kigoi 2.4%, Yurei 1.8%, Sakura 1.6%, Jade 1.4% | 10% |
| Legendary | Tancho 1.2%, Ryujin 0.8%, Raijin 0.65%, Hōō 0.65%, Yuki 0.65%, Abyss 0.55%, Nebula 0.5%, Kitsune 0.5% | 5.5% |
| Godly | Amaterasu 0.2%, Tsukuyomi 0.15%, Void 0.1%, Genesis 0.05% | 0.5% |

Genesis is the rarest baseline species. Focused Genesis chance is about 0.15%, shown precisely as its normalized percentage in the journal. Species is selected at cast start, so its movement style corresponds to the fish you actually catch. Hook and tracking quality do not silently alter these advertised odds.

## Models and effects

- Kujaku: platinum markings and shimmer; Kumonryu: black and white contrast; Yurei: ghostlight.
- Sakura: floating petals; Jade: green and gold radiance.
- Ryujin: crimson dragon crests, antlers and embers; Raijin: horns, orbiting thunder bodies and traveling electric arcs.
- Hōō: fiery plumes and streamers; Yuki: ice shards and frostfall; Abyss: luminous markings, lure and rising motes.
- Nebula: star markings, orbit and violet aura; Kitsune: nine flame-tipped streamers and blue spirit fire.
- Amaterasu: solar corona and golden aura; Tsukuyomi: crescent moon and pearl satellites; Void: horned dark body and rift satellites; Genesis: rainbow markings, halo and prismatic satellites.

Supplied triangle geometry is reduced to 193,311 triangles across sixteen fish (6,084 to 19,474 per species). Articulated fins and animated effect groups use four to six renderers per detailed model. Source point-sprite auras are replaced with bounded Unity mesh effects. Those effects use one renderer, no lights and no particle GameObjects, update at most 30 times per second and stop when hidden. Preview uses one 512 by 288 render target only while the collection is open, capped at 30 Hz.

The derivative shader preserves vertex markings, emissive accents and metallic highlights. Three.js-specific transmission, clearcoat, iridescence and screen-space glow are approximated for the game's stylized Unity renderer; this is not a bit-for-bit recreation of the source renderer. Transparent cornea shells are omitted while irises and pupils are retained.

## Data safety and installation

Existing per-catch files and equipped IDs remain in `persistentDataPath/MindSpaceFishing`. The update does not rewrite old records. In particular, an older Kigoi explicitly saved as Godly retains that rarity; new Kigoi is Epic. A record with no stored rarity resolves the current variety table. Optional accuracy fields are absent on legacy fish, which are not relabeled as Perfect. Capacity is increased from 500 to 5,000 with no automatic deletion.

All preparation is staged in `Deliverables/FishingPantheon`. The installation script checks current source hashes, requires Unity closed and memory headroom, backs up every overwritten file, copies scoped files, builds a separate Resources catalog, then runs Edit-mode checks and offscreen renders. The scene, original koi library, ambient pond, seasons, photo camera, voice/camera consent, player and main menu are not replaced.

The input and real UI still need a hands-on Play check. Automated simulations assess bounds and whether a feedback player can win; they do not establish that the balance is enjoyable for every human player or measure a live FPS improvement.
