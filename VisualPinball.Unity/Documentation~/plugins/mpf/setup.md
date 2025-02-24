---
title: MPF Setup
description: How to set up the Mission Pinball Framework with VPE.
---

# Setup

> [!NOTE]
>
> You will need to have our scoped registry added in order for Unity to find the
> MPF package. How to do this is documented in the
> [general setup section](/creators-guide/setup/installing-vpe.html#vpe-package).

Mission Pinball Framework integration comes as a UPM package. In Unity, add it
by choosing _Window -> Package Manager -> Add package from git URL_:

<p><img alt="Package Manager" width="253" src="../../creators-guide/setup/unity-package-manager.png"/></p>

Then, input `org.visualpinball.engine.missionpinball` and click _Add_ or press
`Enter`. This will download and add MPF itself and VPE's MPF integration to the
project.

So let's [test it](usage.md).
