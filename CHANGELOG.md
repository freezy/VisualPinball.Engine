# Changelog

*This documents all notable changes to the Visual Pinball Engine and its dependent projects.*

## Unreleased

Built with Unity 2020.3.

### Added
- A *Teleporter* component ([#336](https://github.com/freezy/VisualPinball.Engine/pull/336), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/drop-target-banks.html)).
- A *Drop Target Bank* component ([#333](https://github.com/freezy/VisualPinball.Engine/pull/333), [Documentation](https://docs.visualpinball.org/creators-guide/manual/mechanisms/teleporters.html)).
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
- Refactored drag points. They are nicely separated and typed now.
- Collider debug view is now much faster and intuitive. It's also activated per default when there is no visible mesh.
- Drop and hit targets are now different components.
- Kicker is now a coil device with different coils for different angles/forces.
- Ground truth of data is now the scene, not the imported data anymore ([#302](https://github.com/freezy/VisualPinball.Engine/pull/302)).
- Plunger is now a coil device, meaning it can both be pulled back and fired through different inputs.
- Move render pipelines into separate repos ([#259](https://github.com/freezy/VisualPinball.Engine/pull/259)).
- Put game-, mesh-, collision- animation data into separate components ([#227](https://github.com/freezy/VisualPinball.Engine/pull/227), [Documentation](https://docs.visualpinball.org/creators-guide/editor/unity-components.html)). 

### Fixed
- Lighting setup. It's now usable ([#330](https://github.com/freezy/VisualPinball.Engine/pull/330)).
- Ball passing through collider plane and disappearing.
- Alpha channel of color values is now correctly written ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Layer names are correctly computed when importing a 10.6 file ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Clear texture and material references that don't exist before writing (VP 10.7 behavior) ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Bug in writing animation vertices which caused VP to hang when re-reading the file ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- A few bugs in drag point gizmos ([#246](https://github.com/freezy/VisualPinball.Engine/pull/246)).
