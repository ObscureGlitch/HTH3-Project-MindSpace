# One large puffy cloud bank

Puffy cumulus now shows **one connected, towering bank**, not scattered independent puffs. It uses the first existing cloud card with a full-image texture and disables the other 15 renderers. Soft banks and altocumulus keep their existing layouts. Auto, Off and manual selections remain under Esc → Sky & weather → Clouds. Only one family is bound at once, including the pond. The original cumulus atlas is retained as a fallback, not drawn with the bank.

## Research and the game interpretation

- The [Met Office cloud guide](https://weather.metoffice.gov.uk/learn-about/weather/types-of-weather/clouds/low-level-clouds) describes convection-driven cumulus, bright rounded tops, relatively dark bases, and development into taller congestus clouds. The texture uses that rounded structure and shaded underside; geometry grows upward above a stable apparent base.
- The [WMO International Cloud Atlas — cumulus congestus](https://cloudatlas.wmo.int/en/species-cumulus-congestus-cu-con.html) describes strong vertical development and cauliflower-like upper bulges. This is the reference for the towering bank, not scattered shallow fair-weather cumulus.
- The [Met Office observer guide](https://www.metoffice.gov.uk/binaries/content/assets/metofficegovuk/pdf/research/library-and-archive/library/publications/weather--climate-guides/cloud_types_for_observers_rev_2014.pdf) describes roughly horizontal bases and notes that land-based cumulus commonly disperses in the late afternoon or early evening. Auto mode develops the bank through the day and gradually dissipates it after dusk. Manually selecting Puffy cumulus deliberately keeps it visible at night, respecting the player's choice.

These are visual approximations, not a meteorological or fluid simulation. Real clouds can behave differently with moisture, stability, wind and geography. The connected composition, sizes and timing are artistic choices matching the user's reference. No thunderstorms, lightning or forced rain were introduced. Rain remains independently controlled.

## Behavior and rendering

- Virtual radial distance: 7 km. A base direction equivalent to about 650 m altitude at that distance stays fixed while the top grows. Width varies gently from about 9.0 to 9.6 km and full billboard height from 3.7 to 4.7 km. Padding means the visible vapor occupies less than that rectangle.
- Heating ramps up from game hours 07–13, stays strongest until 16, and declines by 20. Development follows this target with a 45-real-second response. It does not pulse, bob, rotate toward the player's look direction or rapidly boil. This simplified timing is authored for the game, not a measured cloud lifetime.
- Existing wind remains 2.4 m/s by default; at the chosen virtual radius this is about 1.18 degrees/minute. Drift uses real elapsed seconds independently of the accelerated day clock. A fixed world-space heading drifts continuously without wrapping or teleporting the bank across the screen. The initial bank is placed near the initial view; later events use randomized world headings.
- One existing renderer, two triangles, one runtime material and one small runtime mesh copy. No new GameObjects, lights, capture cameras, render passes or volumetric ray marching. Other families still use their existing 16 cards. The bank mesh and renderer-enabled states restore on family changes and Play exit.
- One new alpha texture, mipmapped DXT5 at 2048×1024 (about 2.67 MiB including mipmaps), not CPU-readable. Opaque vapor cores hide stars instead of making the whole cloud translucent.
- The pond samples the full bank texture with matching direction, size and alpha. A shared visibility helper masks the bank behind the existing painted mountain/treetop silhouettes in both the sky and reflection. It reproduces their current contours without changing the landscape shader or adding mountain models.

## Verification

Pure CPU tests cover daylight development, evening dissipation, monotonic bounded changes, 30/120 FPS agreement, pause, wind, exactly one active puffy card, restoring the other families and Off. Existing four-hour cloud sequence and all 25 mode-transition checks also run. Full compilation uses current live sources with only the cloud deck/new bank files replaced. The Unity installer checks fixed base elevation at 24 headings, matching sky/pond UVs, compressed alpha import, both affected shader passes, and saved scene references. It creates recoverable scene backups and refuses low-memory imports; no automatic retries, Play mode, microphone, rendering preview, bake or reflection capture are started. Visual Play-mode appearance and frame time remain a separate user-controlled check.

Report: `Assets/TherapyGame/Weather/CloudTypes/CumulusBankCheck.txt`.
Manual installer if needed: `Therapy Game → Install Single Cumulus Bank`.

## Generated asset and full prompt

Created with the **built-in image generation tool**, not a CLI/API fallback. This is a new asset inspired by the reference, not an edit of the supplied photograph. Generated RGBA source is 1774×887 and copied unchanged; alpha inspection found 844,332 clear pixels, 639,002 opaque pixels and 90,204 semitransparent pixels. Mean alpha in the outer 1% border is 0.00000104. No post-generation image editing was used.

Workspace asset: `D:\Hack the Hill\Deliverables\TherapyGame\Weather\Textures\CloudTypes\CumulusBank.png`

Installed asset: `D:\Unity\HTH3 Project\Assets\TherapyGame\Weather\Textures\CloudTypes\CumulusBank.png`

Original retained: `C:\Users\obscu\.codex\generated_images\01a0dc83-2050-7761-b5fd-dd5bd5a639a8\exec-39dab0eb-cf12-45cb-95b0-28654b215a18.png`

Use case: photorealistic-natural. Asset type: high-resolution transparent cloud sprite for a peaceful Unity game's distant sky. Generate ONE single enormous CONNECTED cumulus congestus cloud bank, NOT a collection of separate clouds and NOT a sprite sheet. Wide 2:1 landscape canvas. A spectacular sunlit billowing cloud mountain: broad continuous low base, a majestic tall central cauliflower tower, several attached rounded lobes and shorter shoulders joined into the same silhouette. The bank should feel monumental from ground level, with the huge white domed towers seen just above a distant horizon, but include no actual horizon or scenery. Brilliant soft neutral-white sunlit crests, beautifully detailed pale silver-gray folds and softly shaded gray underside. Dense opaque vapor core, finely feathered semitransparent edges. No anvil, no storm damage, no lightning, no detached mini-puffs. Keep the overall bottom approximately horizontal with soft natural variation. Fit the WHOLE silhouette within the canvas, occupying about 88 percent width and 80 percent height, with genuinely transparent padding on all four sides; do not crop any part. Natural photographic water-vapor detail, lush volumetric depth, neutral grayscale for runtime daylight/sunset/night tinting. Absolutely no blue sky, no landscape, no grass, no people, no buildings, no text, no logos, no watermarks, no drop shadow outside the cloud, no painted checkerboard; genuine alpha transparency everywhere outside this ONE cloud bank.
