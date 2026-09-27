# Varied night sky

- Each dusk has a 45% aurora chance. A night is tracked separately from cloud cover
  and comfort settings, with twilight hysteresis in the sky controller. Storms
  clearing, changing settings and turning the camera never reroll the decision.
- Selected nights have slow 95–155-second displays, 22-second fade-ins and
  28-second fade-outs, with 50–95-second quiet intervals. Quiet nights stay quiet.
- Each display chooses a full-circle world heading, tilt, spread, fold phase and
  green/teal-to-rose/violet color mix. Approximately 70% use high, overhead arcs;
  the remainder are lower over the horizon. Positions only change while hidden.
  Gentle shader drift is world-locked, not attached to the player's head.
- Meteors start after 5–12 clear-night seconds, then every 18–40 seconds, with
  singles, pairs and occasional trios. Brightness is 1.05–1.50 (previously .65–1).
  Three trails remain the hard limit; stalls do not queue catch-up showers.
- The moon is artistically enlarged from .0045 to .0085-radian angular radius,
  with broad dark maria, four filtered craters, limb shading and a subtle halo.
  Its details are fixed in a moon-local frame and only evaluated on moon pixels.
- The base stars use a zenith-centred equal-area projection. Their density stays
  even per solid angle and no longer pinches into radial spokes directly overhead.

Sky and pond share the same shader functions and composition parameters. The
garden's 360-degree horizon is preserved and masks celestial effects below the
mountains. No textures, models, capture cameras, additional lights, bloom or extra
render passes were added. Existing night-effect toggles/intensity are respected.

After Assets > Refresh, the one-time verifier runs CPU scheduling checks and
checks the sky/pond passes sequentially. It refuses low memory and never retries
automatically. Its report is `Weather/NightSky/VarietyCheck.txt`. The menu entry
**Therapy Game > Verify Varied Night Sky** can rerun checks after freeing memory.
It does not enter Play mode, modify the scene, or access the microphone.

Check the final look in a user-controlled clear-night session. Not seeing an
aurora on a particular night is intentional; meteor and moon visibility are
independent of the aurora lottery. Cloud cover still softens/hides sky effects.
