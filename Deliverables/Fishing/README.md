# Koi fishing

Installed into TherapyRoom without replacing the pond, existing fish models, photo camera or menus.

## Player controls

- Stand at the pond's edge or on the bridge, facing open water. Press **F** to cast.
- Watch the float. When a bite appears, press **Space** within four seconds.
- Hold or tap **Space** to raise the green catch bar; release it to lower the bar. Keep the orange koi marker inside the bar until progress fills.
- Press **Esc** or **F** to cancel or return to the pond. After a catch, choose **View your koi** or press **I** after closing the result.
- Press **I** anywhere in the game to open your koi inventory. Select a caught koi and choose **Equip this koi**. Choose **Put koi away** to stow it. Putting it away does not remove it from your collection.
- Settings → Controls also includes an **Open koi inventory** button.

## Content and behaviour

Only the eight varieties already in KoiLibrary are catchable. Catches choose a variety randomly and have the same modest 0.90–1.08 size variation as the pond's koi. Inventory previews and the equipped first-person fish reuse the original meshes, parts and vertex-colour material. Length and weight are cosmetic game estimates, not measurements of the source models.

The existing randomized 15–30 swimming koi remain an ambient school. Fishing does not remove them or change the pond scenery. Fishing locks movement and looking while the minigame or collection is open. Settings, the photo camera, and the main menu cannot share its shortcuts. Fishing time pauses when the app loses focus.

## Local saves

Catches are saved individually in `Application.persistentDataPath/MindSpaceFishing`. The currently equipped koi is remembered across sessions. Capacity is 500 catches; no catches are automatically deleted. Failed saves remain available for retry in the inventory during the current session. Corrupt records are skipped while valid records remain visible. No network, webcam, microphone or AI requests are made by fishing.

## Performance and validation

No extra scene camera or geometry is saved. The small rod/float view meshes are created only on the first cast, have no colliders or shadows, and are cleaned up when the component disables. Water eligibility is checked at most about seven times per second while idle. A 512×288 koi preview is rendered only when a selection changes and is released when the inventory closes, rather than rendering an extra camera continuously.

`CompileChecks.ps1` checks runtime and editor compilation. `TherapyFishingSetup` performs deterministic round tests at 30/60/120 Hz, local-save tests in isolated temporary fixtures, actual scene water/bridge checks, and original koi model checks without entering Play mode. `Install.ps1` guards live source hashes, backs up the touched files and scene, installs, and verifies a fresh Unity reload. Live Play-mode input and UI interaction remain a manual test; automated checks never enable a camera, microphone or voice session.
