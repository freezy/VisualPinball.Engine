![Banner](https://docs.visualpinball.org/creators-guide/introduction/jp-header.png)
# Visual Pinball Engine

*A library that implements world's favorite pinball simulator.*

[![build](https://img.shields.io/appveyor/build/freezy/visualpinball-engine?style=flat-square)](https://ci.appveyor.com/project/freezy/visualpinball-engine)
[![build](https://img.shields.io/appveyor/tests/freezy/visualpinball-engine?compact_message&style=flat-square)](https://ci.appveyor.com/project/freezy/visualpinball-engine)

## Why?

Today we have nice game engines like Unity or Godot that support C# out of the
box. The goal of VPE is to easily provide what Visual Pinball makes so great to
other "current gen" engines, while keeping backwards-compatibility.

VPE also aims to significantly improve the editor experience by extending the 
editor of the game engine.

## How?

The "core" of VPE (i.e. the `VisualPinball.Engine` project) is a pure C# port
of the original Visual Pinball. It has no dependencies to any proprietary third
parties, and provides the data layer.

We're currently focusing on Unity as a game engine. Visual Pinball's physics
engine has been ported to [DOTS](https://unity.com/dots), and we're focusing
on Unity's [HDRP](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@10.0/manual/index.html)
for the visuals.

## Current Status

VPE is still work in progress. You can check the current features list [here](https://docs.visualpinball.org/creators-guide/introduction/features.html)
and the open issues [here](https://github.com/freezy/VisualPinball.Engine/issues).

There are a few videos in the [VPF thread](https://www.vpforums.org/index.php?showtopic=43651), 
where you can discuss.

## Credits

<a title="IntelliJ IDEA" href="https://www.jetbrains.com/idea/"><img src="https://raw.githubusercontent.com/vpdb/server/master/assets/intellij-logo-text.svg?sanitize=true" alt="IntelliJ IDEA" width="250"></a>

Special thanks go to JetBrains for their awesome IDE and support of the Open Source Community!

## License

Since [4616dcbb](https://github.com/freezy/VisualPinball.Engine/commit/4616dcbb), [GPL-3.0](LICENSE). Before [4616dcbb](https://github.com/freezy/VisualPinball.Engine/commit/4616dcbb), [GPL-2.0](https://github.com/freezy/VisualPinball.Engine/blob/32fd8f48d11ba961b50c72cd7f82fc4c34eba26e/LICENSE).

