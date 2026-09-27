# Menu reliability and softer typography

## Cause and repair

The project uses fast Enter Play Mode with both domain reload and scene reload disabled (`m_EnterPlayModeOptions: 3`). `WellnessMainMenu.OnDisable` destroyed its shade texture but retained the managed GUIStyle cache. `Styles` then returned as soon as the old brand style was non-null. The live editor log repeatedly reported `null texture passed to GUI.DrawTexture` at main-menu drawing. The user's screenshot also showed tiny default-black text instead of the authored styles.

The menu now invalidates its entire presentation cache at session boundaries, verifies cached style sizes/texture before reusing them, and restores/normalizes all relevant GUI color state. A runtime session generation guard supports the no-domain/no-scene-reload configuration. An early missing camera reference is retried during Update instead of permanently skipping startup. Once initialized and entered, it does not reopen in the same run.

Consent and pause-menu presentation caches are also disposed/reset on enable/disable and rebuilt with fresh styles. Consent logic, device ownership, calibration thresholds and the provider are unchanged.

## Requested UI changes

- Main-menu default starts at 12:00 using new serialized `titleStartHour = 12`. The obsolete `openingHour` field is intentionally not migrated, so an existing scene's saved 01:30 value cannot override noon. The title's day/night cycle continues normally, and presets still work.
- Left-clicking outside any main-menu subpanel (Settings, How to play, Credits, exit confirmation) returns to the title. Both press and release are consumed so a click on the underlying Enter option does not enter gameplay. Inside clicks, keyboard navigation, Esc, and the existing hover/pop animation remain functional.
- The consent-screen companion selector becomes a 234 × 40 outlined sage button with a person icon and 18px label, separated from the privacy link. It still opens the companion choices; no selection is changed merely by opening it.
- Existing open-license Carlito sans-serif replaces sharp serif headings/buttons in main, pause, consent and camera-test menus. No new font or shader is imported.

## Verification

`Check.ps1` verifies outside-click boundaries for all four subpages, menu animation/state transitions, consent defaults/dependencies, current real camera-gate regressions, source-level lifecycle/cache guards, and 77 scaled control bounds checks. It compiles the complete runtime/editor assemblies against current live sources with only six menu replacements. `CheckTypography.mjs` measures 66 text cases using the actual Carlito font.

These are code/headless checks, not proof of a Unity-rendered result. No Play, webcam, microphone, biometric values, AI connection, shader compile, second Unity process, GPU preview or bake is triggered. Existing fast-play settings remain unchanged.

## User-controlled Play regression

After Assets → Refresh, start and stop Play three times. Each run should show full-size light title text over the live garden at noon, with no null-texture warnings. Open every submenu and click outside it; it should close without entering the game or quitting. Check the larger companion button and sans-serif text after Enter. Device testing is optional and remains behind affirmative consent.
