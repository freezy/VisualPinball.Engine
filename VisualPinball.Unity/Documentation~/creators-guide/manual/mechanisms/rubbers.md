---
uid: rubbers
title: Rubbers
description: Author rubber geometry, collision, and guide-distance elasticity.
---

# Rubbers

Rubbers can use either **Spline** or **Guided** geometry. Spline geometry follows the rubber's authored control points. Guided geometry is fitted around ordered guide slots and stores a baked path made of supported arcs and free spans. Refit a guided rubber after changing its guide bindings, guide slots, or geometry.

## Collider Models

The Rubber Collider offers two models:

- **Legacy** builds collision from the rendered rubber mesh. Use it for spline rubbers and compatibility with existing tables.
- **Physical** builds a static chain of round-cord capsule colliders from the serialized guided path. If that path is missing or structurally invalid, VPE reports the problem and falls back to Legacy collision. Live guide references and editor bake-current status do not affect runtime collider generation.

Physical collision is a static approximation. It gives the ball the round contact surface of a rubber cord, but it does not deform or store strain energy.

## Guide-distance Elasticity

Enable **Guide-distance Elasticity** on a Physical rubber to make unsupported free spans less bouncy than rubber backed by a guide. Supported arcs always use the full elasticity from **Ball Contact Material**. Each free-span capsule receives a fixed multiplier based on its distance along the span from the nearest supported endpoint.

The multipliers are generated with the capsule colliders when physics starts. They are not stored in the editor bake and do not require guide slots or other editor-only authoring state at runtime.

- **Free-span Elasticity Scale** controls the response in the unsupported middle. `1` keeps the base material response and `0` removes its normal rebound term.
- **Guide Influence Length** is the distance in VPX units over which the response transitions away from each support.
- **Maximum Response Segment Length** sets an upper bound for capsule length while this response is active. VPE uses the smaller of this value and one eighth of **Guide Influence Length**, ensuring at least eight response samples through the transition. The inspector shows this effective maximum; shorter segments follow the transition more closely but create more colliders.

The effect is disabled by default. A value of `1` for the free-span scale also preserves the existing collider count. Changes refresh Scene-view collider visualization immediately; physics already registered in Play mode uses the new settings after Play mode is restarted.

This setting changes ball-contact elasticity only. It does not change friction, scatter, the guided rubber's shape, or the reserved deformation material.
