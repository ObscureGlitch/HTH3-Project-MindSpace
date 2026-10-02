# Natural pond floor

With TherapyRoom open and Play mode stopped, select **Therapy Game > Install Natural Pond Floor**. This replaces only the existing combined pond-floor mesh, preserving the current fish count, fish assets, water, terrain, materials and safety setup. It does not add the new scenery on top of the old layout.

The layout contains 88 individually generated pieces in seven distinct model families:

- 25 asymmetric river stones, with varied outlines, heights, facet patterns and earthy colors.
- 14 angular slate fragments, some with irregular layered ledges.
- 12 gravel pockets with different grain counts, individual pebble shapes and loose arrangements.
- 6 forked pieces of driftwood with unique bends, branches and tapered broken ends.
- 9 broadleaf aquatic rosettes with varied leaf counts, curves and widths.
- 12 ribbon-grass tufts with different blade directions and heights.
- 10 small fallen leaves with individual outlines, folds and muted colors.

Models are built from distinct vertex geometry; they are not copies of one mesh placed at different scales. Each piece has a unique local-geometry signature. The layout is seeded and baked once, so it does not reshuffle or allocate geometry during gameplay.

Placement uses irregular density and spacing, not an evenly spaced ring or grid. Plants occupy deeper pockets near the margins; low stones and debris scatter across the floor. Open patches remain between groupings. Every vertex is checked against the authored water outline and actual pond terrain before the scene is modified. The south entry is kept clear, and low-profile decorations remain at least 5 cm below the conservative fish envelope.

All 88 pieces are combined into one mesh: 17,092 triangles, one existing material, one renderer, no colliders, no textures, no new shader and no runtime generation. The original floor asset is retained, and the installer backs up the open scene under `TherapyBackups/KoiPond/NaturalFloor-<timestamp>`. Use Undo immediately after installation, or restore that backup, to recover the prior layout.

Repeating the install command validates the existing natural floor without creating duplicate scenery. The saved report is `Assets/TherapyGame/Documentation/NaturalPondFloorCheck.txt`. Offline code, geometry, and preview checks do not replace a Play-mode visual check through the game's water.
