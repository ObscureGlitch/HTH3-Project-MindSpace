# Summon northern lights

In Play, open **Esc → Sky & weather → Night sky → Summon northern lights**.

The button explicitly enables aurora, clears rain/weather and chooses 22:00 if the current sky is not already dark. If night-effect intensity is zero, it restores 80% so the requested display is visible. It does not change cloud family, day speed or the time-running setting. Weather stays on Clear; select Auto to resume the weather cycle.

The display fades in over six seconds, arches across the current vista, and fades out over its final twenty seconds. It lasts up to two minutes of unpaused sky time, ending sooner at dawn or if aurora is switched off. A countdown replaces repeat input while active. If a natural display is already visible, its composition and brightness are retained to avoid a jump. Existing sky and pond shaders handle the effect; no new render pass or shader is needed.

Manual occurrences use a separate random generator. The natural 45% nightly decision, automatic scheduling and shooting stars retain their original random sequence. At the manual display's end the sky returns to a quiet gap until the next scheduled natural display, rather than jumping to a different composition.

Verification: existing 400-night regression suite; manual lifetime/fades/gating/repeat-click/invalid-angle/pause tests; 40 paired natural/manual nights; frame-rate agreement; four-file full runtime/editor compilation; bounds and actual-font text-fit checks. These are CPU/code checks, not a rendered Unity test. No camera, microphone, provider session or Play mode was started.

User visual check after Unity imports: select the button during daytime/rain and from an already-clear night; close the menu and look up for the gradual display. Check its pond reflection, countdown, off switch, expiry, and that Auto weather can be resumed. No installer menu is needed.
