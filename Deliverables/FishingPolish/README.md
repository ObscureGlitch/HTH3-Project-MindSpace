# Fishing polish

Controls remain left click to cast at the pond or bridge, Space to hook and reel, I to open the koi inventory, and Esc to cancel or close it.

## Grip, cast and catch

The right hand grips above the reel. The wrist joins the bottom of the palm, fingers wrap the far side of the cork handle, and the thumb points toward the rod tip. The reel is smaller and sits below the hand, rather than crowding it.

Casting now lasts 1.6 seconds with quintic easing and quaternion rotation interpolation. Velocity and acceleration ease continuously through the wind-up, forward flick and follow-through. The animation never moves the player's camera.

On a successful catch, the original koi model starts below the surface, lifts vertically out of the water, then reels toward the player over 1.8 seconds. The fish has a small tail/fin motion and the fishing line follows its mouth. It remains visible with the transparent catch result until dismissed. Cancelling, opening another menu, or disabling the component removes the temporary reveal. The ambient pond school remains intact.

## Transparent fishing UI

Fishing prompts, reeling and result screens, inventory cards and buttons have no filled panel backgrounds or fullscreen dimming. Light text uses a dark shadow for readability. Catch bars, thin interactive outlines, selected-card borders and rarity text keep their colours. The inventory fish render has an alpha-transparent background. Consent, main-menu and other non-fishing screens are unchanged.

## Rarity and catch odds

These are gameplay rarity tiers, not biological facts. Odds apply to a successful catch using the installed eight-variety koi library.

| Koi variety | Tier | Chance |
| --- | --- | --- |
| Kohaku | Common | 35% |
| Benigoi | Common | 25% |
| Sanke | Uncommon | 18% |
| Showa | Uncommon | 12% |
| Asagi | Rare | 6% |
| Ogon | Epic | 2.5% |
| Tancho | Legendary | 1.25% |
| Kigoi | Godly | 0.25% |

New catches store their rarity. Existing saved catches without a rarity field resolve a tier from their variety without rewriting, deleting or recatching them. Rarity appears on the catch result, every inventory card and selected-fish detail. All catches remain local in `Application.persistentDataPath/MindSpaceFishing`, with existing equip persistence and a 500-catch capacity.

## Inventory spin and performance

The selected koi rotates at 18 degrees per second, completing a turn every 20 seconds. Only the open, focused inventory advances it. A single disabled camera manually renders a 512×288 alpha-transparent texture at no more than 30 frames per second. Closing the inventory immediately releases the camera, texture and model. Hidden menus do not render it. Shared original koi meshes and material remain unchanged; runtime rod/hand/catch visuals have no colliders or shadows.

Validation uses compile checks, Edit-mode tests for rarity distribution, legacy/new save compatibility, cast/catch motion, grip placement, cleanup and previous fishing checks, plus offscreen grip/catch/spin render frames. Live Play input and transparent UI legibility remain a manual check. No webcam, microphone or AI session is activated by the checks.
