---
title: PinMAME Setup
description: How to set up PinMAME with VPE.
---

# Setup

## Prerequisites

You'll currently need to have [VPinMAME](https://github.com/vpinball/pinmame/releases) (VPM) installed on your system, because we'll look into the VPM folder for the ROM file. You'll also need to have the ROM files of the games you're using this plugin with.

## Unity Setup

The PinMAME integration comes as an UPM package. In Unity, add it by choosing *Window -> Package Manager -> Add package from git URL*:

<p><img alt="Package Manager" width="294" src="../../creators-guide/setup/unity-package-manager.png"/></p>

Then, input `org.visualpinball.engine.pinmame` and click *Add* or press `Enter`. This will download and add PinMAME to the project. 

> [!NOTE]
> You will need to have our scoped registry added in order for Unity to find the PinMAME package. How to do this is documented in the [general setup section](../../creators-guide/setup/installing-vpe.md#vpe-package).

So let's [test it](usage.md).
