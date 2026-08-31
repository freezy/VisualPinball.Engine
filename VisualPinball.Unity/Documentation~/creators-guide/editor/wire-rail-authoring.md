---
uid: wire_rail_authoring
title: Wire Rail Authoring
description: Author three-dimensional wire rail routes, distance-based wire layouts, and fixtures with Unity Splines.
---

# Wire Rail Authoring

The **Wire Rail** component provides an early authoring workflow for routing playable wire rails with a native Unity spline. A component-wide rail count defines the available wires, independently positioned wire layouts enable or disable those wires and control their cross-section offsets along the route, and the component generates both visible wire tubes and a separate ball-channel collider. The inspector's **Render Geometry**, **Ball Channel Collider**, **Wire Layouts**, and **Fixtures** headers can each be collapsed.

> [!warning]
> Wire rail authoring does not yet generate branched junctions, end fittings, or VPX import/export data.

## Create a Wire Rail

1. Select the GameObject that should contain the new rail, or clear the selection to create it at the scene root.
2. Choose **GameObject > Pinball > Wire Rail**. VPE creates and selects a new Wire Rail GameObject under the selected parent with a reset local transform.
3. Click **Edit Spline in Scene View** in the Wire Rail inspector, or click **Edit Wire Rail Spline** in the Scene view panel, to activate the spline editing tools.
4. Drag a circular knot grip to move it freely, or click it to show the standard position gizmo and constrain movement to an axis or plane. Select a Bézier knot and drag either smaller tangent grip to reshape its curve. Double-click the spline to add a knot, or double-click an existing knot to remove it.

Creating the Wire Rail adds a child named **Wire Rail Spline** with a 500 VPX-unit route along its local positive Y-axis. Keep the generated child's transform unchanged; move or rotate the parent Wire Rail GameObject when the entire assembly needs to be repositioned.

Click **Center Pivot** to move the Wire Rail GameObject pivot to the point halfway along the spline's traveled distance. The spline knots are translated inversely, so the authored rail, fixtures, collider, and layout positions do not move in world space. The operation supports Undo.

For the usual top-view workflow, first draw the complete horizontal route, then set the Z height of its first and last knots. The grading action is available both in the Wire Rail inspector and in the **Editing Wire Rail Spline** panel in the Scene view. With no knots selected, click **Grade Heights First → Last** to interpolate every intermediate knot height by cumulative Bézier distance in the horizontal XY plane rather than by knot index. Select exactly two knots to change the action to **Grade Heights Between Selected Knots** and limit the operation to that interval; selecting one or more than two knots disables the button. The same constant grade is applied to the affected Bézier handles without moving any handle in XY, geometry outside a selected interval remains unchanged, and the operation supports Undo. Auto Smooth knots inside the interval become Continuous knots so their plan-view control points remain fixed. A coupled Auto Smooth, Continuous, or Mirrored tangent at a selected interval boundary becomes Broken when necessary to leave its outside handle untouched. The action is available only for open splines with at least two knots.

## Units and Orientation

Spline knot positions and rail offsets are stored in VPX units. A standard pinball has a diameter of 50 VPX units. See [Units and 3D Space](xref:units_3d_space) for the relationship between VPX and Unity units.

Rail offsets describe the cross-section around the spline centerline:

- **X** moves a rail laterally to the left or right.
- **Z** moves a rail vertically within the cross-section.
- The spline tangent supplies the longitudinal direction, which is positive Y on the default route.

The offset frame follows the spline through all three dimensions. Knot rotation controls how the cross-section twists around the route, so banking or inverting a rail is handled with Unity's spline rotation controls rather than by changing the meaning of X and Z.

## Edit Wire Layouts

Wire layouts are positioned by absolute distance along the complete spline and are independent from spline knots. Editing the route does not add or remove layouts, and adding a layout does not alter the route. A new Wire Rail starts with one layout at 0 VPX that applies to the complete route.

Click **Add Wire Layout** to copy the active layout into a new independently editable layout halfway along the route. Use **Position** to move every layout after the first; the first stays at 0 VPX so the route always has a starting definition. Drag a complete layout panel by its handle to reorder it, including its outgoing transition settings. Use the trash icon to remove a layout while retaining at least one.

For each layout, you can:

- Click a wire in the cross-section to select it.
- Hold **Shift** while clicking to add wires to the selection, or hold **Ctrl** on Windows and Linux or **Cmd** on macOS to toggle individual wires.
- Use the checkbox before **Position** to enable or disable every selected wire for the span that starts at this layout. Disabled wires remain editable and appear gray in the preview.
- Drag any selected wire in the cross-section to move all selected wires in X and Z together.
- Type values into **X** and **Z**, or drag either field's label to scrub the value. A changed value is applied to every selected wire, while unchanged mixed values stay independent.
- Click **Apply to All** to copy the selected wires' X and Z positions from the current layout to every layout. Wire activation and transition settings are not changed.
- Use **All** and **None** to select or clear all wires in the layout.
- Choose **Left** or **Right** for the raised third rail when the component has three rails.
- Click **Reset** to restore the recommended offsets for the current rail count.

Set **Rails** in Render Geometry before fine-tuning individual wires. The count applies to every layout, retains custom offsets for existing wires, gives newly added wires their recommended positions, and enables new wires in every layout. Layout activation remains independent. The global **Wire Diameter** in Render Geometry applies to every rail and fixture.

Existing scenes created before distance-based layouts are migrated by placing their old per-segment layouts at the equivalent spline-curve start distances. After migration, layout positions are independent from knots.

## Keep Wires Continuous Between Layouts

Each layout panel includes its outgoing **Transition to Layout N** controls below the cross-section. A closed spline also displays the transition from its last layout back to its first layout at the spline seam.

Wires use a continuous linear transition by default and therefore do not need individual rows. Use the numbered **Override Wires** buttons to expose only the wires that need custom behavior. Each exposed wire has one compact row containing its **Continuous** checkbox and transition curve. Clear **Continuous** when that wire should intentionally stop, restart, or jump at the next layout position. Turning off a numbered override resets that wire to the continuous linear default and hides its row. A transition button is available only when that wire is active in both layouts. Existing authored non-continuous or non-linear transitions are automatically retained as overrides.

A continuous wire starts at the current layout's exact position and diameter, then transitions to the next layout's exact values over the physical distance between their positions. Its rendered tube, Scene view centerline, and ball-channel collider therefore stay joined. The rendered path also shares one tangent across the layout boundary, preventing a crease when one layout uses an extreme offset.

An inactive wire ends at the next layout boundary, or starts there when it becomes active. It is never treated as continuous across an inactive layout: its stored inactive position does not blend into or otherwise change the position authored for a later active span.

The **Transition Curve** controls the interpolation shape over that single layout-to-layout span. Its horizontal axis runs from the current layout position to the next and its vertical axis is the amount of the transition applied. The default linear curve moves the wire at a constant rate; reshape it to ease the wire into or out of the new position. Curve endpoints are always treated as 0 and 1 so both generated wire tubes and collider profiles reach the exact authored layouts. Move either layout's **Position** to change where the transition begins, where it ends, and how much route distance it occupies.

## Default Rail Positions

New Wire Rail components start with four rails. Choose the component-wide **Rails** value from 1–6 in Render Geometry. The recommended layouts use an 8 VPX-unit reference wire diameter and are arranged around a 50 VPX-unit ball.

| Rail count | Default centerline offsets (X, Z) | Layout |
| --- | --- | --- |
| 1 | `(0, 0)` | Bottom center |
| 2 | `(-15, 0)`, `(15, 0)` | Bottom left and right |
| 3 | Two bottom rails plus `(-30, 30)` or `(30, 30)` | Raised third rail on the selected side |
| 4 | `(-15, 0)`, `(15, 0)`, `(-30, 30)`, `(30, 30)` | Bottom and middle rails on both sides |
| 5 | Four-rail layout plus `(0, 60)` | Top center added |
| 6 | Four-rail layout plus `(-15, 60)`, `(15, 60)` | Two top rails distributed symmetrically |

These positions are practical starting points rather than constraints. Adjust them for different wire diameters, wider clearances, decorative guides, or transitions into other mechanisms.

## Add Fixtures

Fixtures are repeated structural elements positioned along the complete spline rather than owned by an individual segment. A **Brace** is a wire ring that surrounds and holds the authored rails together. A **Cross Wire** is a straight wire joining the first two rails, which are the two bottom rails in the default layouts.

Click **Add Brace** in the **Fixtures** section, then use **Position** to move it anywhere along the route by absolute spline distance. A new brace starts halfway along the spline, and you can add as many independently positioned braces as needed. Drag a complete brace panel by its handle to reorder the authored fixture list without changing any fixture's route position. The brace automatically encloses the wire cross-section at its position and remains perpendicular to the spline tangent. Its preview shows the evaluated brace shape, offset, cutout, and straight-line replacement.

Use the compact **Offset** row to move the complete brace along the local cross-section X and Z axes, and use **Reset** to return both offsets to zero. **Scale** multiplies the automatically fitted brace radius, with `1` preserving the default fit. **Ring Density** sets how many longitudinal tube rings make up a complete brace; partial and cutout braces use the proportional number. Offset values use VPX units. Brace thickness uses the global **Wire Diameter** from Render Geometry.

Enable **Cutout** to reveal its **From/To** range control and remove that part of the ring. Angles use the brace cross-section: 0° points right along positive X and 90° points up along positive Z. Cutout ends are capped.

Enable **Straight Line** to reveal its **From/To** range control and replace that part of the circular brace with the straight chord between the two angles. A brace can use a cutout and a straight line at the same time.

Click the horizon icon at the end of either angle row to rotate that range until both endpoints have the same vertical height while preserving its angular span. Click **Apply to All** to copy scale, ring density, offsets, cutout, and straight-line settings to every other brace without changing any brace's route position.

Click **Add Cross Wire** to place a straight connector halfway along the route. **Position** and **Offset** work like their brace equivalents. **Angle** rotates the connector around the center of the complete active-rail envelope at that position: 0° runs horizontally along local X, 90° runs vertically along local Z, and the remaining values continue around the cross section. **Length** is a signed VPX adjustment to the span between the inward-facing surfaces of the two bottom rails: `0` reaches both rails, positive values extend the connector equally at both ends, and negative values shorten it. The preview renders the connected rails in gray and the cross wire in orange. If either bottom rail is inactive at the fixture position, the cross wire is not rendered.

Click **Add Leg & Foot** to add a linked playfield support halfway along the route. Its attachment wire spans the two bottom rails. Under **Rail Attachment**, use **Offset X/Z** to translate that segment in the route-local cross section and **Length** to apply the same signed span adjustment as a cross wire: `0` reaches the inward-facing rail surfaces, positive values extend both ends, and negative values shorten them. **Side** selects which adjusted attachment end begins the leg, producing the short and long portions of the usual L-shaped support. **Start Vector** is a route-local XYZ direction and **Start Length** is the distance followed along that direction before the leg bends and aims at the foot. A default vector of `(0, 0, -1)` sends the leg vertically down; use the Y component to continue along the route or combine axes for an angled support.

The first foot type is a U-shaped hook. Its **Position** translates the foot pivot in route-local XYZ relative to the selected leg attachment, and **Rotation** applies a full XYZ Euler rotation in the same route-local frame. This allows the hook to lie flat on a horizontal playfield, stand against a vertical surface, or use an arbitrary mounting angle. **Width** controls the hook's centerline span, **Arm Length** controls the free straight side, and **Connected Arm Length** independently controls the distance from the bend to the point where the leg's final connection segment joins the hook. The generated leg always joins that authored point after either transform changes. The inspector preview uses a projected three-dimensional view and draws the complete rounded centerline in orange.

All fixtures are included in the visible render mesh, share the global wire diameter and Wire Rail material, and are not added to the ball-channel collider. A leg and foot is omitted wherever either of its two bottom rails is inactive or its authored path folds back too tightly to form a valid tube.

Use the duplicate icon in a fixture header to insert an independent copy immediately after it, or the trash icon to remove it. A duplicate starts with all of the source fixture's settings and route position.

## Render Geometry

Every active authored rail is swept along the spline as a visible tube. **Rails** sets the component-wide number of available wires from 1–6. **Wire Diameter** sets the shared thickness of every rail and fixture and defaults to 8 VPX units. The tubes use an octagonal cross-section by default. Use **Tube Sides** to increase or decrease radial detail, **Wire Cap Bevel** to add one chamfer segment around every exposed wire end, and **Minimum Samples Per Layout Span** to set the baseline longitudinal detail. The global bevel is clamped to half the wire diameter, and its bevel and flat face have appropriate distinct normals. The generator inserts additional rings automatically wherever the actual offset wire turns by more than five degrees, so smooth bends do not collapse into one skewed end ring.

Assign **Material** to control the wire's appearance. When no material is assigned, VPE uses the active render pipeline's default material.

Render tubes are capped where a rail begins or ends. Matching or explicitly continuous rails in neighboring layouts remain open internally so their surfaces meet without an unnecessary cap.

## Ball Channel Collider

The collider is one swept channel around the space occupied by the reference ball. It deliberately does not create a physics tube around every visible wire. Instead, its cross-section uses up to eight flat facets fitted to the contact directions between the rails and the reference ball.

The generated channel opens or closes according to the rails enabled in the active wire layout. Its facets are refitted when individual wire positions, activation, or diameter change:

- One rail provides a narrow bottom contact surface.
- Two bottom rails provide a faceted floor with the top open.
- Three rails add a wall on the side selected by **Third Rail**.
- Four rails provide floor and left/right walls while keeping the top open.
- Five or more rails add upper support and close the cross-section around the ball.

The collider is registered with VPE's pinball physics engine rather than Unity's built-in physics. **Ball Diameter** is the diameter of the reference ball used to calculate the rail contact points and fit the channel around them. It changes only this generated collision channel; it does not resize the visible wires or any ball in the game. **Curvature Detail** controls adaptive tessellation: curved or changing channel spans receive more rows, while straight spans remain sparse even when the spline contains extra collinear knots. Physics material, elasticity, friction, and scatter are configured in the same collapsible **Ball Channel Collider** section.

> [!note]
> The collider facets model the continuous channel that constrains the ball, not the exact round surface of each wire. This keeps the collision topology small and prevents seams between several independent wire colliders.

## Scene View Preview

Select either the Wire Rail GameObject or its spline child to display the active rail centerlines in the Scene view. Each rail uses a different preview color, and every layout start is labeled with its layout number plus active and available rail counts. A thick, high-contrast center spine marks the editable spline route over the generated geometry. The generated render tubes remain visible normally in the Scene and Game views.

Selecting the Wire Rail also displays an **Edit Wire Rail Spline** button in the Scene view. While spline editing is active, knots appear as outlined circular grips; the active knot is gold, and editable Bézier tangents appear as smaller cyan grips joined to it by thick arms. A grip becomes active on the same mouse-down that starts its drag, so it does not need to be selected before moving it. Clicking a knot also displays Unity's standard position gizmo for axis- and plane-constrained movement. Hover a grip for its action hint. The panel also shows the double-click shortcuts: double-click the route line to insert a knot at that position, or double-click a knot to remove it. An open spline keeps at least two knots, and a closed spline keeps at least three.

Enable **Show Collider Preview** to draw the generated ball-channel mesh with VPE's standard translucent green collider color and green edges. The colored centerlines and center spine are authoring guides; their colors and line widths do not represent the render material or physical wire thickness. The wire circles in the Inspector cross-section do use their authored diameters.

## Current Limitations

- Only the first spline in the generated Spline Container is used.
- Transition blending matches wires by their one-based index; it does not remap a wire to a different index in the next layout.
- Extreme cross-section changes can alter the ball-channel facet topology and cannot currently be blended as one collider.
- Junctions and end fittings are not generated.
- Wire rail data is not yet imported from or exported to VPX.
