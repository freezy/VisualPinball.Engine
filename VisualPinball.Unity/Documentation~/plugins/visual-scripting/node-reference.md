---
uid: uvs_node_reference
title: Visual Scripting Node Reference
description: Reference documentation for all VPE-specific nodes.
---

# Node Reference

This page details all the VPE-specific nodes that we have created for visual scripting.

You can recognize VPE nodes easily by their color; they are orange. When creating new nodes, VPE event nodes can be found under *Events/Visual Pinball*, and other nodes simply under the root's *Visual Pinball*.

Besides the simple read/write/event nodes, there are a bunch of nodes that solve common patterns in pinball games. While you could implement the same logic using Unity's standard nodes, we recommend using those custom nodes, because they save you space and thus increase the readability of your graphs.

However, it's hard to use them without knowing about them, so we recommend reading through this page in order to familiarize with them.

## Coils

### Set Coil

This node assigns a given value to one or multiple coils, and keeps that value. This is useful when both the *enabled* and *disabled* status are important. Otherwise, use the [*Pulse Coil*](#pulse-coil) node, which enables a coil, and automatically disables it after a short delay.

A typical use case for this node is linking the flipper coil to a switch event. Here an example of a game that has an upper flipper and a lower flipper, both linked to the same *left flipper* switch.

![Set Coil](set-coil-example.png)

As seen in the screenshot, you can set the number of affected switches in the header of the node. Increasing the number will add additional ports below.

### Pulse Coil

This node enables one or multiple coils, and disables them after a given delay. This is useful when you only care about the "enabled" event, which often the case. Here an example of the eject coil of the trough being pulsed when the *running* state is entered.

![Pulse Coil](pulse-coil-example.png)


## Switches

### On Switch Enabled

This is probably the most common switch event you'll use. It triggers when any switch of a list of switches is *enabled*.

Here is an example of the drain switch increasing the *current ball* variable.

![On Switch Enabled](on-switch-enabled.png)


### On Switch Changed

The other switch event triggers in both cases, when the switch is enabled, and when it gets disabled. The classic example already mentioned above are the flipper buttons.

![Set Coil](set-coil-example.png)


### Get Switch Value

This node just returns the current switch value of a given switch. While usually you should rely on player and table variables for saving and retrieving status, it still has its usage. For example, you might want to not add the state of a kicker to the variables and rely on the kicker switch directly instead.

