---
uid: insert_visual_plan
title: Insert Documentation — Illustration Plan
description: Screenshot pairs and explanatory diagrams planned for the insert documentation.
---

# Insert Documentation — Illustration Plan

This is the production checklist for illustrations, kept outside the reader's navigation. The documentation is readable without the images; HTML comments at the intended insertion points identify each group below. Once screenshots are supplied, replace those comments with images and captions, keeping the parameter comparison beside its explanation.

## Capture rules

For **each parameter**, capture at least **two labeled values or states**, varying that parameter alone. A row below listing several fields requests a separate pair for each field. Suggested values are starting points, not required settings; adjust them when a particular outline makes one invalid or the effect too subtle. Keep the camera, light rig, exposure, other material settings, and other lamps unchanged within a pair.

Use a top view for outline changes, an oblique or cutaway view for height and bevel changes, and a reflected light for normal/smoothness changes. Bake both states when the parameter affects geometry or bake settings. Turn the optional cover off when it hides body detail. For material controls, use a fixed camera and a fitting bake. Record the prerequisite settings in the caption so a reader can reproduce the comparison.

For vectors, vary one coordinate at a time when the axes do different jobs. Color fields need two clearly different colors at equal brightness unless brightness is the purpose of the comparison. Pure configuration fields such as *Blender* executable and *Bake on GPU* need Inspector states or explanatory diagrams, rather than a claimed visual change in the insert.

## Overview and workflow

| Figure group | Capture | Placement |
| --- | --- | --- |
| `overview-family` | Eight shape thumbnails; the same finished insert with lamp intensity 0 and 1. | Overview → What you can make |
| `customizing-library` | Standard library insert placed in the table; the same insert customized. Include the preset buttons. | Customizing → Customize a library insert |
| `customizing-colors` | All seven color presets on one bake, fixed lamp Color and exposure. Include cover when demonstrating Cover Color. | Customizing → Color presets |
| `customizing-material-presets` | Clear and Colored on the same bake, with an off pair and a lit pair. | Customizing → Material presets |
| `baking-inspector` | Before bake versus success, plus read-only package versus forked project assets. | Baking → First bake and Bake actions |
| `baking-stale` | Fitting shaded insert versus yellow stale source after an edit. Use shaded Scene drawing mode. | Baking → Fresh and stale bakes |

## Geometry comparisons

All lengths here are VPX units. Use an adequately sized outline so radius and channel settings fit without clamping. Show the actual field label and value, and the whole finished outline.

| Parameter | Suggested two values / states | View and prerequisites |
| --- | --- | --- |
| Shape | Standard / Custom; supplement with all eight built-in shapes | Top view; common approximate bounding size |
| Width, on Triangle, Rectangle, Diamond, Bullet, and Arrow | 47.059 / 70.588 | Separate pair for each shape; fix Height |
| Height, on Triangle, Rectangle, Diamond, Bullet, and Arrow | 70.588 / 94.118 | Separate pair for each shape; fix Width |
| Each corner group's Radius | 0 / 4 | Separate pair for every group listed below; distinguish concave and convex corners |
| Each corner group's Segments | 1 / 8 | Separate pair for every group listed below; nonzero radius and wireframe overlay |
| Side Segments | 1 / 4 | Polygonal outline, long straight edges, source wireframe |
| Circle Diameter | 47.059 / 70.588 | Top view |
| Circle Vertices | 8 / 32 | Top/oblique wireframe |
| Banana Diameter | 94.118 / 141.176 | Fix Thickness and Angle |
| Banana Thickness | 15 / 24 | Fix centerline Diameter |
| Banana Angle | 60 / 120 | Top view |
| Banana Arc Vertices | 6 / 24 | Wireframe; fix End Vertices |
| Banana End Vertices | 2 / 8 | End close-up; fix Arc Vertices |
| Diamond Shear | 0 / 25 | Top view |
| Flash Width | 50 / 60 | Top view; keep all other dimensions valid |
| Flash Height | 125 / 150 | Top view |
| Flash Tip Width | 8 / 15 | Top view |
| Flash Notch Depth | 10 / 18 | Top view; less than half Width |
| Flash Step Height | −8 / 8 | Top view; label steps and arm overlap |
| Bullet Shoulder Height | 0 / 20 | Top view |
| Arrow Notch Depth | 0 / 10 | Flat base versus notched base |
| Arrow Notch Width | 14 / full Width | Narrow notch versus chevron; nonzero Notch Depth |
| Arrow Foot Angle | −25 / 25 | Nonzero notch narrower than Width; retain feet |
| Custom Source Mesh | None / valid wire; supplement with the matching filled surface | Same final outline; include open, non-flat, and multiple-boundary error states |
| Custom Corners Radius / Segments | Radius 0/2, then Segments 1/8 | Separate pairs; nonzero radius for sampling comparison |
| Custom Reload | Before / after source reimport and Reload | Source changed; show stale bake state afterward |
| Body Depth | 8 / 16 | Oblique/cutaway; fix channel depth |
| Channel | Off / On | Cutaway; use nonzero Width when on |
| Channel Width | 2 / 5 | Top and cutaway; keep below the outline's clamp |
| Channel Depth | 0 / 4 | Cutaway |
| Bottom Bevel Width | 0 / 1.5 | Channel width at least 3; cutaway/close-up |
| Bottom Bevel Segments | 1 / 6 | Nonzero bevel; wireframe and shaded comparison |
| Holes list | Empty / several | Top view, channel enabled |
| Hole X | 0 / 5 | Move one hole with all other fields fixed |
| Hole Y | 0 / 5 | Separate pair from X |
| Hole R | 2 / 4 | Keep the hole inside the available floor |
| Circle Segments | 6 / 24 | Hole close-up with wireframe |
| New Hole Radius | 2 / 4 | Apply Add Hole separately after setting each value |
| Around count | 4 / 8 | Apply Around separately; show both placement results |

Corner groups needing individual *Radius* and *Segments* pairs are Custom: *Corners*; Triangle: *Base Corners*, *Apex Corner*; Rectangle: *Corners*; Diamond: *Side Points*, *Top and Bottom*; Flash: *Tip Corners*, *Spike Corners*, *Notch Corners*; Bullet: *Tip*, *Shoulder Corners*, *Base Corners*; Arrow: *Tip*, *Notch Corner*, *Inner Corners*, *Feet*. For Bullet *Tip*, *Segments* covers its whole circular tip and rounds up to an even count; caption this exception.

Use the comments `geometry-triangle`, `geometry-rectangle`, `geometry-circle`, `geometry-diamond`, `geometry-custom`, and `geometry-holes` for the corresponding screenshots. Banana, Flash, Bullet, Arrow, and the body would benefit from the dimension diagrams below as well as their parameter pairs.

## Pattern comparisons

| Parameter / action | Suggested two values / states | View and prerequisites |
| --- | --- | --- |
| Pattern | None / Starburst, then None / Stippled, and None / Engraved Line | Same rectangle for the first two; Flash and Custom for engraving; source and baked views |
| Bursts | 8 / 24 | Starburst; fix Center and Offset |
| Center X | 0 / 30 | Starburst; fix Center Y |
| Center Y | 0 / 60 | Starburst; fix Center X; show an external fan center |
| Offset | 0.5 / 2 | Enough channel/body depth; oblique source and lit final view |
| Centre on the Arc | Before / After | Banana; use a visibly different initial Center |
| Stipple Height | 0.5 / 2 | Stippled; final normal relief and source side view |
| Stipple Width | 3 / 6 | Stippled; fix height and margin |
| Stipple Margin | 0 / 1 | Stippled; top close-up |
| Engraved Inset | 4 / 7 | Valid Flash; fix Width and Depth |
| Engraved Width | 1 / 3 | Keep the outer line edge inside the floor |
| Engraved Depth | 0.4 / 1.2 | Side section and reflected detail |
| Engraved Segments | 1 / 6 | Source trough close-up, then baked result |

Place these at `patterns-family`, `patterns-starburst`, `patterns-stipple`, and `patterns-engraving`. For stipples and engraving, include a final wireframe comparison with the ordinary no-pattern floor to establish that the relief is baked.

## Bake comparisons

| Parameter | Suggested two values / states | View and prerequisites |
| --- | --- | --- |
| Blender | Automatically detected / explicitly selected | Inspector; show a valid executable in both states |
| Texture Size | 512 / 2048 | Identical close-up and screen distance, both baked |
| Padding (px) | 2 / 16 | UV atlas with padded island edges highlighted |
| Edge Bevel | 0 / 1 | Reflected light at a sharp crease; fix Bottom Bevel |
| Grain Strength | 0 / 0.5 | Body reflection close-up, fix Grain Size |
| Grain Size | 0.5 / 3 | Nonzero Grain Strength; fix material Grain at zero |
| Unwrap in Blender | Disabled / Enabled | UV atlases and identical rendered view |
| Prefer UVPackMaster | Disabled / Enabled with add-on installed | Both packer readouts and atlases; do not imply an effect when unavailable |
| Bake on GPU | Disabled / Enabled | Configuration and successful bake states; rendered result should agree |
| Bake Hybrid Optics | Disabled / Enabled | Asset outputs and material state; enabled produces both optical maps |

Use `baking-settings` for these pairs. Illustrate the outputs and encodings at `baking-maps`, rather than requiring a reader to infer optical map meaning from their colors.

## Insert Light comparisons

| Parameter | Suggested two values / states | View and prerequisites |
| --- | --- | --- |
| Lamp N / Intensity | 0 / 1 | Repeat for each active lamp in isolation; same material |
| Number of lamps | 1 / 3 | Same shape; layout and corresponding rows |
| Name | Empty / Left | Inspector plus Lamp Manager's resulting item name |
| Color | Red / Blue | Equal intensity and neutral Lamp Emission; repeat for each lamp |
| Position X (mm) | 0 / 5 | Top view; fix Y/Z |
| Position Y (mm) | −10 / −20 | Two identical camera angles for each depth to reveal parallax |
| Position Z (mm) | 0 / 5 | Top view; fix X/Y |
| Tint With Lamp | Disabled / Enabled | Grey cover and colored lamp |
| Tint Amount | 0.25 / 1 | Tint enabled; saturated lamp |
| Spill Light | Unassigned / assigned spot | Ball above insert; body glow fixed |
| Lumens | 0 / 1.5 | Ball/nearby object; one active lamp and Max Brightness fixed |
| Range (m) | 0.04 / 0.1 | Nearby object spanning the two ranges |

Use `lighting-layout`, `lighting-tint`, and `lighting-spill`. *Bulbs* and *Position* in the material are the same stored layout as *Number of lamps* and *Position* here; use the same figure with both UI locations labeled. Capture *Radius* under the material reference.

## Body material comparisons

| Parameter | Suggested two values | Conditions |
| --- | --- | --- |
| Tint | Neutral white / saturated red | Lamps off; Clarity fixed |
| Clarity | 0 / 1 | Lamps off, neutral Tint, light Cavity Color |
| Cavity Color | Dark grey / light grey | Clarity 1; lamps off |
| Cavity Spread | 2 / 6 | Clear starburst, nonzero Cavity Contrast |
| Cavity Contrast | 0 / 0.5 | Clear starburst, lamps off |
| Min Brightness | 0.04 / 0.4 | Clarity 0, first lamp off |
| Smoothness | 0.4 / 0.93 | Body reflections visible; cover removed |
| Surface Normal Strength | 0 / 0.3 | Normal map assigned; Facet Flatten 0 |
| Facet Flatten | 0 / 0.85 | Starburst; reflected light; optics held fixed |
| Grain | 0 / 0.5 | Close-up; baked Grain Strength 0 to isolate color grain |
| Max Brightness | 1 / 20 | Lamp 1 at intensity 1; fixed exposure |
| Lamp Emission | Neutral / colored, equal HDR magnitude | Same runtime lamp Color; add a magnitude pair if useful |
| Radius (mm) | 4 / 10 | Same position and optical strengths |
| Strength | 0 / 1.5 | Baked Scatter left nonzero |
| Index of Refraction | 1.1 / 1.49 | Oblique view, tilted optical facets |
| Lamp Spread (mm) | 0 / 12 | Long insert, one lamp; inset extra example with spread 4/16 if needed |
| Baked Scatter | 0 / 0.5 | Fixed virtual Strength |
| Edge Glow | 0 / 0.2 | Channel and holes; fixed Scatter |
| Facet Contrast | 0 / 0.8 | Starburst; nonzero lamp or Scatter |
| Mottle Amount | 0 / 0.7 | Starburst; fixed Mottle Size |
| Mottle Size (mm) | 0.4 / 3 | Starburst; nonzero Mottle Amount |
| Variation Seed | 7 / 23 | Starburst mottle or procedural Grain enabled |
| Surface Normal | Own map / neutral map | Show the assignment and resulting reflection; restore correct map afterward |
| Optical Normals | Own map / neutral directions | Compare optical direction with surface reflections fixed; explanatory example only |
| Optical Response | Own map / neutral response | Map thumbnail and shader result; explanatory example only |
| Starburst Sectors | Correct count / mismatched count | Mottle enabled; label mismatch as diagnostic, not a recipe |
| Starburst Centre (mm) | Correct center / shifted center | Mottle enabled; vary X and Z separately |

Place groups at `material-plastic`, `material-surface`, `material-output`, `material-virtual-lamp`, `material-glow`, `material-variation`, and `material-baked-data`. Hidden runtime properties are demonstrated through the Insert Light component rather than through separate material asset edits.

## Cover comparisons

| Parameter | Suggested two values / states | Conditions |
| --- | --- | --- |
| Material | None / Insert Cover | Same body bake |
| Use Playfield Normals | Disabled / Enabled | Source has a distinctive scratch crossing the insert area |
| Playfield Override | Automatic source / alternate valid source | Show Detected Surface and the resulting projection |
| Opacity | 0.01 / 0.1 | Body pattern visible; same Haze Color |
| Haze Color | Light grey / dark grey | Opacity fixed above zero; lamp tint disabled |
| Smoothness | 0.4 / 0.96 | Reflection crossing playfield and top |
| Normal Map | None / scratch map | Use Playfield Normals disabled |
| Tiling X / Y | 1 / 4 per axis | Own normal map; independent pair for each axis |
| Offset X / Y | 0 / 0.25 per axis | Own normal map; independent pair for each axis |
| Normal Strength | 0 / 1 | Own map; optionally repeat with playfield projection to show multiplier behavior |

Use `cover-component`, `cover-haze`, and `cover-surface`. *Detected Surface*, *Source Material*, *Source Normal Map*, *Configured Spill*, and *UV fill* are readouts: show them with the corresponding changed source/configuration rather than presenting them as editable parameters.

## Preset asset comparisons

| Parameter | Suggested two values / states | What to capture |
| --- | --- | --- |
| Color Name | Teal / Cyan on separate custom assets | Button tooltip and newly generated material names |
| Order | 0 / 100 | Button position among other presets |
| Swatch | Dark / light | Button color; rendered material unchanged |
| Body Color | White / red | Newly created color materials; existing matches are reused |
| Cavity Color | Dark / light | Newly created clear body materials |
| Lamp Emission | Neutral / colored HDR | Newly created body material and resulting lamp |
| Cover Color | Neutral / colored | Newly created cover material; nonzero Opacity |
| Material preset Body Shader | Capture from matching shader / inspect stored shader name | Inspector metadata; do not imply shader replacement on apply |
| Material preset entry Name / Kind / Float / Vector | Two valid captures or edited numeric values | Show the corresponding material control changing; Color entries are excluded |

Preset creation comparisons must avoid reusing an existing material of the requested name, otherwise the preset's changed colors will not be applied to that existing asset. Document the starting material and generated name in the caption.

## Additional visualizations worth making

| Marker | Visualization | Why it helps |
| --- | --- | --- |
| `overview-layers` | Exploded cutaway of top cover, body, channel, pattern, virtual bulb, and real spot light | Establishes what each component/material owns before showing its Inspector. |
| `geometry-banana-measures` | Labeled centerline diameter, thickness, angle, and arc center | Avoids interpreting Diameter as the outer width of the band. |
| `geometry-flash-measures` | Labeled flash dimensions and corner groups | Makes Step Height and inward Notch Corners understandable at a glance. |
| `geometry-bullet-measures` | Tip circle and shoulder height overlaid on outline | Explains why Tip Radius differs from a normal corner fillet. |
| `geometry-arrow-measures` | Flat base, feet and notch, and full-width chevron | Shows why some controls become disabled. |
| `geometry-body-cutaway` | Cross-section with rim zero, Body Depth, Channel Depth/Width, and Bottom Bevel | Resolves the two depth controls and actual bevel location. |
| `patterns-starburst-wheel` | Whole radial fan intersecting a narrow outline | Explains why Bursts differs from visible rib count. |
| `patterns-stipple-cell` | Pyramid dimensions and omitted grid cells around a hole | Shows Margin and the whole-pyramid boundary rule. |
| `patterns-engraving` | Offset line and trough cross-section | Explains Inset versus Width, Depth, and Segments. |
| `baking-maps` | Source → target → three maps → shaded insert | Connects geometry, UV ownership, and the two normal encodings. |
| `lighting-parallax` | Short camera orbit, or stereo/two-view pair | A static intensity comparison cannot explain moving bulb images. |
| `material-spread` | Falloff curve with distance ticks and two colored overlapping lamps | Shows local lamp reach independently of real spill Range. |
| `cover-projection` | Playfield UV mapping and one scratch continuing across the cover | Explains the benefit of projection over hand-matched tiling. |

The most valuable additions are the **layered cutaway**, **body/channel depth cross-section**, **bake source versus target diagram**, and **short parallax orbit**. Ordinary side-by-side screenshots are sufficient for most remaining parameters.
