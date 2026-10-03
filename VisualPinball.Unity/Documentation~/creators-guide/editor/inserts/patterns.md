---
uid: insert_patterns
title: Insert Patterns
description: Choose and tune starbursts, stipples, and engraved lines, and understand which pattern details remain in the mesh after baking.
---

# Insert Patterns

A pattern shapes the floor you see through the insert. It changes how the lamp breaks into ribs or facets and how surface detail catches a reflection. **Insert → Geometry → Pattern** offers one choice at a time; the selected pattern uses the current outline, channel, and holes. New inserts start with Starburst.

| Pattern | Shapes | Finished mesh after baking |
| --- | --- | --- |
| **None** | All shapes | Plain floor |
| **Starburst** | All shapes | Radial facets remain in the mesh |
| **Stippled** | All shapes | Ordinary no-pattern body mesh; pyramids live in the maps |
| **Engraved Line** | Flash and Custom | Plain middle; the trough lives in the maps |

<!-- Screenshot: patterns-family. The same rectangle with None, Starburst, and Stippled; a separate flash with Engraved Line. Lit and unlit. -->

> [!NOTE]
> **Pattern shape and material response are separate.** Pattern controls change the source geometry and need baking again. Surface Normal Strength, Optical Facet Strength, and the material's glow controls change how existing maps are read and update immediately. Facet Contrast and Mottle are specialized for a starburst fan; the Inspector disables them for stipples, engraving, and a plain floor.

## None

None leaves the main floor flat. The channel, bevel, and holes still apply. Baking can still give it rounded edge normals, fine grain, and hybrid optical maps. Choose this when the insert has a smooth interior or when a plain floor makes the lamp appearance you want.

## Starburst

A starburst is a radial wheel of alternating ridges and valleys, clipped to the available floor. The center can sit inside the insert or outside it; a center beyond one end gives the long fan seen in many narrow inserts.

| Parameter | Meaning |
| --- | --- |
| **Bursts** | Number of bursts around the complete wheel, from 0 to 128. Each burst has two slope sectors. Only the sectors that intersect the insert appear, so this is not a count of visible ribs. Below 2 there is no starburst. The default is 12. |
| **Center** | Center of the radial wheel in generator X/Y coordinates, relative to the insert origin, in VPX units. Moving it changes the direction and spacing of ribs on the insert. The default is (0, 0). |
| **Offset** | Vertical rise and fall of the wheel's rim relative to its center plane, in VPX units. The default is 1.5. Larger values make steeper facets; zero removes their relief. |
| **Centre on the Arc** | Banana-only button that places Center at the center of its circular arc, making the spokes cross the band. |

The offset is reduced when needed to keep the ridges below the rim and, with a channel, the valleys above the channel floor. If the shape allows little vertical relief, increase the available depths or choose a smaller offset.

<!-- Diagram: patterns-starburst-wheel. Complete wheel overlaid on a narrow insert: Bursts counts the wheel, while only a subset intersects the floor. -->
<!-- Screenshot: patterns-starburst. Bursts 8/24, Center inside/outside, Offset 0.5/2, and banana center before/after Centre on the Arc. -->

> [!NOTE]
> **Starburst normals.** The finished mesh retains its slope normals. The bake also stores a tangent-space surface map and an insert-local optical map. Raising Surface Normal Strength strongly can exaggerate a facet already present in the mesh. Optical Facet Strength changes the lamp's bending separately, and Facet Flatten softens the surface reflection ribs without changing that optical bending.

## Stippled

Stipples are square pyramids arranged on a grid. Each has a square base and four triangular slopes meeting at an apex. A flat margin separates neighboring bases. The grid is centered on the floor's bounding box.

| Parameter | Meaning |
| --- | --- |
| **Stipple Height** | Apex height above the main floor, in VPX units. The default is 1. Taller pyramids produce steeper normals. It is reduced to keep the apex below the rim. |
| **Stipple Width** | Length of each side of the square base, in VPX units. The default is 4. Larger bases make a coarser pattern. |
| **Stipple Margin** | Flat gap between adjacent bases, in VPX units. The default is 0.5. Zero allows neighboring bases to touch. Grid pitch is Width plus Margin. |

The generator places only complete pyramids. A pyramid is omitted if its base would overlap a rounded boundary or a hole; stipples are not cut into partial pieces at those edges. Changing Width or Margin can therefore change which pyramids fit as well as their density. The grid is limited to 4096 candidate cells across the floor's bounding box, including cells that are later omitted. If it exceeds that limit, rebuilding reports an error; increase Stipple Width or Stipple Margin.

<!-- Diagram: patterns-stipple-cell. Side and top views dimensioning Height, Width, and Margin; a hole with intersecting cells omitted. -->
<!-- Screenshot: patterns-stipple. Height 0.5/2, Width 3/6, Margin 0/1; compare source and finished wireframes. -->

> [!NOTE]
> **Plain bake target.** Blender receives the pyramids as its detailed source and the ordinary no-pattern body as its bake target. The target owns the UV atlas and tangent frames returned to Unity. Once the bake fits, that target is the displayed body, with exactly the same triangle positions as the corresponding no-pattern body. Detail remains in the normal and optical maps rather than in a flattened grid of source triangles.

## Engraved Line

Engraved Line makes a narrow trough following an inset version of the floor's outline. As the outline is inset, its flat ends can close into points and its inward notches widen. On a flash, the result resembles a lightning bolt engraved into the plastic; a custom shape follows its own floor outline.

| Parameter | Meaning |
| --- | --- |
| **Inset** | Distance from the main floor edge to the middle of the line, in VPX units. The default is 6. It is applied inside the channel's already inset floor. |
| **Width** | Full width across the trough, in VPX units. The default is 2. A wider line needs more clearance from the floor edge. |
| **Depth** | Depth of the line at its middle, in VPX units. The default is 0.8. Zero removes its vertical cut. |
| **Segments** | Steps down each side of the trough, from 1 to 16. One gives a V cut; additional steps approximate a rounded floor. The default is 1. The bake's Edge Bevel can soften the V cut. |

Engraved Line is available for Flash and Custom shapes, and takes the place of the starburst or stipples. If the inset line cannot fit or crosses itself at a narrow part of the outline, the Inspector reports why. Reduce Inset when the inner outline collapses; increase clearance or reduce Width when its outer edge reaches beyond the floor.

<!-- Diagram: patterns-engraving. Flash outline, channel edge, and line center; cross-section showing Width, Depth, and Segments. -->
<!-- Screenshot: patterns-engraving. Inset 4/7, Width 1/3, Depth 0.4/1.2, Segments 1/6, with an inset that fits each chosen flash. -->

> [!NOTE]
> **Replacing the trough.** The engraved middle keeps its projection from above through UV packing. After a fitting bake, VPE replaces the middle's trough geometry with a plain island carrying the same projected maps. Changing the line brings its source geometry back until the next bake. This differs from stippling, which bakes onto a separate plain body target from the start.

## Shared detail

**Bottom Bevel** shapes the actual body where the outer wall meets the channel. **Edge Bevel** rounds sharp edges in the baked surface normals, including pattern edges. **Grain Strength** and **Grain Size** add fine baked surface texture. These apply across patterns, so they are documented once under [Geometry](geometry.md#body-channel-and-bottom-bevel) and [Baking](baking.md#bake-settings).

The material's **Grain** is a separate procedural variation in color and emission. It needs no rebake. Its **Facet Contrast**, **Mottle Amount**, and **Mottle Size** are starburst-specific, while the virtual lamp's refraction reads the optical normals of every pattern. See [Body Material](body-material.md).
