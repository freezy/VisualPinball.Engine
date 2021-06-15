# Changelog

*This documents all notable changes to the Visual Pinball Engine and its dependent projects.*

## Unreleased

Built with [Unity 2020.2](https://github.com/freezy/VisualPinball.Engine/pull/255).

### Added
- Native support for nFozzy flipper physics ([#305](https://github.com/freezy/VisualPinball.Engine/pull/305)).
- Automated camera clipping ([#304](https://github.com/freezy/VisualPinball.Engine/pull/304/files)).
- DMD and segment display support ([Documentation](https://docs.visualpinball.org/creators-guide/manual/displays.html)).
- Plugin: Mission Pinball Framework ([Documentation](https://docs.visualpinball.org/plugins/mpf/index.html))
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
- Plunger is now a coil device, meaning it can both be pulled back and fired through different inputs.
- Move render pipelines into separate repos ([#259](https://github.com/freezy/VisualPinball.Engine/pull/259)).
- Put game-, mesh-, collision- animation data into separate components ([#227](https://github.com/freezy/VisualPinball.Engine/pull/227), [Documentation](https://docs.visualpinball.org/creators-guide/editor/unity-components.html)). 

### Fixed
- Alpha channel of color values is now correctly written ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Layer names are correctly computed when importing a 10.6 file ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Clear texture and material references that don't exist before writing (VP 10.7 behavior) ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- Bug in writing animation vertices which caused VP to hang when re-reading the file ([#291](https://github.com/freezy/VisualPinball.Engine/pull/291)).
- A few bugs in drag point gizmos ([#246](https://github.com/freezy/VisualPinball.Engine/pull/246)).
