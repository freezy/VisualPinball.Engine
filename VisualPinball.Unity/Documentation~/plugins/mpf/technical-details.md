---
title: Technical details
description: How VPEs MPF integration works under the hood
---

# Technical details

VPE connects to MPF using [gRPC](https://grpc.io/), which is a high-performance,
low-latency RPC framework. When using MPF, VPE acts as a hardware controller.
MPF does not "know" there is no real playfield.

When VPE starts up, it will:

1. Launch MPF as a Python process
2. Create a gRPC client
3. Connect to MPF
4. Inform MPF about the starting states of all switches
5. Query the machine description and check whether it matches the description
   saved earlier
6. Tell MPF to start the game

At runtime:

- VPE sends an update to MPF whenever a switch is opened or closed
- MPF sends commands to VPE to control coils, lights, and displays
- The `MpfGamelogicEngine` executes those commands

When VPE shuts down, it will:

1. Notify MPF that it is shutting down
2. Disconnect from MPF and shut down the client
3. Wait until MPF shuts down (or kill the process if it does not shut down
   within one second)

## Included MPF binaries

VPEs MPF integration comes with a custom prebuilt version of MPF that is
slightly different from the official version. VPEs version of MPF supports a
_Ping_ RPC that allows VPE to check whether MPF is ready without any
side-effects (like starting the game). This way, the game can start as soon as
MPF is ready, regardless of how long MPF takes to start up. We have
[proposed this change](https://github.com/missionpinball/mpf/pull/1865) to the
MPF developers, but as of February 2025 they have not yet gotten around to
including it in the official version. Therefore, you will need to set the
_Startup Behaviour_ of the `MpfGamelogicEngine` to _Delay Connection_ if you
want to use any official version of MPF.

## Hardware rules

VPE supports MPF's _hardware rules_, which are dynamic connections between coils
and switches that are handled by the hardware controller boards in real pinball
machines in order to reduce latency, but they are useful for VPE as well. For
example, when the player presses the flipper button on the keyboard, VPE already
knows that the flipper coil should be activated and does not need to wait for a
response from MPF, because MPF has previously told VPE that the flipper switch
should activate the flipper coil. When the table goes into attract mode, MPF
removes the rule and the flippers no longer work. This means that even though
MPF runs in a separate process, using it to control your VPE table **does not
increase latency**.

## Media controller

The media controller is not yet supported.
