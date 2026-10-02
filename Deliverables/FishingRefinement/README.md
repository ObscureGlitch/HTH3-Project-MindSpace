# Fishing refinement

Solid deep teal panels, clear catch cards, a spinning model detail panel and explicit page counts replace the transparent fishing interface. The active HUD has only the reel meter, rarity, Space / Click and Esc. The catch result slides into place after the fish clears the water.

Each inventory opening resets to All fish, Newest and page one. Rarity filtering has an explicit dropdown and Show all fish action. Filters never remove records. Existing catches retain their stored rarity, ID, size and date.

Catches use a flushed pending journal before final commit. Completed pending or temporary records can be recovered on the next load. Failed reads retain previously loaded catches and show a warning. Damaged temporary data is preserved as a backup rather than overwritten. Disk failure still requires Retry save before quitting; no code can guarantee a save when storage is unavailable.

Catch motion now has a faster surface exit, a higher lift, rod tug, a small arrival spring, a brief water splash and separate soft launch/reveal sounds. No camera shake, new lights or colliders. Splash geometry is bounded to 72 quads and updates at most 30 Hz. Existing rarity effects, odds and original 15–30 ambient koi are unchanged.

Controls: click to cast beside the pond or from the bridge. Space or click hooks and reels. Enter recasts after a round. I opens the collection. Esc cancels or closes. Clicking outside the collection closes it. Sound is optional in the collection.

The save audit found nine healthy catch records, not deleted fish. A retained rarity filter and six-per-page view could hide them. All nine and the equipped choice are backed up and preserved byte-for-byte. Historically deleted or never-written files cannot be reconstructed.

Validation reports and offscreen renders are in Deliverables/FishingRefinement/Checks. Unity Edit-mode checks cover save recovery, nine saved catches, filters/pages, opaque layout, bounded effects and fishing regressions. Hands-on Play input feel and sound remain manual checks.
