![Banner](https://docs.visualpinball.org/creators-guide/introduction/jp-header.png)
# Visual Pinball Engine

*A library that implements world's favorite pinball simulator.*

[![build](https://github.com/freezy/VisualPinball.Engine/workflows/Build/badge.svg)](https://github.com/freezy/VisualPinball.Engine/actions?query=workflow%3ABuild) [![codecov](https://codecov.io/gh/freezy/VisualPinball.Engine/branch/master/graph/badge.svg?token=gyLOj3al3T)](https://codecov.io/gh/freezy/VisualPinball.Engine) [![UPM Package](https://img.shields.io/npm/v/org.visualpinball.engine.unity?label=org.visualpinball.engine.unity&registry_uri=https://registry.visualpinball.org&color=%2333cf57&logo=unity&style=flat)](https://registry.visualpinball.org/-/web/detail/org.visualpinball.engine.unity)

## Why?

Today we have nice game engines like Unity or Godot that support C# out of the
box. The goal of VPE is to easily provide what Visual Pinball makes so great to
other "current gen" engines, while keeping backwards-compatibility.

VPE also aims to significantly improve the editor experience by extending the 
editor of the game engine.

For a more detailed overview, header over to the [website](https://docs.visualpinball.org/creators-guide/introduction/overview.html)!

## How?

The "core" of VPE (i.e. the `VisualPinball.Engine` project) is a pure C# port
of the original Visual Pinball. It has no dependencies to any proprietary third
parties, and provides the data layer.

We're currently focusing on Unity as a game engine. Visual Pinball's physics
engine has been ported to [DOTS](https://unity.com/dots), and we're focusing
on Unity's [HDRP](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@10.0/manual/index.html)
for the visuals.

## Dependency Graph

This repository is part of a number of packages. It is what we're referring to as the **main package**.

![image](https://user-images.githubusercontent.com/70426/103706031-64db6080-4fac-11eb-837e-5e7cddd86d7b.png)

## Current Status

VPE is still work in progress. You can check the current features list [here](https://docs.visualpinball.org/creators-guide/introduction/features.html)
and the open issues [here](https://github.com/freezy/VisualPinball.Engine/issues).

There are a few videos in the [VPF thread](https://vpuniverse.com/forums/topic/5362-wip-visual-pinball-in-unity-2021-edition/), 
where you can discuss. Screenshots are [here](https://github.com/freezy/VisualPinball.Engine/wiki/Unity-Screenshots)! :)

## Credits

<a title="IntelliJ IDEA" href="https://www.jetbrains.com/idea/"><img src="https://raw.githubusercontent.com/vpdb/server/master/assets/intellij-logo-text.svg?sanitize=true" alt="IntelliJ IDEA" width="250"></a>

Special thanks go to JetBrains for their awesome IDE and support of the Open Source Community!

## License

Since [4616dcbb](https://github.com/freezy/VisualPinball.Engine/commit/4616dcbb), [GPL-3.0](LICENSE). Before [4616dcbb](https://github.com/freezy/VisualPinball.Engine/commit/4616dcbb), [GPL-2.0](https://github.com/freezy/VisualPinball.Engine/blob/32fd8f48d11ba961b50c72cd7f82fc4c34eba26e/LICENSE).

