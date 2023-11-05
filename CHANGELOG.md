# Changelog

*This documents all notable changes to the Visual Pinball Engine and its dependent projects.*

## Unreleased

Built with Unity 2022.3.x

### Added

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

### Fixed
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
