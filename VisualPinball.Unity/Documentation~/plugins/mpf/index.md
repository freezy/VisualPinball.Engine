---
title: VPE - Mission Pinball Framework
description: Visual Pinball Engine integration with the Mission Pinball Framework.
---

![mpf logo](https://raw.githubusercontent.com/missionpinball/mpf/dev/mpf-logo-200.png)

# Mission Pinball Framework

VPE connects to MPF through [gRPC](https://grpc.io/), which is a high-performance, low-latency RPC framework. It works by VPE launching a Python process with MPF which spawns the gRPC server, to which VPE connects to.

There are two situations when this is done:

- In edit mode in order to retrieve the available switches, coils and lamps
- During runtime to drive the game

VPE supports MPF's *hardware rules*, which are dynamic connections between coils and switches handled by the controller boards in order to reduce latency. The media controller is not yet supported.