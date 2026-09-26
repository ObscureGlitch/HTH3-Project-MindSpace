# Choose a seat

Walk close to a chair or sofa cushion and aim at it. The on-screen prompt names the selected spot. Press **E** to sit there.

Five spots are available: the two sofa cushions, the two conversation chairs, and the window reflection chair. Aim at the left or right half of the sofa to choose that cushion (left/right as seen facing the sofa).

While seated, the camera lowers to seated eye height. Mouse look remains active; walking is disabled. Press **Space** to stand. **E** also stands up unless you are aiming at a nearby notice-able object, in which case E retains its usual interaction. Stand up before choosing a different seat. **Esc** releases the cursor; **Enter** resumes without changing your seat.

A short fade covers each posture change. Standing first returns you to the clear place from which you approached the seat. If that becomes obstructed, the game checks nearby exit positions and refuses to place you inside furniture or over missing floor.

Seat labels, eye height, facing and fallback standing positions are editable on each furniture object's **Wellness Seat** component. The original furniture colliders, lighting and seven reflective interactions are retained. The window chair still displays its reflection when you sit.

`Therapy Game > Verify Seats in Play Mode` runs the in-editor smoke checks and returns to Edit mode. Results are saved in `Documentation/SeatingCheck.txt`. The pre-seating live scene snapshot is under `Seating/Backups`.

Verified in the current Unity project: real E input selected each of the five spots, seated height and free mouse look worked, WASD could not move the seated player, Esc/Enter preserved the seated state, Space/E stood up safely, and walking resumed. Invalid/repeated sit requests and completely obstructed exits were rejected. All original interaction targets remain present. No standalone build was run.

The final source also passes Unity's C# compiler. The only compiler warning is the pre-existing deprecated CPU lightmapper setting in the realism-upgrade utility. The one-shot test request is marked complete, so returning to Unity will not automatically start another Play-mode test.
