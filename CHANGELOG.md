# Changelog

*This documents all notable changes to the Visual Pinball Engine and its dependent projects.*

## Unreleased

Built with Unity 6.5

### Added

- An arrow insert shape: an arrowhead on two feet with a notch cut up into its base, set by the notch's depth and width and the tilt of the feet, with fillets at the tip, the notch, the inner corners and the feet; it covers the scooped arrow, the arrowhead with a hollow base and the chevron, and fills its width and height exactly.
- A bullet insert shape: a flat base, straight sides up to the shoulders, and flanks that lean in as tangents to a round tip, with fillets on the shoulders and the base corners; the width and height are exact.
- A flash insert shape, the lightning bolt: width, height, tip width, notch depth and step height, with fillets of their own on the tips, the spikes and the notches; it fills its width and height exactly.
- A diamond insert shape, with its own fillets on the side points and on the top and bottom, and a shear that leans it over; the fillets and the shear leave the width and height exactly as set.
- A banana insert shape: a band bent round a circle of a given diameter, with rounded ends, set by its thickness, angle and vertex counts, with a button that centres the starburst on the circle; every option of the other shapes works on it.
- A circle insert shape, drawn as a regular polygon of a chosen diameter and vertex count, with every option of the other shapes.
- The insert's channel is optional: without it the floor is the outline itself, straight down, flat or with the starburst; holes and the bottom bevel need a channel. Depth is now the floor's own depth, with the channel sunk below it.
- The insert's starburst can be switched off, keeping the raised floor flat, and its holes are listed one per line.
- The Insert and Insert Light inspectors are grouped into sections with a tooltip on every control, controls that would do nothing greyed out, and a top view of the insert's outline, raised floor, starburst and holes.
- Insert bakes record the insert they were made for, so a duplicated insert, wherever it ends up, bakes into a folder of its own instead of over the original's.
- Inserts whose bake lives in a read-only package have their bake options locked, with Fork to Project copying the bake into the project as a material variant that keeps following the package's tuning and baking there from then on.
- Insert Light is directly selectable in Lamp Manager and light groups, with ROM on/off, fading, RGB channels, wire events and editor previews driving both the lens and spill light.

- Fine surface grain in the insert normal bake, with adjustable strength and size in VPX units; optical bulb normals and the clear cover remain independent.

- Optional clear insert cover in a second material slot, using any assigned material or automatically detecting and aligning the playfield's normal map across insert position, rotation and scale changes.
- Hybrid insert materials expose Min Brightness for a darker, adjustable unlit body while the bulb and spill remain off at zero intensity.
- Optional hybrid insert optics bake: neutral cavity illumination and sharp optical normals for the HDRP virtual lamp shader, with face contrast, stable optical variation, independent runtime RGB/fading, and insert brightness controls linked to a real spill light.
- Spring-hinge bash toys with a finite-inertia analytic box collider, reciprocal Spatial magnet holds, packaged player reconstruction, editor setup/handles, and a Play Mode qualification sample.
- Motion Transform input ranges for animating a follower during only part of its source travel.
- Wire Rail component with native spline authoring, fixtures (rings, rungs, cradles, stands, hairpins, and elbows), and an inferred ball-channel collider ([Documentation](https://docs.visualpinball.org/creators-guide/editor/wire-rails/index.html)).
- Make packaging functional ([#557](https://github.com/freezy/VisualPinball.Engine/pull/557))
- New threading model ([#552](https://github.com/freezy/VisualPinball.Engine/pull/552))
- Free transformation ([#500](https://github.com/freezy/VisualPinball.Engine/pull/500))
- Kinematic collisions ([#460](https://github.com/freezy/VisualPinball.Engine/pull/460))
- Flipper tricks by nFozzy ([#436](https://github.com/freezy/VisualPinball.Engine/pull/436))
- Asset Library now has thumbnails.
- Documentation for score reels.
- Score Motor Component ([#435](https://github.com/freezy/VisualPinball.Engine/pull/435), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/score-motors.html)).
- Scale support for rubbers.
- Slingarm coil arms can now be any game objects, not just primitives ([#432](https://github.com/freezy/VisualPinball.Engine/pull/432)).
- Gate Lifter Component ([#418](https://github.com/freezy/VisualPinball.Engine/pull/418), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/lifting-gates.html)).
- Asset Browser ([#412](https://github.com/freezy/VisualPinball.Engine/pull/412))
- Trigger meshes can now be easily scaled ([#374](https://github.com/freezy/VisualPinball.Engine/pull/374))
- We got a new game item called *Metal Wire Guide* (thanks @Cupiii, [#366](https://github.com/freezy/VisualPinball.Engine/pull/366))
- A *Collision Switch* component ([#344](https://github.com/freezy/VisualPinball.Engine/pull/344), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/collision-switches.html)).
- A *Rotator* component ([#337](https://github.com/freezy/VisualPinball.Engine/pull/337), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/rotators.html)).
- A *Teleporter* component ([#336](https://github.com/freezy/VisualPinball.Engine/pull/336), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/teleporters.html)).
- A *Drop Target Bank* component ([#333](https://github.com/freezy/VisualPinball.Engine/pull/333), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/drop-target-banks.html)).
- Editor: Enable manual trigger for coils, switches, lamps and wires during gameplay ([#332](https://github.com/freezy/VisualPinball.Engine/pull/332))
- Support for dynamic wires, also known as *Fast Flip* ([#330](https://github.com/freezy/VisualPinball.Engine/pull/330), [Documentation](https://docs.visualpinball.org/creators-guide/editor/wire-manager.html#dynamic)).
- Component for light groups, allowing easy grouping of GI lamps. ([#330](https://github.com/freezy/VisualPinball.Engine/pull/330) [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/light-groups.html)).
- Slingshot component ([#329](https://github.com/freezy/VisualPinball.Engine/pull/329), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/slingshots.html)).
- Create insert meshes ([#320](https://github.com/freezy/VisualPinball.Engine/pull/320)).
- Full support for custom playfield meshes.
- Remove Hybrid Renderer ([#316](https://github.com/freezy/VisualPinball.Engine/pull/316)).
- Create and use Unity assets when importing ([#320](https://github.com/freezy/VisualPinball.Engine/pull/302)).
- Native support for nFozzy flipper physics ([#305](https://github.com/freezy/VisualPinball.Engine/pull/305)).
- Automated camera clipping ([#304](https://github.com/freezy/VisualPinball.Engine/pull/304/files)).
- DMD and segment display support ([Documentation](https://docs.visualpinball.org/creators-guide/manual/displays.html)).
- Plugin: Mission Pinball Framework ([Documentation](https://docs.visualpinball.org/plugins/mpf/index.html)).
- Gamelogic Engine: Support for hardware rules ([#293](https://github.com/freezy/VisualPinball.Engine/pull/293)).
- Support for Extended ASCII strings ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Support for Elasticity Falloff in walls (added in VP 10.7) ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Support for table notes (added in VP 10.7) ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Slow motion during gameplay ([#288](https://github.com/freezy/VisualPinball.Engine/pull/288)).
- [Lamp Manager](https://docs.visualpinball.org/creators-guide/editor/lamp-manager.html) ([#282](https://github.com/freezy/VisualPinball.Engine/pull/282)).
- The VPE core is now also available on [NuGet](https://www.nuget.org/packages/VisualPinball.Engine/).
- VPE is now packaged and published on every merge!
- Native trough component ([#229](https://github.com/freezy/VisualPinball.Engine/pull/229), [#248](https://github.com/freezy/VisualPinball.Engine/pull/248), [#256](https://github.com/freezy/VisualPinball.Engine/pull/256), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/troughs.html)).

### Changed

- Renamed Actuator and Actuator Transform to Motion Controller and Motion Transform in components, APIs, editor menus, and documentation, preserving existing scene references, package identifiers, and coil mappings.
- Removed DOTS in favor of Jobs with Burst ([#459](https://github.com/freezy/VisualPinball.Engine/pull/459))
- All geometry is now in world space.
- Removed internal ID in gamelogic engine API ([#408](https://github.com/freezy/VisualPinball.Engine/pull/408))
- When importing, meshes are now saved as easily editable `.fbx` files instead of Unity's internal format ([#387](https://github.com/freezy/VisualPinball.Engine/pull/387)).
- Revised rubber mesh generation ([#384](https://github.com/freezy/VisualPinball.Engine/pull/384)).
- APIs for RGB lamps and Visual Scripting ([#382](https://github.com/freezy/VisualPinball.Engine/pull/382)).
- Playfield is now rotated to the correct angle during gameplay ([#370](https://github.com/freezy/VisualPinball.Engine/pull/370)).
- Decouple light components from transformation override ([#350](https://github.com/freezy/VisualPinball.Engine/pull/350)).
- Refactored drag points. They are nicely separated and typed now.
- Collider debug view is now much faster and intuitive. It's also activated per default when there is no visible mesh.
- Drop and hit targets are now different components.
- Kicker is now a coil device with different coils for different angles/forces.
- Ground truth of data is now the scene, not the imported data anymore ([#302](https://github.com/freezy/VisualPinball.Engine/pull/302)).
- Plunger is now a coil device, meaning it can both be pulled back and fired through different inputs.
- Move render pipelines into separate repos ([#259](https://github.com/freezy/VisualPinball.Engine/pull/259)).
- Put game-, mesh-, collision- animation data into separate components ([#227](https://github.com/freezy/VisualPinball.Engine/pull/227), [Documentation](https://docs.visualpinball.org/creators-guide/editor/unity-components.html)).

### Removed

- The legacy VPX table patcher and its import option, including `IPatcher`, `PatcherManager`, the render-pipeline material-adapter APIs, and the `applyPatch` import parameters.

### Fixed

- Insert rebakes correctly restore the generator UV layout when Blender produces folded UVs, preventing false fallback success followed by overlapping-texel errors.

- Hybrid inserts expose normalized Intensity and Color on the component, driven by ROM lamps at runtime, with unrestricted Max Brightness on the material and matching environmental spill.

- Insert geometry edits rebuild tangent data to prevent HDRP blackouts, keep the current material visible, and show a rebake warning.
- Disappearing objects due to wrong bounding box ([#441](https://github.com/freezy/VisualPinball.Engine/pull/441)).
- Default table import ([#434](https://github.com/freezy/VisualPinball.Engine/pull/434))
- Remaining ball spinning issue should now be solved ([#397](https://github.com/freezy/VisualPinball.Engine/pull/397)).
- Physics error when the ball would stop rotate ([#393](https://github.com/freezy/VisualPinball.Engine/pull/393)).
- Finally, ball rotation is rendered correctly ([#386](https://github.com/freezy/VisualPinball.Engine/pull/386)).
- Ball stuttering when rolling over dropped target ([#375](https://github.com/freezy/VisualPinball.Engine/pull/375)).
- Plunger disappearing due to too small bounding box.
- Fixed switch status when multiple mappings point to the same ID ([#347](https://github.com/freezy/VisualPinball.Engine/pull/347)).
- Lighting setup. It's now usable ([#330](https://github.com/freezy/VisualPinball.Engine/pull/330)).
- Ball passing through collider plane and disappearing.
- Alpha channel of color values is now correctly written ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Layer names are correctly computed when importing a 10.6 file ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Clear texture and material references that don't exist before writing (VP 10.7 behavior) ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Bug in writing animation vertices which caused VP to hang when re-reading the file ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- A few bugs in drag point gizmos ([#246](https://github.com/freezy/VisualPinball.Engine/pull/246)).
