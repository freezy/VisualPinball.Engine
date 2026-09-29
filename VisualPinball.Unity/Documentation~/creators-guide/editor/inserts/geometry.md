---
uid: insert_geometry
title: Insert Geometry
description: Every outline, corner, body, channel, bevel, and hole parameter in the insert generator.
---

# Insert Geometry

The **Geometry** section builds the insert's outline and the body below it. Start with the dimensions that fit the playfield opening, then shape the corners and recessed floor. Patterns use that same floor and are covered on the [Patterns](patterns.md) page.

## Dimensions and coordinates

Length fields in the mesh generator use **VPX units**, even where the Inspector simply says Width or Depth. One unit is **0.53975 mm**; an inch is about **47.059 units**. Angles are in degrees and segment counts are whole numbers. The preview also reports the finished width and height in millimeters. See [Units and 3D Space](../units-3d-space.md).

**Width** and **Height** describe the finished rounded outline, rather than an imaginary sharp outline that gets smaller when the corners are rounded. **Shape** selects which outline controls are visible.

Controls unique to a shape remain stored when you switch. Width and Height are shared by Triangle, Rectangle, Diamond, Bullet, and Arrow; Diameter is shared by Circle and Banana.

A new insert starts as a Triangle. Defaults below describe a newly added component; library inserts can start with different settings. Corner-group defaults list Radius / Segments.

> [!NOTE]
> **Generator coordinates.** The outline is centered around the insert's origin and converted into a mesh in Unity's local XZ plane. The rim sits at local Y = 0 and the body extends below it. Hole X/Y and pattern Center use the generator's two-dimensional coordinates; lamp Position uses Unity-local X/Y/Z in millimeters. The preview and Scene handles show the actual result, so these two coordinate systems do not need to be converted by hand.

## Rounded corners and sampling

Every corner group has the same two controls:

| Parameter | Meaning |
| --- | --- |
| **Radius** | Radius of the rounded corner. Zero gives a sharp corner. A larger radius consumes more of the straight sides on either side. VPE reduces it if it cannot fit and shows a warning. |
| **Segments** | Number of straight segments approximating that arc, from 1 to 64. One gives a chamfer; more make it rounder. Each arc has one more vertex than segments. |

**Side Segments** applies to the whole outline: it subdivides each straight run, from 1 to 32, with a default of 1. Raise it when you need more even spacing beside finely sampled corners. It is available on polygonal shapes; circles and bananas have their own sampling controls. Scene view colors sharp corner dots red, arc samples yellow, and side subdivisions grey.

Radius changes the shape. Segments changes how closely the mesh follows that shape. A higher segment count also adds walls and bake work, so use the lowest count that looks smooth from the intended camera.

<!-- Screenshot: geometry-corners. Radius 0 versus 4; separately Segments 1 versus 8. Outline overlay and oblique wireframe. Repeat for the groups listed in visuals.md. -->

## Triangle

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Width** | 47.059 (1 inch) | Finished width across the base. |
| **Height** | 94.118 (2 inches) | Finished distance from the base to the apex. |
| **Base Corners** | 4 / 8 | Shared Radius and Segments for the two ends of the base. |
| **Apex Corner** | 4 / 8 | Radius and Segments of the opposing tip. |

Base and apex rounding can be tuned independently. The generator adjusts the underlying triangle so the requested finished dimensions are retained.

<!-- Screenshot: geometry-triangle. Top view; vary Width, Height, and each corner group individually. -->

## Rectangle

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Width** | 47.059 (1 inch) | Finished horizontal extent. |
| **Height** | 94.118 (2 inches) | Finished vertical extent in the outline preview. |
| **Corners** | 4 / 8 | Shared Radius and Segments for all four corners. |

<!-- Screenshot: geometry-rectangle. Square and elongated rectangle; individual width, height, and rounding comparisons. -->

## Circle

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Diameter** | 47.059 (1 inch) | Diameter of the circular outline. |
| **Vertices** | 32 | Number of sides approximating the circle, from 6 to 128. |

A circle has no separate corner or Side Segments controls. **Vertices** determines its entire outline sampling.

<!-- Screenshot: geometry-circle. Diameter 47.059 versus 70.588; Vertices 8 versus 32 with wireframe. -->

## Banana

The banana is a band bent around a circle, with rounded ends. Its Diameter describes the circle through the middle of the band, rather than the band's outer diameter.

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Diameter** | 47.059 (1 inch) | Diameter of the circle followed by the band's centerline. |
| **Thickness** | 23.529 (0.5 inch) | Width across the band. Half extends on either side of that centerline. |
| **Angle** | 90° | Sweep around the circle, from 1° to 180°. The ends turn upward in the preview. |
| **Arc Vertices** | 24 | Segment count along each of the two arcs, from 2 to 128. |
| **End Vertices** | 8 | Segment count around each rounded end, from 2 to 32. |

Thickness limits how wide its channel can be. A starburst centered on the banana's arc can create ribs crossing the band evenly; see [Starburst](patterns.md#starburst).

<!-- Diagram: geometry-banana-measures. Centerline diameter, band thickness, angle, and arc center labeled. -->

## Diamond

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Width** | 47.059 (1 inch) | Finished extent across the side points. |
| **Height** | 94.118 (2 inches) | Finished extent between the top and bottom. |
| **Side Points** | 2 / 8 | Shared Radius and Segments for the left and right points. |
| **Top and Bottom** | 4 / 8 | Shared Radius and Segments for the upper and lower points. |
| **Shear** | 0° | Lean of the top and bottom points away from upright, from −75° to 75°. Positive leans toward the table's right in the preview. Zero is upright. |

<!-- Screenshot: geometry-diamond. Shear 0 versus 25, plus separate corner comparisons. -->

## Flash

The flash has two offset arms, flat ends, projecting spikes, and inward notches. These controls let you fit a lightning-bolt insert without drawing its polygon manually.

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Width** | 50 | Finished extent from spike to spike. |
| **Height** | 125 | Finished extent from end to end. |
| **Tip Width** | 10 | Width of each flat end. |
| **Notch Depth** | 16 | Distance inward from a spike to its notch. It must be less than half the width. |
| **Step Height** | −8 | Placement of the two steps above and below the middle. Zero aligns them with the middle line. Negative values increase the overlap of the arms. |
| **Tip Corners** | 5.2 / 4 | Radius and Segments for the four corners of the flat ends. |
| **Spike Corners** | 5 / 4 | Radius and Segments for the two projecting spikes. |
| **Notch Corners** | 3 / 3 | Radius and Segments for the two inward turns. These fillets round inward. |

The [Engraved Line](patterns.md#engraved-line) is a pattern on the flash's floor. Its inset is measured from the floor edge, and does not change these outline controls.

<!-- Diagram: geometry-flash-measures. Tip Width, Notch Depth, Step Height, and all three corner groups labeled. -->

## Bullet

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Width** | 47.059 (1 inch) | Finished width of the bullet. |
| **Height** | 94.118 (2 inches) | Finished height from base to tip. |
| **Tip → Radius** | 5 | Radius of a circular tip touching the top. Zero makes the tip pointed. It is limited to fit inside the bullet's width and height. |
| **Tip → Segments** | 8 | Segments across the whole tip arc. The generator rounds the count up to an even number. |
| **Shoulder Height** | 20 | Height from the base at which the straight sides turn toward the tip. Zero starts the sloping flanks at the base. The shoulders remain below the tip's center. |
| **Shoulder Corners** | 2 / 4 | Radius and Segments where the straight sides meet those flanks. |
| **Base Corners** | 3 / 8 | Radius and Segments at the two base corners. |

<!-- Diagram: geometry-bullet-measures. Circular tip radius and shoulder height labeled; show pointed and rounded tip. -->

## Arrow

The arrow can have a flat base, a narrow notch with feet on either side, or a notch spanning the entire base for a chevron.

| Parameter | Default | Meaning |
| --- | --- | --- |
| **Width** | 47.059 (1 inch) | Finished width across the base. |
| **Height** | 94.118 (2 inches) | Finished height from base to tip. |
| **Tip** | 4 / 8 | Radius and Segments of the upper point. |
| **Notch Depth** | 10 | How far the notch cuts into the base. Zero gives a flat base and disables the notch-specific controls. |
| **Notch Width** | 14 | Width of the notch opening. At the full base width there are no feet, so Foot Angle and Inner Corners are disabled. |
| **Foot Angle** | 0° | Tilt of the feet, from −85° to 85°. Positive lifts their inner corners; negative drops them below the outer corners. |
| **Notch Corner** | 4 / 8 | Radius and Segments at the inward notch apex. A large radius makes a round scoop. |
| **Inner Corners** | 2 / 4 | Radius and Segments where each foot turns into the notch. |
| **Feet** | 2.5 / 8 | Radius and Segments at the two outer base corners. |

<!-- Diagram: geometry-arrow-measures. Flat base, notched arrow, and full-width chevron, with the four corner groups labeled. -->

## Body, channel, and bottom bevel

**Depth** sinks the main floor below the rim. A channel surrounds that floor and sits lower still, leaving the main floor as a raised middle. This distinction matters when placing holes and limiting pattern height.

| Section / parameter | Meaning |
| --- | --- |
| **Body → Depth** | Distance from the rim to the main floor. The default is 16. With a channel, the deepest floor is Body Depth plus Channel Depth. |
| **Channel** | Enables the surrounding recessed channel, on by default. Without it, the main floor reaches the outside wall. |
| **Channel → Width** | Distance inward from the outline to the raised middle. Zero gives no channel even when the toggle is enabled. The default is 3. It is reduced if the inset would fold or consume the shape. |
| **Channel → Depth** | Additional distance below the main floor. The default is 4. The channel's width still matters when its depth is zero. |
| **Bottom Bevel → Width** | Rounding where the outer wall meets the channel floor, extending up the wall and inward along the floor. Zero leaves a sharp edge. The default is 1. It is limited to fit the wall and channel. |
| **Bottom Bevel → Segments** | Segments approximating that rounding, from 1 to 16. One gives a flat chamfer. The default is 4. |

Bottom Bevel is actual geometry and requires a channel. The separate **Edge Bevel** under Normal Bake rounds sharp edges in the surface normal map; it does not replace this body bevel.

<!-- Diagram: geometry-body-cutaway. Dimension Body Depth, Channel Width/Depth, and Bottom Bevel Width. Pair channel enabled/disabled and bevel 0/1. -->

## Holes

Holes cut through the raised middle to the channel floor. They require a channel with a nonzero width. They describe molded detail in the insert, rather than a new physical ball hole in the playfield.

| Parameter | Meaning |
| --- | --- |
| **Holes** | Editable list of holes. Each numbered row corresponds to the numbered hole in the preview and Scene view. |
| **X, Y** | Hole center relative to the insert's origin, in generator coordinates and VPX units. |
| **R** | Radius of that hole. Each hole can have a different radius. |
| **Circle Segments** | Side count for every hole, from 3 to 64. The default is 16. |
| **New Hole Radius** | Radius assigned when adding a hole or using a placement preset. The default is 2. Changing it does not resize existing holes. |

Drag the circular Scene handle to move a hole across the insert. Drag the small dot on its rim to change its radius. The Inspector's X, Y, and R fields provide exact placement and size.

### Placement helpers

| Action | Result |
| --- | --- |
| **Corners** | Replaces the list with a hole at each corner of the raised floor. |
| **Corners + Sides** | Replaces the list with corner holes and one halfway along each side. |
| **Around** | Replaces the list with evenly spaced holes along the floor edge. Choose 1–64 in the adjacent count field before pressing the button. |
| **Add Hole** | Adds one hole at an interior starting point, using New Hole Radius. Move it afterward. |
| **Symmetrize** | Snaps nearly centered holes onto the middle line and makes nearly mirrored pairs exact. “Nearly” means within 5% of the outline's larger dimension. |
| **Clear** | Removes all holes. |

Corners and Corners + Sides apply to polygonal shapes. Circle and Banana use their sampled curves instead; use Around or individual placement. The placement helpers are available when editing a single insert.

<!-- Screenshot: geometry-holes. A numbered Inspector row and its Scene handle; compare center, radius, and circle sampling. -->

## When a value cannot fit

The Inspector reports reduced radii, channel width, bevel width, or pattern height beside the relevant controls. Increasing a value after it has reached the shape's limit may no longer change the mesh. Make more room in the outline, reduce the neighboring feature, or use the smaller value.

If a rebuild fails completely, VPE retains the last mesh that worked and displays the error. Correct the invalid parameters before baking. Generated meshes belong to the component; hand-editing them is not a lasting customization because a rebuild recreates them.
