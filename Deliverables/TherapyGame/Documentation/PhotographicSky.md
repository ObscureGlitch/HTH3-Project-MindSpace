# Photographic cloud layers and natural night sky

This refines the existing weather/day-night system; it does not rebuild the garden or room. Use **V → Sky & weather** for the existing time, weather and rain controls plus the new **Cloud drift** slider (0–4 m/s). Choose **Night** to view stars during a user-controlled Play session. The microphone remains opt-in.

## What changed

- Eight generated photographic cloud shapes, arranged as 16 layers with different size/aspect ratio, placement, opacity, slight tilt and mirrored variants. Soft source alpha replaces faceted silhouettes. Clouds change tint at dawn/night and increase in coverage during rain.
- A texture-free natural sky shader with a smoother atmospheric gradient, restrained sunward haze, approximately half-degree sun/moon discs, and a darker night background.
- A procedural star field with irregular positions, brightness, sizes and subtle warm/cool colors. Stars fade in after twilight, diminish toward the horizon and under overcast, and remain behind the cloud layers. No flashing or animated twinkle; the very slow star drift is independent of the accelerated day clock. It is an artistic star distribution, not an astronomical catalogue.
- Old 3D cloud objects and their assets remain in the scene/project but are disabled and no longer animated. The pre-refinement scene is preserved in `Weather/Backups/TherapyRoom_BeforePhotographicSky.unity`.

## Real-time drift model

Default wind is **2.4 m/s** (about 5.4 mph), with small per-cloud variations of 0.90–1.08. Clouds are placed on a **virtual layer 1,500–2,100 metres above the observer** and projected onto the distant sky; the small game's world scale does not make them sweep past like nearby objects. Wind uses elapsed game-frame seconds, not the 12-minute day/night clock. Holding the time-of-day clock therefore does not freeze cloud motion. Zero wind holds cloud positions. Leaving game focus or pausing game time stops advection rather than catching up abruptly.

At 2.4 m/s and 1,500 m, maximum overhead angular drift is approximately `wind / altitude = 0.0016 rad/s = 0.0917 degrees/s`. That is around **11 minutes to cross 60 degrees** near overhead, slower toward the horizon. At the highest default variation and lowest allowed altitude, it stays below 0.10 degrees/s. Weather changes do not multiply the wind speed.

This is a plausible **calm-weather artistic choice**, not a claim that all clouds move at this speed. The [National Weather Service Beaufort scale](https://www.weather.gov/mfl/beaufort) describes 4–7 mph as a light breeze; surface wind classifications are only a reference here, not a measurement of wind aloft. The [Met Office cloud factsheet](https://www.metoffice.gov.uk/api/assets/file/factsheet_1-clouds_2023pdf?prefix=assets) gives usual cumulus base heights of 300–1,500 m with a wider observed range to 2,000 m. Cloud tops and layered formations may be higher; actual cloud motion depends on the wind at cloud height.

## Laptop budget and rendering

Sixteen transparent cards use **32 triangles total**, one shared runtime material and one mipmapped, alpha-compressed atlas capped at 2048 pixels. Opaque geometry always occludes the clouds using sky-depth projection; clouds cannot cover a mountain just because the projection shell is smaller than the mountain's distance. Cloud positions follow the viewing camera only to remove nearby-object parallax, while their virtual positions advect slowly in metres.

No cloud colliders, cloud shadows, new lights, render cameras, volumetric raymarching, reflection captures, dynamic GI refresh or lighting bake. Existing frame-rate/URP settings, voice consent, rain ducking, warm indoor lighting and pond behavior remain. Alpha-layer cost still depends on screen coverage; reduced geometry is not a measured FPS guarantee. GPU appearance/performance is not verified by CPU-only checks.

## Asset provenance and prompt record

Created with the **built-in image-generation tool**, following the imagegen skill. No paid-stock texture or external cloud photograph was downloaded. Source alpha was inspected before import (including genuinely empty and partially transparent pixels); it is preserved by Unity's alpha-aware texture importer. The shader converts cloud RGB to neutral luminance before weather tinting to avoid small colored fringes.

Selected project asset: `D:/Hack the Hill/Deliverables/TherapyGame/Weather/Textures/PhotographicCloudAtlas.png`.

Live Unity asset after import: `D:/Unity/HTH3 Project/Assets/TherapyGame/Weather/Textures/PhotographicCloudAtlas.png`.

The generated source is 1774×887 RGBA. Unity rescales the 2:1 atlas to a nearby power-of-two size for reliable compressed mipmaps, capped at 2048; this does not create additional photographed detail. Source texture is not left only in the image-generation cache.

### Initial generation prompt

> Use case: photorealistic-natural. Asset type: production Unity game cloud sprite atlas, one wide 2048x1024 transparent PNG. Create EXACTLY EIGHT distinct photorealistic fair-weather cloud cutouts arranged in an exact 4-column by 2-row equal-cell grid. Each cloud is centered entirely inside its own cell, leaving at least 12% transparent padding to all cell borders. No cloud may touch another cell. Top row: small ragged cumulus humilis; broad asymmetric cauliflower cumulus; broken elongated cumulus fragment; tall but gentle cumulus cluster. Bottom row: wide flat-base stratocumulus bank; thin wispy broken cloud; airy layered cloud fragments; soft rounded medium cumulus with trailing wisps. Ground-observer view of sides and slightly underneath, not overhead. Genuine natural condensed-water texture, irregular wispy semitransparent edges, diffuse white highlights, neutral gray softly shaded undersides, believable three-dimensional volume in photographic texture, restrained contrast. Diffuse upper daylight with no hard baked sun direction so the game can tint it at dawn and night. Grayscale / neutral white and gray cloud RGB. Genuinely transparent background and semitransparent thin cloud edges; preserve alpha, not an illustrated checkerboard. This is an actual game texture atlas, not a screenshot of a UI. No blue sky, horizon, terrain, sun, stars, text, numbers, borders, labels, panel lines, logos or watermark. No cartoon outlines, polygon facets, identical repeated silhouettes, smoky dark plumes, thunderheads or fluffy cotton balls.

### Final refinement prompt

> Edit target: the attached transparent 4 by 2 photographic cloud sprite atlas. Preserve exactly eight distinct realistic cloud formations in an exact four-column two-row equal-cell layout. Improve ONLY edge quality, photographic softness and padding: shrink/reposition each cloud so it fits entirely within the central 72% of its individual cell, leaving at least 14% completely empty transparent margin on every side. No speck or wisp touches any cell border. Remove all blue/cyan color contamination and all bright white sticker-like cutout outlines. Cloud boundaries should dissolve smoothly into true semitransparent alpha wisps, with neutral grayscale water-vapor texture and soft neutral gray undersides, not hard cutout borders. Keep the distinct asymmetric cloud shapes and fine real cloud volume. Maintain genuinely transparent background, no checkerboard painted into pixels, no text, no grid lines, no landscape, no sky, no sun or stars. Aim for photographic natural clouds rather than outlined or sharpened illustrations. Output one wide 2:1 PNG atlas, 4 equal columns x 2 equal rows.

The result is not a mathematically perfect padded atlas; the shader additionally fades the outer 4.5% of each cell to avoid cut edges/mipmap seams. Prompt wording is a request, not a claim that every generated pixel meets it.

## Safe import/checks

`SkyRefinementRequest.txt` is a one-use import gate. Outside Play/baking, **Assets → Refresh** imports the files and applies the update. If it defers, use **Therapy Game → Refine Clouds and Night Sky**. The importer first backs up the open scene, preserving unsaved edits. `SkyRefinementCheck.txt` records shader import/reference, cloud budget/variation, angular speed, minute-by-minute movement, wrap, projection, and previous day/weather math checks.

No automated microphone call, Play mode, bake or rendering preview is part of those checks. The final appearance still needs a short user-controlled test when the laptop has adequate headroom.

### Verified after refresh — 26 September 2026

Unity completed the one-use import and saved the scene at 17:34:15. Runtime/editor C# compilation and Unity shader import checks passed. All 19 CPU-only photographic-sky audit checks passed, including 16 active layers, eight source tiles, 16 unique sizes, 32 triangles, disabled cloud shadows/probes, material/texture references, and preservation of the existing lights, baked settings, rain and voice links. The fastest saved default cloud drift is approximately 0.0962 degrees/second. All 20 existing garden import checks also passed.

These results verify saved assets, references and math, not rendered appearance or frame rate. No Play session, microphone session, lighting bake or GPU preview was started.
