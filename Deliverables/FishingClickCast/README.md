# Click to fish and animated casting

Left click while standing beside the pond or on the bridge and facing open water. F is no longer a fishing shortcut. Space still hooks a bite and controls the catch bar; I opens the koi collection; Esc cancels or closes it. All in-game fishing prompts and the settings Controls page are updated.

The first-person cast lasts 1.2 seconds: the visible hand and sleeve lift the rod into a backswing, flick it forward, then settle. The float stays attached through the wind-up, flies from the release point in an arc, and lands with a short expanding ripple. The line follows the rod tip and float. Casting and waiting use a small bottom-left prompt, leaving the animation and water visible. Only the reeling minigame and catch result use the larger panel.

The animation is evaluated from fishing simulation time and is stable across frame rates. It never moves the player's camera. Hands, sleeve, rod, float and ripple are lightweight runtime-only visuals, without colliders or shadows. They are hidden when fishing is cancelled, during menus/photo camera use, and cleaned up on disable. No extra continuous rendering camera, imported animation package, networking or provider access is added.

Click casting requires a fresh left-button press during focused, cursor-locked gameplay with a valid water landing. It cannot cast while seated, in a menu, in the photo camera or album, in the koi collection, or during another fishing round. Closing the collection or a fishing result briefly suppresses recasting to prevent a UI click from casting another line. Remote clicks do nothing. The existing pond, koi models, catch saves and equipped selection are untouched.

Automated checks run in Unity Edit mode only: casting pose and flight continuity, frame-rate-independent timing, click/modal guards, temporary mesh/collider cleanup, previous catch/inventory/pond tests, and rendered animation keyframes. Live Play input and UI interaction remain a manual check. The installer backs up touched sources and documentation, guards source hashes, and confirms the scene and koi asset remain unchanged.
