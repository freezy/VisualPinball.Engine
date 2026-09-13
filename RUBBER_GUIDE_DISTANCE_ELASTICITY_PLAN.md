# Guided Rubber Distance-Dependent Elasticity Implementation Plan

Status: implemented on `feat/rubber-guide-distance-elasticity`; revised after Fable 5.1 review and Opus pre-commit review

Scope: guided rubbers using `RubberColliderMode.Physical`

Non-goal: deformable free-span simulation

## 1. Outcome

Give each emitted round-cord capsule a deterministic elasticity response based on its distance along the rubber from the nearest supporting guide. Supported arcs use an authored attenuated response. Free spans transition from that backed response toward the full contact-material response in short, piecewise-constant bands.

This is a static-contact approximation. It does not move the rubber, store strain energy, couple ball mass to a deforming span, or replace the separate experimental deformation path represented by `RubberPhysicsMaterialAsset` and `RubberSlingshotComponent`.

Legacy collider generation must remain byte-for-byte and behaviorally unchanged.

## 2. Product behavior

Add an opt-in **Guide-distance elasticity** section to `RubberColliderComponent`, visible only for Physical mode:

- **Enabled**, default `false`. Existing scenes, imports, and packages keep their current behavior and collider counts.
- **Guide elasticity scale**, default `1`, authored in the inclusive range `[0, 1]`. This multiplies the elasticity produced by the existing contact material where the rubber is backed by a guide, so the approximation can reduce supported rebound while free spans retain the full authored response.
- **Guide influence length**, proposed default `50` VP units. This is measured as arc length along a free span from its nearest supported endpoint, not as world-space distance to a guide center.
- **Maximum response segment length**, proposed default `20` VP units. This limits the length of free-span capsules while distance-dependent elasticity is enabled.

The existing Physics Material or inline override remains the full free-span response. No second material asset is introduced. Friction, scatter, and elasticity falloff retain their current authored values.

The effective scale for a capsule on a free span is sampled at the capsule midpoint:

```text
distance = min(midpoint distance from span start, midpoint distance from span end)
weight = smoothstep(0, guide influence length, distance)
elasticity scale = lerp(guide elasticity scale, 1, weight)
```

Every capsule has one constant scale. There is no guide lookup, curve evaluation, or distance calculation in the physics loop.

Consequences of this definition:

- Every supported arc uses the guide elasticity scale.
- A free span starts and ends near the guide elasticity scale because both endpoints touch supports.
- A long free span reaches scale `1` in its middle.
- A span shorter than twice the influence length never reaches scale `1`, reflecting support from both ends.
- Scale `1` is the existing full response and guide scale `0` removes normal rebound from the elasticity term at supports. The final resolved coefficient is not globally clamped, because existing materials may intentionally author values above one; the new feature only attenuates that existing response near guides.

## 3. Collider subdivision

Retain the existing geometry-driven tessellation first:

- cubic free spans use `RubberPathMath.FreeSpanSubdivisionCount`;
- supported arcs use the current sagitta/chord tolerance;
- adjacent path elements continue to share their exact endpoint.

When guide-distance elasticity is enabled and the guide scale differs from `1`, further split every tessellated free-span chord into `ceil(chordLength / effectiveMaximumLength)` collinear capsules. Splitting the already accepted chord, rather than merely increasing uniform Bézier parameter subdivisions, guarantees the emitted capsule length bound even on Béziers with non-uniform parameter speed.

Use:

```text
effective maximum length = min(maximum response segment length, guide influence length / 8)
```

The `influence length / 8` constraint provides at least eight midpoint samples through a full transition. Because the maximum derivative of smoothstep is `1.5`, adjacent midpoint scales differ by at most `3/16` of the authored scale range when the requested bound is honored. Runtime validation must sanitize settings before allocation: non-finite scale becomes `1`, finite scale is clamped to `[0, 1]`, non-finite or non-positive lengths become their documented defaults (`50` and `20`), and finite positive lengths below `0.1` VP units clamp to `0.1`.

Add `MaximumResponseSegmentsPerElement = 4096` to `RubberPhysicalColliderGenerator`, matching the existing maximum cubic tessellation count. Compute the desired final count before allocating. If response splitting would exceed the cap, emit one warning for that rubber generation pass and retain the element's existing geometry-driven chords, assigning midpoint scales to those chords without further subdivision. Thus the feature never increases a free-span element beyond the branch's current geometric ceiling, while corrupt or extreme settings degrade to a coarse response profile rather than an allocation spike.

Build the final point list before assigning materials. Compute cumulative distance over that emitted polyline and sample each capsule at half its chord length. This keeps distance calculation deterministic and consistent with the geometry that actually collides, without requiring an inverse arc-length solve for cubic Béziers.

Do not add response subdivisions when the feature is disabled or the sanitized guide scale is exactly `1`. That preserves current Physical collider count and ordering by default without making serialized behavior depend on an undocumented epsilon.

## 4. Material integration

`ColliderHeader.Material.Elasticity` alone is insufficient because `BallCollider.Collide3DWall` ignores it when the selected `PhysicsMaterialAsset` uses an elasticity-over-velocity curve. The guide-distance response must work for both constant and velocity-dependent materials.

Add an `ElasticityScale` float to `SweptCircleCollider`, initialized to `1` by the existing constructor. Explicitly copy `Header` and `ElasticityScale` in `Transform(SweptCircleCollider collider, float4x4 matrix)` before transforming geometry; the self-transform then remains safe. The Physical rubber generator passes the sampled scale to each emitted collider. `NativeColliders` uses struct-sized allocation and `MemCpy`, so no manual native offset is required, but its identity and kinematic copies need regression coverage.

Extract the existing constant/falloff-versus-LUT branch into one `BallCollider.ResolveElasticity` helper. Keep the existing `Collide3DWall` signature as the scale-`1` path and add a scale-aware overload used only by `SweptCircleCollider`; both paths call the same resolver. Apply the sanitized scale after the helper obtains elasticity from either:

- `Math.ElasticityWithFalloff`, or
- the per-item elasticity-over-velocity LUT.

Conceptually:

```csharp
elasticity = ResolveExistingElasticity(material, impactSpeed, state);
elasticity *= elasticityScale;
```

All other collider types continue to use an implicit scale of `1` through their existing call path. Do not duplicate the LUT/falloff resolution and do not add guide or rubber concepts to `ColliderInfo`, `PhysicsMaterialData`, or generic broad-phase structures. `SpringHingeCollider` has a pre-existing separate elasticity-resolution implementation and LUT-range discrepancy; changing it is explicitly out of scope for this feature.

The additional runtime cost is one float in each `SweptCircleCollider` and one multiply when that collider resolves an impact. All distance and profile work occurs during collider generation.

## 5. Authoring and serialization

### 5.1 Component and inspector

Add serialized fields and public properties to `RubberColliderComponent` using the existing naming and tooltip conventions. The inspector must:

- show the section only when Physical is selected or included in a mixed selection;
- disable its subordinate fields until Enabled is checked;
- explain that this is a static approximation and that distance is measured along the free span;
- use the inspector's collider-dirty hook when any setting changes, rebuilding editor collider visualization immediately and applying runtime physics changes on the next play-mode start/physics registration;
- state that play-mode physics does not hot-rebuild when these values are edited;
- show sanitized limits rather than allowing zero, negative, NaN, or infinity into generation.

Keep the reserved deformation material disabled. This feature does not consume `RubberPhysicsMaterialAsset`, whose stress curve and damping describe future deformation rather than static restitution.

### 5.2 Package compatibility

Add the settings to `RubberColliderPackable`, not the references payload, because they are scalar collider configuration rather than asset references. Add a small schema/version discriminator for this optional block:

- old payload or version `0`: reset to disabled and retain the component's documented defaults;
- version `1`: migrate the former free-span scale value to the guide scale, preserving its numeric value while adopting the corrected response direction;
- version `2`: restore Enabled, guide scale, influence length, and maximum response segment length through the same sanitizer used by authoring and generation.

The explicit version avoids treating an old payload's missing numeric fields as intentional zero values. A missing or zero version resets the entire optional block to `Enabled = false`, `GuideElasticityScale = 1`, `GuideInfluenceLength = 50`, and `MaximumResponseSegmentLength = 20`. For versions `1` and `2`, a non-finite scale becomes `1`, finite scale clamps to `[0, 1]`, non-finite or non-positive lengths return to their respective `50`/`20` defaults, and finite positive lengths below `0.1` clamp to `0.1`; the serialized Enabled value otherwise remains intact. Add package round-trip, version-1 migration, old-payload, and malformed-value tests as required deliverables.

No new VPX fields are exported. VPX import still creates Spline + Legacy rubbers, so this setting cannot change imported-table behavior unless an author explicitly converts and enables it.

## 6. Planned code changes

1. `VisualPinball.Unity/VisualPinball.Unity/VPT/Rubber/RubberColliderComponent.cs`
   - Add defaults, serialized configuration, one sanitization helper, public accessors, and a small readonly settings value passed explicitly to generation.
2. `VisualPinball.Unity/VisualPinball.Unity.Editor/VPT/Rubber/RubberColliderInspector.cs`
   - Add the Physical-only guide-distance elasticity UI and collider rebuild hooks.
3. `VisualPinball.Unity/VisualPinball.Unity/VPT/Rubber/RubberPhysicalColliderGenerator.cs`
   - Separate geometry tessellation from final chord splitting, compute cumulative free-span distance, enforce the 4096-segment response ceiling, sample the response profile, and pass one scale per capsule. Resolve and reuse one `ColliderInfo` per `GenerateColliders` call instead of invoking `GetColliderInfo()` for every capsule.
4. `VisualPinball.Unity/VisualPinball.Unity/Physics/Collider/SweptCircleCollider.cs`
   - Store and preserve `ElasticityScale`, including the copy-transform overload; pass it to rigid-wall response.
5. `VisualPinball.Unity/VisualPinball.Unity/VPT/Ball/BallCollider.cs`
   - Extract shared elasticity resolution and add the scoped scaled-elasticity path after constant/falloff or LUT resolution while preserving all existing callers.
6. `VisualPinball.Unity/VisualPinball.Unity/VPT/Rubber/RubberPackable.cs`
   - Persist the versioned scalar configuration with backward-compatible defaults.
7. `VisualPinball.Unity/VisualPinball.Unity.Test/Physics/SweptCircleColliderTests.cs`
   - Cover subdivision, scale assignment, transforms, seams, material response, and unchanged defaults.
8. The existing rubber package tests
   - Cover new, old, and malformed payload behavior.
9. `VisualPinball.Unity/Documentation~/creators-guide/manual/mechanisms/rubbers.md`, its `toc.yml` entry, and `CHANGELOG.md`
   - Document Guided versus Spline geometry, Legacy versus Physical collision, the static nature of the profile, the settings, the next-play-start update behavior, and the user-visible addition.

No changes are planned for guide slots. `RubberSupportFriction` describes rubber-on-guide support friction for future deformation and must not be repurposed as ball-contact elasticity.

## 7. Test plan

### 7.1 Generation tests

- A straight free span emits the existing single capsule when the feature is disabled.
- A scale of `1` emits the existing collider count and material data even when Enabled is true.
- A long straight span emits capsules no longer than the effective maximum length.
- A curved Bézier first satisfies geometric tolerance and then the maximum emitted chord length.
- Emitted points remain continuous and deterministically ordered.
- Supported arcs use the guide scale and retain their existing tessellation.
- Free-span scales are symmetric about the span midpoint.
- The first and last free-span capsule scales approach the guide value; the middle of a sufficiently long span reaches scale `1`.
- A short span produces the expected partial transition and never overshoots either endpoint value.
- Adjacent scales differ by no more than `3/16` of the authored scale range while the requested response bound is honored.
- Emitted-polyline length remains within the existing chord-tolerance envelope of the baked `RubberPathElement.Length`.
- Non-finite, non-positive, and extreme settings use the documented sanitization or 4096-segment fallback and cannot create unbounded collider counts.
- `GetColliderInfo()` is evaluated once per generator pass, not once per emitted capsule.

### 7.2 Collision-response tests

- Two otherwise identical swept-circle impacts with different scales produce the expected relative rebound for a constant/falloff material.
- The same comparison works when elasticity comes from an elasticity-over-velocity LUT.
- A scale of `1` reproduces existing rebound within current test tolerance.
- A scale below `1` never produces more rebound than the scale-`1` path for the same base material and impact.
- Scale and `Header` survive the explicit copy-transform overload and static native-container copy.
- Kinematic coverage exercises the actual ball-into-local collision path and `KinematicCollidersAtIdentity` native copy; it must not assume swept-circle colliders are transformed by `PhysicsState.TransformKinematicColliders`.
- Hits at adjacent capsule endpoints remain finite, stay within the documented adjacent-scale bound, and do not double-fire the rubber hit event.

### 7.3 Compatibility tests

- Legacy rubber collider golden tests are unchanged.
- Existing Physical tests pass with default settings and existing collider counts.
- New package data round-trips all four fields.
- An old package without the versioned block loads with the feature disabled and current behavior.
- Malformed version-1 values resolve to the exact documented fallbacks/clamps.
- Copy/duplicate and prefab serialization preserve the settings.

### 7.4 Performance validation

Record, on at least one representative table and one synthetic worst-case loop:

- total Physical swept-circle count before and after enabling the proposed defaults;
- collider registration time and persistent collider memory;
- broad-phase candidate count and physics-step time with balls repeatedly contacting long spans.

The expected hot-path change is one multiply per swept-circle collision. The meaningful risks are collider-count growth, registration cost, broad-phase candidates, and editor gizmo drawing. The authored default of `20` is an upper bound; with the default influence length of `50`, the effective maximum is `6.25` because the transition sampling constraint is tighter. Validate the effective default and the 4096 safety ceiling before merge. The default disabled state must have no collider-count increase.

## 8. Delivery sequence

1. Add the sanitized serialized configuration, versioned package data, inspector controls, creator documentation, and changelog entry with the feature disabled by default.
2. Refactor Physical tessellation into final emitted chords without changing current output; lock that equivalence with tests.
3. Add bounded free-span subdivision, the 4096-segment fallback, one-per-pass `ColliderInfo`, and deterministic midpoint scale assignment.
4. Add `SweptCircleCollider.ElasticityScale`, preserve it in copy/native paths, extract the shared elasticity resolver, and add the constant-material response test.
5. Verify elasticity-over-velocity LUT behavior through the shared resolver and add the LUT response test.
6. Run focused Unity rubber/collider/package tests, then the full Unity EditMode suite used for the branch.
7. Profile representative and worst-case content, tune the default maximum length/cap, and record results in the implementation commit or accompanying private development notes.

## 9. Acceptance criteria

- The serialized feature may remain configured while inactive. Physical runtime generation consumes any structurally valid serialized guided path and bake transform, independent of live guide references, slot IDs, or editor bake-current status; only a missing or structurally invalid path retains the Legacy fallback.
- Supported arcs always use the guide elasticity scale when the feature is active.
- Free-span effective elasticity varies deterministically with along-span distance to the nearest support.
- No guide-distance work or response interpolation occurs in the per-step broad/narrow phase.
- Constant and velocity-dependent base materials both honor the per-capsule scale.
- Default and Legacy behavior remain unchanged.
- Collider growth is bounded, measured, and acceptable on representative content.
- Packaging, duplication, copy transforms, the actual kinematic collision path, and invalid-data handling are covered by tests.

## 10. Deliberate limitations

- Response is piecewise constant per capsule, so an exact shared endpoint may resolve against either adjacent value. Smooth sampling and the `3/16` adjacent-range bound keep that difference explicit and testable; the 4096-segment pathological fallback may be coarser and warns. This plan does not add a continuous per-contact path coordinate to collision events.
- The effect is an authored restitution approximation, not a claim that coefficient of restitution physically depends on guide distance.
- Friction does not vary with support distance in this change.
- Dynamic deformation and modern slingshot behavior remain separate experimental phases.

## 11. Fable 5.1 review disposition

Fable 5.1 reviewed the original draft against the branch source in read-only plan mode. This revision incorporates its substantiated findings: explicit copy/native/kinematic path coverage, attenuation-only scale bounds, a concrete segment ceiling and fallback, one-per-pass material resolution, a tighter seam bound, accurate editor rebuild semantics, a shared elasticity resolver, exact package sanitization, and documentation work.

Two review claims were adjusted rather than copied verbatim. The workspace-level redesign brief exists outside this Git repository, so this plan is now self-contained and names the concrete deformation types instead of depending on that file. The reviewer also attributed a changelog requirement to an unrelated line in `AGENTS.md`; documentation and changelog work remains in scope because this is a user-facing authoring feature, not because that citation was valid.

## 12. Implementation validation

Validation was run on 2026-09-13 in Unity 6000.5.0f1 through the `vpe-godzilla` project, which references this repository as a local package. The focused rubber, swept-circle, and packaging suite passed 69/69 tests. The full `VisualPinball.Unity.Test` EditMode assembly completed all 685 tests with 672 passes and 13 failures in unrelated importer precision/filesystem and spring-hinge fixture tests; none of those failures touched the files in this change.

The checked-in measurement test uses two synthetic loop fixtures because existing authored content cannot contain this new opt-in setting yet. Build and registration figures are averages over 200 iterations after five warm-up iterations. The contact-query figures are averages over 2,000 iterations after 50 warm-up iterations and include the production static broad-phase query plus swept-circle narrow-phase hit tests for every returned candidate. They are scoped microbenchmarks, not whole-player frame timings.

| Fixture | Colliders disabled / enabled | Persistent swept-circle bytes disabled / enabled | Generation ms disabled / enabled | Registration ms disabled / enabled | Candidates disabled / enabled | Contact query ms disabled / enabled |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Representative loop | 52 / 176 | 5,824 / 19,712 | 0.138 / 0.463 | 0.004 / 0.009 | 24 / 77 | 0.028041 / 0.110190 |
| Four 1,000-unit free spans | 4 / 640 | 448 / 71,680 | 0.018 / 1.595 | 0.003 / 0.029 | 0 / 298 | 0.001509 / 0.452275 |

The results confirm zero collider-count growth while the feature is disabled and quantify the opt-in tradeoff. The default profile adds bounded registration-time and persistent-memory cost; the deliberately pathological loop remains far below the 4,096-per-element ceiling. The inspector exposes the computed effective maximum so authors can see why the default `20`-unit authored cap produces `6.25`-unit response capsules with a `50`-unit influence length. No guide-distance calculation or curve evaluation was added to the contact loop; enabled swept-circle response adds only the stored scale and one multiplication after resolving the existing material elasticity.
