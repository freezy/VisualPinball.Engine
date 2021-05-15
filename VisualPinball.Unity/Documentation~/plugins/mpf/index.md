---
title: Mission Pinball Framework
description: Visual Pinball Engine integration with the Mission Pinball Framework.
---

<img alt="MPF Logo" width="256" src="mpf-logo-full.png" />

# Mission Pinball Framework

MPF is a rich framework that allows to easily implement game logic for existing or completely new pinball machines. It's mature, very well documented, and actively maintained.

VPE connects to MPF using [gRPC](https://grpc.io/), which is a high-performance, low-latency RPC framework. It works by VPE launching MPF as a Python process. MPF will then spawn a gRPC server, to which VPE connects to.

There are two situations when this is done:

- In edit mode to retrieve available switches, coils and lamps
- During runtime to drive the game

VPE supports MPF's *hardware rules*, which are dynamic connections between coils and switches handled by the controller boards in order to reduce latency. The media controller is not yet supported.
