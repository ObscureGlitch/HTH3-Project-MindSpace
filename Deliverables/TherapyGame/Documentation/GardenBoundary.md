# Continuous garden boundary

The rectangular playable garden and fence are unchanged. A single four-material,
5,376-triangle ground apron matches all 224 original terrain edge samples and extends
in every direction (including corners). It has no collision, shadow casting,
reflection probes or runtime behaviour. Its outer edge is at least 285.2 metres
from the fence; the existing player camera stops at 220 metres.

The original mountain/snow batches are disabled, not deleted. Distant mountains,
layered hills and tiny treetops now live in the sky shader. Integer-frequency
profiles wrap continuously through 360 degrees. They respond to the current
day/night and storm colors and mask stars below their silhouette. The pond uses
the same sky sampler, so it also reflects the distant scenery without a capture
camera. Existing clouds, aurora and shooting stars remain unchanged.

## Safe installation

Keep TherapyRoom open and Play stopped. Assets > Refresh loads the one-time
`GardenBoundaryRequest.txt`. The installer refuses low-memory, wrong-scene,
changed-terrain or unsafe-fence states. If it pauses, close unused applications
and choose **Therapy Game > Repair Garden Horizon**. It never repeatedly retries.

Before applying scene changes, it checks the original fence using player-sized
physics sweeps at intervals along every side, diagonal corners, and an elevated
height. It verifies the sky and pond passes sequentially; no automatic cache
clearing, full reimport, bake, Play mode, camera capture or microphone access.

Scene snapshots are saved outside Assets in `TherapyBackups/GardenBoundary`.
The result is recorded in `Assets/TherapyGame/Exterior/Horizon/BoundaryCheck.txt`.
The shader/source deployment also has timestamped file backups outside Assets.

## Manual visual check

In a normal player-controlled session, walk along each fence edge and all four
corners. Look outward and down: meadow should continue beyond the fence without
holes, and the distant skyline should encircle the garden. Walking into the fence
must still stop movement. Check daylight, sunset, night and rain, including the
pond reflection. The editor installer does not render or run the game, so passing
its geometry/physics checks does not replace this appearance/performance check.
