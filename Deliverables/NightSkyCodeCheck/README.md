# Gentle night sky

Adds occasional world-space shooting stars (single, pair, or rare trio) and two slow, folded aurora curtains. Both use the existing sky and pond shader evaluation, behind the photographic clouds. No extra textures, scene renderers, lights, cameras, volumes, capture passes or particles.

The first meteor is scheduled 8–18 real seconds into visible night, followed by 24–62-second event gaps. Groups are staggered; trails last 1.25–1.95 seconds. Aurora shows begin within 2–7 seconds, fade in over 18 seconds and out over 24 seconds, with quiet intervals between shows. These are artistic gameplay timings, independent of the accelerated day cycle. Camera heading only influences an event's initial placement: existing trails/curtains remain fixed in the world.

Esc → Sky & weather contains separate toggles and an overall strength slider. Zero strength disables visible effects. Rain/heavy cloud and daylight fade them out. The existing clock, cloud speed, lighting, music and voice permissions are preserved.

Verification: CheckNightSky.ps1 compiles against the current live Unity assemblies and runs pure scheduling tests. CheckNightShader.cs compiles the exact shared HLSL to DirectX 11 bytecode without creating a device. aurora-cpu-study.png is an approximate CPU shader study, not a live Unity screenshot.

Import with Play stopped via Assets → Refresh. The guarded installer saves an unmodified scene copy first, then enables the two night effects at 80% on the existing sky controller. It writes Assets/TherapyGame/Weather/NightSky/NightSkyCheck.txt. If not automatically applied, use Therapy Game → Install Gentle Night Sky. No Play, bake, reflection capture, microphone or network session is started by verification.

User-controlled visual check: Esc → Sky & weather → Clear, Night; pause the day cycle to observe. Wait about 20 seconds and look across the sky. Check the moving pond reflections, then test rain/day and the comfort toggles. Frame rate on the laptop remains unmeasured until that live check.
