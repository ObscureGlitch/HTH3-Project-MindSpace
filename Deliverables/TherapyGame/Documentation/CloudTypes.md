# Exclusive cloud families

## Player controls

Open **Esc → Sky & weather → Clouds**. Clouds are their own event system, independent of rain and northern lights. The Clouds toggle hides every cloud (including its pond reflection). **Automatic cloud events** restores occasional randomized changes; turn it off to keep the current family, or click **Soft banks**, **Puffy cumulus** or **Altocumulus** to choose a manual shape. A manual selection immediately replaces the one atlas, even with the game paused; it never layers another family on top and remains selected until the player enables Auto. Cloud drift is on this page too. Existing rain and night-effect settings are untouched. Settings apply to the current play session, like the existing weather controls.

The lower-left music widget while exploring is now a **222×54 display-only card**, containing only the stable spinning record, song title and creator (plus the existing subtle notes). No previous/next/pause buttons, timer or progress line obscure the view. Playback controls remain in the pause-menu music panel; existing keyboard shortcuts remain available.

Additional checks cover all 25 transitions between Auto, Off and the three manual shapes, immediate paused changes, manual locks, a selection during fade-out, invalid choices, and resuming Auto. Editor checks cover the compact music rectangle at four resolutions, unchanged pause-menu controls, and the separate Clouds settings page.

The existing 16 sky cards now use one of three cloud families: the original soft banks, new billowy **puffy cumulus**, or high rippled **altocumulus**. Play starts with cumulus. After a randomized 3–5 real-minute hold, the current clouds fade out for 18 seconds, the sky stays clear for one second, and the next family fades in for 18 seconds. A shuffled selection prevents immediate repeats and makes sure all types are visited. Pausing/focus loss pauses the sequence; accelerated day time does not speed it up.

There is only **one active cloud atlas** on the one shared runtime material. Switching happens at zero opacity; different families never crossfade over one another. The pond receives the same atlas, card positions/sizes and opacity. Weather still controls coverage, cloud tint and rain. Wind remains the existing 2.4 metres/second, with subtle per-card variation; altocumulus is higher and has slower apparent drift. The original scene material/texture is kept unchanged as the soft-bank family and compatibility fallback.

No cloud geometry, lights, shaders or render passes were added: still 16 quads / 32 triangles. Two new 2048×1024 mipmapped DXT5 textures use roughly 5.3 MiB GPU memory together (plus normal engine overhead). Textures are not CPU-readable. Runtime reuses the existing property blocks, arrays and single cloned material. No microphone, Play mode, bake, reflection-camera capture or automatic retry is started by the installer.

## Installation and checks

With TherapyRoom open and Play stopped, the one-time request runs `Therapy Game > Install Cloud Types`. It imports only the two new textures, checks the existing cloud/pond shaders for errors without forcing a recompile, preserves the on-disk and open scene under the project's `TherapyBackups/CloudTypes` folder, sets the deck's two new texture references, saves and verifies those references. It will stop rather than continue with inadequate memory. If it stops, free memory and use that menu item manually.

Report: `Assets/TherapyGame/Weather/CloudTypes/CloudTypesCheck.txt`. A successful install records `installed-live-check-pending` in `CloudTypesRequest.txt`.

CPU sequence checks simulate four hours (576,000 checks): bounded smooth opacity, zero-opacity switches, clear gap, three-family coverage, pause, invalid/stalled time and matching 32/64 FPS sequences. Full C# compilation uses current live project sources with only scoped cloud replacements. Scene checks validate all 16 projections for all three families, preserved material/triangle counts, original atlas, compressed alpha textures and saved references. These are not a visual Play-mode or GPU performance test; the appearance and frame time still need a user-controlled Play check.

The source PNGs are original built-in image-generation output, 1774×887 RGBA, with real alpha. Unity resizes to the nearest 2:1 power-of-two size for compression. Alpha was inspected before import: almost completely transparent cell borders (mean border alpha below 0.002), semitransparent vapor edges, and transparent gaps between altocumulus cloudlets. The reference images informed the shapes but were not copied into the game or edited to remove their marks.

## Saved assets and generation provenance

Generated using the **built-in image generation tool**, not a CLI/API fallback. Each was a fresh image with transparent background requested. No post-generation image editing was applied.

Workspace assets:

- `D:\Hack the Hill\Deliverables\TherapyGame\Weather\Textures\CloudTypes\PuffyCumulusAtlas.png`
- `D:\Hack the Hill\Deliverables\TherapyGame\Weather\Textures\CloudTypes\AltocumulusAtlas.png`

Installed assets:

- `D:\Unity\HTH3 Project\Assets\TherapyGame\Weather\Textures\CloudTypes\PuffyCumulusAtlas.png`
- `D:\Unity\HTH3 Project\Assets\TherapyGame\Weather\Textures\CloudTypes\AltocumulusAtlas.png`

Original generation files (retained):

- `C:\Users\obscu\.codex\generated_images\01a0dc83-2050-7761-b5fd-dd5bd5a639a8\exec-8a20eb2b-8cc3-4437-a1ad-211c1c2e9a38.png`
- `C:\Users\obscu\.codex\generated_images\01a0dc83-2050-7761-b5fd-dd5bd5a639a8\exec-4ee72235-ab55-435e-9a39-03b236b8f8a6.png`

### Full prompt: puffy cumulus

Use case: photorealistic-natural. Asset type: production Unity game cloud sprite atlas on a genuinely transparent background. Create ONE wide 2:1 aspect ratio image, ideally 2048 by 1024 pixels. Exactly FOUR equal columns and TWO equal rows, eight evenly sized invisible cells. In each cell place a different complete isolated puffy CUMULUS cloud bank, centered, with generous clear transparent padding on every side (at least 12% of the cell). No cloud touches another cell. Eight distinctly varied natural silhouettes: rounded billowing cauliflower tops, several taller cottony towers, several broad multi-lobed banks, soft flat-ish bottoms. Match the lush, soft, voluminous white clouds used in peaceful exploration games, but with photographic water-vapor detail rather than cartoon blobs. Bright neutral-white rounded highlights, softly shaded silver-gray undersides and small internal folds, neutral grayscale colors for runtime weather tinting. View from ground looking slightly upward, no horizon or perspective ground. Preserve fine semi-transparent wispy edges and actual alpha. Entire silhouettes must fit within their own cells; do not crop any cloud. The background and gutters must be completely transparent, no blue sky, no ground, no rectangle backgrounds, no checkerboard pattern painted into the image, no shadows outside the clouds, no labels, no borders, no grid lines, no text, no watermark.

### Full prompt: altocumulus

Use case: photorealistic-natural. Asset type: production Unity game ALTOCUMULUS cloud-bank sprite atlas on a genuinely transparent background. One wide 2:1 aspect-ratio image, ideally 2048 by 1024 pixels. Exactly FOUR equal columns and TWO equal rows, eight invisible equal cells with clear transparent gutters and at least 10 percent padding inside every cell. Each cell contains a DIFFERENT separate broad soft-edged patch of altocumulus cloudlets, viewed looking up: dozens of small white rounded cotton tufts arranged in loosely undulating parallel rows, rippled mackerel-sky bands, subtle natural irregularity and blue-sky-shaped gaps that are actually completely transparent. These are thin high cloud SHEETS made of many tiny puffs, NOT eight giant cumulus towers, NOT isolated single clouds, NOT cirrus streaks. Individual cloudlets have softly lit ivory-white tops and pale neutral silver-gray shaded undersides, photorealistic fine vapor edges, neutral grayscale for runtime weather tinting. Vary each patch's wave direction, cloudlet spacing, broken edges and winding band shapes. Every entire patch stays within its own cell and fades irregularly to transparency at its perimeter; no rectangular hard cutoffs. Absolutely no blue sky or colored background, no landscape, no sun, no buildings, no horizon, no text, no logos, no watermarks, no drawn checkerboard, no grid lines. Genuine alpha transparency between cloudlets and around all eight patches.
