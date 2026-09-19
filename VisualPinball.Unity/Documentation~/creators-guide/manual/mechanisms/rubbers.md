---
uid: rubbers
title: Rubbers
description: Author rubber geometry, collision, guide-distance elasticity, and the compliant rebound.
---

# Rubbers

Rubbers can use either **Spline** or **Guided** geometry. Spline geometry follows the rubber's authored control points. Guided geometry is fitted around ordered guide slots and stores a baked path made of supported arcs and free spans. Refit a guided rubber after changing its guide bindings, guide slots, or geometry.

## Collider Models

The Rubber Collider offers two models:

- **Legacy** builds collision from the rendered rubber mesh. Use it for spline rubbers and compatibility with existing tables.
- **Physical** builds a static chain of round-cord capsule colliders from the serialized guided path. If that path is missing or structurally invalid, VPE reports the problem and falls back to Legacy collision. Live guide references and editor bake-current status do not affect runtime collider generation.

Physical collision geometry is static: the cord never moves. What a Physical rubber can do beyond a wall is respond to a hit as a stretched cord would, see [Compliant Rebound](#compliant-rebound).

## Guide-distance Elasticity

Enable **Guide-distance Elasticity** on a Physical rubber to make its unsupported free spans more bouncy than rubber backed by a guide. Supported arcs use the configured guide scale, and each free-span capsule receives a fixed multiplier that transitions toward the full **Ball Contact Material** response with distance from the nearest supported endpoint.

The multipliers are generated with the capsule colliders when physics starts. They are not stored in the editor bake and do not require guide slots or other editor-only authoring state at runtime.

- **Guide Elasticity Scale** controls the response where the rubber is backed by a guide. `1` keeps the base material response and `0` removes its normal rebound term at supported arcs and span endpoints.
- **Guide Influence Length** is the distance in VPX units over which the response transitions from the guide scale toward full elasticity.
- **Maximum Response Segment Length** sets an upper bound for capsule length while this response is active. VPE uses the smaller of this value and one eighth of **Guide Influence Length**, ensuring at least eight response samples through the transition. The inspector shows this effective maximum; shorter segments follow the transition more closely but create more colliders.

The effect is disabled by default. A value of `1` for the guide scale also preserves the existing collider count. Changes refresh Scene-view collider visualization immediately; physics already registered in Play mode uses the new settings after Play mode is restarted. While the compliant rebound is enabled, these settings are hidden, because the free spans then respond as a cord and only the supported arcs keep the guide scale.

## Compliant Rebound

A ball that hits a free span of a real rubber does not bounce off it like off a wall. The cord stretches into a V around the ball, and the direction and speed it throws the ball back with depend on where along the span the ball hit and how hard. **Compliant Rebound** gives a Physical rubber exactly that response while its collision geometry stays static.

At the moment of impact, VPE computes the trajectory the ball would follow if it were riding the apex of a massless cord pinned at the span's two guides, with the cord's tension taken from the rubber's material at its current stretch, and hands the ball the resulting exit velocity. A hit near a post leaves toward the far post, a hard hit sinks deeper and turns further, and a center hit at normal incidence behaves exactly as before. Restitution, friction, scatter, and hit events come from the collider's **Ball Contact Material** as they do for every other collider.

### Setting It Up

1. Give the rubber a **Rest Length**. It is the unstretched length of the cord in VPX units. In the Rubber inspector, **Set Rest Length From Fitted Length** takes the fitted centerline length and assumes a ten percent installed stretch, which is a good start for a rubber ring on posts. The inspector shows the resulting installed stretch.
2. In the Rubber Collider inspector, under **Compliant Rebound**, enable it and assign a **Rebound Material**. This is a *Rubber Physics Material* asset with the cord's density, its stress-versus-stretch curve, and its damping ratio, authored in SI units. The default asset is soft; stiffer rubber wants a steeper curve.
3. Set the **Deflection Clearance**, the deepest deflection in VPX units the cord may reach before it is treated as stopped by whatever sits behind it. The material's maximum stretch caps it as well.
4. That is all. VPE compiles the material and the guided path into per-span data on its own, whenever one of the inputs changes and when the game starts, and the Rubber Collider inspector says whether the rebound is active on the rubber's free spans and, if not, why. **Recompute Compliant Rebound** re-runs it on demand.

### What Qualifies

- Every free span gets the compliant rebound, straight or rounded. The cord model is pinned at the span's two ends and runs straight between them, so a rounded span's bulge is ignored the way the tube's thickness is.
- The rubber must not be kinematic.
- Every guide the path touches counts as rigid: near a post the response blends back to the wall response, so nothing ever pushes the cord through its guide.
- A hit that would carry the cord's apex past a guide, or would stretch it beyond the material's maximum, falls back to the wall response for that hit.

### Visuals

Add a **Rubber Deformation Renderer** to the rubber to see the cord move. It displaces the rendered tube along a V profile: inward when a ball hits, decaying over a few tens of milliseconds, and outward when a [physical slingshot](xref:slingshots) bound to the span fires. It never touches the shared mesh and does nothing outside Play mode.
