---
uid: mpf_index
title: Mission Pinball Framework
description:
  Visual Pinball Engine integration with the Mission Pinball Framework.
---

<img alt="MPF Logo" width="256" src="mpf-logo-full.png" />

# Mission Pinball Framework

[MPF](https://missionpinball.org/latest/about/) is an open-source framework
written in Python to drive real pinball machines. It has a "configuration over
code" approach, meaning that 90% of what you'd do in a pinball game can be
achieved through configuration (YAML files) rather than implementing it in code.

When you read MPF's [Getting Started](https://missionpinball.org/latest/start/)
page, you'll notice a banner stating that "MPF is not a simulator." Well, you've
found the simulator. ;)

This project lets you use MPF to drive game logic in
[VPE](https://github.com/freezy/VisualPinball.Engine), a pinball simulator based
on Unity. It does this by spawning a Python process running MPF and
communicating with VPE through [gRPC](https://grpc.io/).
