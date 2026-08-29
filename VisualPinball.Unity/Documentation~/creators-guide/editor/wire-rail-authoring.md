---
uid: wire_rail_authoring
title: Wire Rail Authoring
description: Author three-dimensional wire rail routes and per-segment rail layouts with Unity Splines.
---

# Wire Rail Authoring

The **Wire Rail** component provides an early authoring workflow for routing playable wire rails with a native Unity spline. Each spline segment has its own rail count and cross-section offsets, and the component generates both visible wire tubes and a separate ball-channel collider.

> [!warning]
> Wire rail authoring does not yet generate branched junctions, end fittings, or VPX import/export data.

## Create a Wire Rail

1. Select the GameObject that should contain the new rail, or clear the selection to create it at the scene root.
2. Choose **GameObject > Pinball > Wire Rail**. VPE creates and selects a new Wire Rail GameObject under the selected parent with a reset local transform.
3. Click **Edit Spline in Scene View** in the Wire Rail inspector, or click **Edit Wire Rail Spline** in the Scene view panel, to activate the spline editing tools.
4. Drag a circular knot grip to move it. Select a Bézier knot and drag either smaller tangent grip to reshape its curve. Double-click the spline to add a knot, or double-click an existing knot to remove it.

Creating the Wire Rail adds a child named **Wire Rail Spline** with a 500 VPX-unit route along its local positive Y-axis. Keep the generated child's transform unchanged; move or rotate the parent Wire Rail GameObject when the entire assembly needs to be repositioned.

## Units and Orientation

Spline knot positions and rail offsets are stored in VPX units. A standard pinball has a diameter of 50 VPX units. See [Units and 3D Space](xref:units_3d_space) for the relationship between VPX and Unity units.

Rail offsets describe the cross-section around the spline centerline:

- **X** moves a rail laterally to the left or right.
- **Z** moves a rail vertically within the cross-section.
- The spline tangent supplies the longitudinal direction, which is positive Y on the default route.

The offset frame follows the spline through all three dimensions. Knot rotation controls how the cross-section twists around the route, so banking or inverting a rail is handled with Unity's spline rotation controls rather than by changing the meaning of X and Z.

## Edit Segment Layouts

The inspector displays one layout for every spline segment. An open spline with _n_ knots has _n − 1_ segments; a closed spline has _n_ segments.

For each segment, you can:

- Change **Rail Count** with the number field or the **−** and **+** buttons.
- Click a wire in the **Wires** cross-section to select it.
- Hold **Shift** while clicking to add wires to the selection, or hold **Ctrl** on Windows and Linux or **Cmd** on macOS to toggle individual wires.
- Drag any selected wire in the cross-section to move all selected wires in X and Z together.
- Type values into **X Position**, **Z Position**, and **Diameter**, or drag each field's label to scrub the value. A changed value is applied to every selected wire, while unchanged mixed values stay independent.
- Use **All** and **None** to select or clear all wires in the segment.
- Choose **Left** or **Right** for the raised third rail when the count is three.
- Click **Reset Layout** to restore the recommended offsets for the current rail count.

Changing **Rail Count** reapplies the complete recommended layout for that segment, replacing any custom offsets and diameters on it. Set the count before fine-tuning individual wires. **New Wire Diameter** in the Render Geometry section supplies the diameter for newly created or reset layouts.

When a knot splits a segment, the new segment receives an independent copy of the original layout. Removing a knot removes the corresponding segment layout while preserving its neighbors.

## Keep Wires Continuous Between Segments

The inspector displays a **Connection** panel between every neighboring pair of segment layouts. A closed spline also displays a connection from its last segment back to its first segment.

Each matching wire index has its own row and is **Continuous** by default. Clear **Continuous** when that wire should intentionally stop, restart, or jump at the knot. Only indices present on both segments are listed. A continuous wire does not end and restart when its position or diameter differs between the two layouts; its rendered tube, Scene view centerline, and ball-channel collider blend through the adjoining segments instead. The rendered path also shares one tangent across the knot, preventing a crease when one layout uses an extreme offset.

Each continuous wire has its own **Weight**. The weight chooses that wire's shared cross-section at the knot:

- **0** places the junction entirely at the first segment's wire position. The next segment performs the full transition.
- **0.5** places the junction halfway between both segment positions. Both segments perform half of the transition.
- **1** places the junction entirely at the next segment's wire position. The first segment performs the full transition.

The **Transition Curve** controls the interpolation shape across both adjoining spline segments. Its horizontal axis runs from the outer end of a segment toward the shared knot and its vertical axis is the amount of the transition applied. The default linear curve moves the wire at a constant rate; reshape it to ease the wire into or out of the new position. Curve endpoints are always treated as 0 and 1 so both generated wire tubes and collider profiles remain joined at the knot.

## Default Rail Positions

New Wire Rail components start with four rails. The recommended layouts use an 8 VPX-unit reference wire diameter and are arranged around a 50 VPX-unit ball.

| Rail count | Default centerline offsets (X, Z) | Layout |
| --- | --- | --- |
| 1 | `(0, 0)` | Bottom center |
| 2 | `(-19, 0)`, `(19, 0)` | Bottom left and right |
| 3 | Two bottom rails plus `(-19, 44)` or `(19, 44)` | Raised third rail on the selected side |
| 4 | `(-19, 0)`, `(19, 0)`, `(-19, 44)`, `(19, 44)` | Bottom and middle rails on both sides |
| 5 | Four-rail layout plus `(0, 52)` | Top center added |
| 6 or more | Four-rail layout plus rails distributed from X `-19` to `19` at Z `52` | Additional top rails distributed symmetrically |

These positions are practical starting points rather than constraints. Adjust them for different wire diameters, wider clearances, decorative guides, or transitions into other mechanisms.

## Render Geometry

Every authored rail is swept along the spline as a visible tube. New wires have an 8 VPX-unit diameter by default and an octagonal cross-section. Select one or more wires in a segment's cross-section to edit their individual diameters. Use **Tube Sides** to increase or decrease radial detail and **Minimum Samples Per Segment** to set the baseline longitudinal detail. The generator inserts additional rings automatically wherever the actual offset wire turns by more than five degrees, so smooth bends do not collapse into one skewed end ring.

Assign **Material** to control the wire's appearance. When no material is assigned, VPE uses the active render pipeline's default material.

Render tubes are capped where a rail begins or ends. Matching or explicitly continuous rails on neighboring segments remain open internally so their surfaces meet without an unnecessary cap.

## Ball Channel Collider

The collider is one swept channel around the space occupied by the reference ball. It deliberately does not create a physics tube around every visible wire. Instead, its cross-section uses up to eight flat facets fitted to the contact directions between the rails and the reference ball.

The generated channel opens or closes according to the segment's rail layout. Its facets are refitted when individual wire positions or diameters change:

- One rail provides a narrow bottom contact surface.
- Two bottom rails provide a faceted floor with the top open.
- Three rails add a wall on the side selected by **Third Rail**.
- Four rails provide floor and left/right walls while keeping the top open.
- Five or more rails add upper support and close the cross-section around the ball.

The collider is registered with VPE's pinball physics engine rather than Unity's built-in physics. Configure **Ball Diameter**, collider sampling, physics material, elasticity, friction, and scatter in the Wire Rail inspector.

> [!note]
> The collider facets model the continuous channel that constrains the ball, not the exact round surface of each wire. This keeps the collision topology small and prevents seams between several independent wire colliders.

## Scene View Preview

Select either the Wire Rail GameObject or its spline child to display the authored rail centerlines in the Scene view. Each rail uses a different preview color, and every segment is labeled with its segment number and rail count. A thick, high-contrast center spine marks the editable spline route over the generated geometry. The generated render tubes remain visible normally in the Scene and Game views.

Selecting the Wire Rail also displays an **Edit Wire Rail Spline** button in the Scene view. While spline editing is active, knots appear as outlined circular grips; the active knot is gold, and editable Bézier tangents appear as smaller cyan grips joined to it by thick arms. A grip becomes active on the same mouse-down that starts its drag, so it does not need to be selected before moving it. Hover a grip for its action hint. The panel also shows the double-click shortcuts: double-click the route line to insert a knot at that position, or double-click a knot to remove it. An open spline keeps at least two knots, and a closed spline keeps at least three.

Enable **Show Collider Preview** to draw the generated ball-channel mesh as a yellow wireframe. The colored centerlines and center spine are authoring guides; their colors and line widths do not represent the render material or physical wire thickness. The wire circles in the Inspector cross-section do use their authored diameters.

## Current Limitations

- Only the first spline in the generated Spline Container is used.
- Connection blending matches wires by their one-based index; it does not remap a wire to a different index on the next segment.
- Extreme cross-section changes can alter the ball-channel facet topology and cannot currently be blended as one collider.
- Junctions and end fittings are not generated.
- Wire rail data is not yet imported from or exported to VPX.
