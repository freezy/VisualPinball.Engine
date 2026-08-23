---
uid: rolling-resistance-calibration
title: Calibrating Rolling Resistance
description: How to measure, fit, and validate rolling resistance for a pinball playfield.
---

# Calibrating Rolling Resistance

Rolling resistance controls energy loss while a ball is in sustained, near-no-slip contact with a surface. It is represented by the dimensionless coefficient `Crr`. It is separate from **Friction**, which controls sliding, grip, and tangential collision impulses.

> [!IMPORTANT]
> There is no recommended production preset yet. Existing packages, VPX imports, and new templates must use `Crr = 0` until a physical-table data set has been measured and validated. Do not choose a default from generic steel-on-steel coefficient tables.

## Where the value applies

A nonzero rolling-resistance value can be authored on a physics material asset or on the physics-material override for playfields, ramps, surfaces, primitives, rubbers, metal wire guides, hit targets, and drop targets.

The first implementation deliberately does not apply rolling resistance to:

- collision impacts;
- ball-to-ball contacts;
- the specialized flipper contact solver;
- spin about the contact normal, also called drilling or pivot friction; or
- a stationary ball on a shallow incline.

The model uses one constant coefficient. It does not vary with speed, load, temperature, or wear. Measurements may justify a speed-dependent follow-up, but that should not be assumed in advance.

Kinematic surface velocity is included when deciding whether the ball is rolling and which direction to damp. The v1 support load does not include kinematic surface acceleration, so the analytic guarantee applies to static surfaces and constant-velocity kinematic surfaces only.

## Mechanical reference

For the solid sphere used by VPE, ideal level rolling with a constant coefficient has deceleration:

```text
a_rr = 5/7 Crr g
```

For a ball rolling downhill on an incline of angle `theta`:

```text
a = 5/7 g (sin(theta) - Crr cos(theta))
```

The solver clamps at zero rolling speed and does not reverse the ball. It does not model the set-valued static state that could hold a motionless ball when `Crr >= tan(theta)`.

## Build a measurement fixture

Use a clean, standard pinball ball and a representative playfield sample. The sample should include the finish and preparation state that the table is expected to use, such as clear coat, wax, paint, Mylar, inserts, or representative wear.

Record these conditions before every measurement session:

- ball diameter and mass;
- ball condition and cleaning method;
- surface material, finish, and preparation state;
- surface angle and how it was measured;
- starting speed or release height;
- position versus time, preferably from high-frame-rate video or photogates;
- temperature; and
- whether the surface is waxed.

Keep the release repeatable and avoid imparting side spin. Measure enough of the trajectory to distinguish sustained rolling from the initial sliding-to-rolling transition. Discard a trial if the ball touches a guide, crosses a seam, or visibly slips during the fitted interval.

## Level-coast method

Measure the speed `v0` at the start of a confirmed rolling interval and the distance `d` from that point to rest. Use consistent units for distance and speed:

```text
Crr = 7 v0^2 / (10 g d)
```

Do not estimate `v0` only from the release height unless the ball is already rolling without slip at the start of the measured interval. A photogate pair or a fitted position-versus-time curve is preferred.

## Incline method

Measure the downhill acceleration `a` on a surface inclined by `theta`:

```text
Crr = (sin(theta) - 7 a / (5 g)) / cos(theta)
```

Choose an angle and starting speed that keep the ball moving downhill and rolling without slip throughout the fitted interval. Measure the actual surface angle rather than relying on a nominal cabinet setting.

## Data sheet

Keep raw observations as well as the derived coefficient. One row per trial is sufficient:

| Field | Value |
| --- | --- |
| Trial ID and timestamp | |
| Ball diameter and mass | |
| Ball preparation | |
| Surface and finish | |
| Waxed or unwaxed | |
| Temperature | |
| Surface angle | |
| Release method or height | |
| Starting speed `v0` | |
| Coast distance `d` | |
| Fitted acceleration `a` | |
| Derived `Crr` | |
| Video or photogate source | |
| Exclusions or observations | |

Preserve the position-versus-time samples or source video. A derived coefficient without the raw trajectory is not sufficient calibration evidence.

## Fit and validate a constant

1. Measure several starting speeds across the gameplay-relevant range.
2. Repeat every condition enough times to quantify trial-to-trial variation.
3. Fit one constant `Crr` to the retained trials and report the residuals.
4. Validate the fitted value on a second representative surface or preparation state.
5. Reproduce the fixture in VPE and compare position-versus-time or coast distance, not only the final stop time.
6. Record the Unity version, render-pipeline project, physics step settings, ball data, collider type, and material values used for the simulation.

If residuals show a repeatable speed dependence large enough to affect gameplay, keep the constant default at zero and propose a separate velocity-dependent model with tests and migration rules.

## Acceptance before recommending a preset

A proposed nonzero preset should include:

- the completed data sheet and raw trajectories;
- the fitted value and uncertainty or observed spread;
- results for more than one starting speed;
- validation on a second representative surface;
- a comparison between physical and simulated trajectories;
- an end-to-end table smoke in `VisualPinball.Unity.Project.Hdrp` using Unity 6000.5 or newer; and
- the known limitations listed on this page.

The headless core test project does not contain a render-pipeline implementation, so its table import/export tests cannot substitute for the HDRP smoke. The smoke should include real transformed and non-transformed colliders and confirm that mesh seams do not multiply rolling loss.

## Rollout policy

- Existing `.vpe` packages keep `Crr = 0` when the field is absent.
- Imported VPX physics materials and overrides start at `Crr = 0`.
- Existing tables do not change unless an author opts in.
- New templates stay at `Crr = 0` until the acceptance evidence above is published.
- A future recommended value must be presented as a measured preset for a documented surface, not as a universal material constant.

When tuning a table before a measured preset exists, keep rolling resistance at zero. Do not compensate by changing Friction: that also changes sliding, rubber behavior, and oblique impacts.
