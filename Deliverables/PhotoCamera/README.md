# MindSpace photo camera

Installed into `D:\Unity\HTH3 Project`. Reopen TherapyRoom, press Play, enter the game and finish the usual consent choices. The photo camera is separate from the wellness webcam and does not need microphone or webcam permission.

## Controls

- C: hold or put away the photo camera.
- Mouse wheel: 1–4× optical zoom.
- Left click or Space: capture the current game view without the HUD.
- G: open the photo inventory. Also available under pause settings → Controls.
- Click a thumbnail: open the full image. Arrow keys or the on-screen arrows browse photos.
- Esc: return to the album, close the album, or put the camera away.
- Click outside the album panel: close it.

Normal looking and walking remain available while holding the camera. Interaction and standing shortcuts are suspended while photographing. The gallery releases the cursor and blocks player movement, while the world continues animating.

## Storage and performance

Photos persist in `Application.persistentDataPath/MindSpacePhotos`. Each has an original PNG (up to 1920×1080, original aspect ratio), a 320px JPEG thumbnail and JSON metadata. The gallery loads only six thumbnails per page and one opened photo. No automatic photo deletion, network upload, webcam capture or continuous extra camera render is used. The safe album limit is 200 photos; moving older photo files out of the folder makes room.

Affected scripts and the previous saved scene are backed up beneath the Unity project's `TherapyBackups/PhotoCamera` folder. The scene change adds one non-rendering component linked to the existing gameplay camera.

## Verification

Current Runtime and Editor trees compiled with the staged changes. Headless Unity tests exercised actual image encoding, file save, metadata commit, album reload, image decoding and exact PNG pixel colors; duplicate and unsafe-ID protection; damaged metadata tolerance; album capacity; optical FOV zoom and aspect-preserving capture dimensions. Unity imported the scripts and saved component references.

Still needs an interactive Play-mode test: capture an outdoor scene, inspect screenshot orientation and colors in the gallery, zoom while photographing, confirm HUD exclusion, close the gallery and verify movement/FOV restoration, then restart Play and confirm photos remain. Headless tests did not start Play, voice or webcam services.
