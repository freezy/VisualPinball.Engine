---
uid: uvs_node_reference
title: Visual Scripting Node Reference
description: Reference documentation for all VPE-specific nodes.
---

# Node Reference

This page details all the VPE-specific nodes that we have created for visual scripting.

You can recognize VPE nodes easily by their color; they are orange. When creating new nodes, VPE event nodes can be found under *Events/Visual Pinball*, and other nodes simply under the root's *Visual Pinball*.

## Coils

### Set Coil

This node assigns a given value to one or multiple coils, and keeps that value. This is useful when both the *enabled* and *disabled* status are important. Otherwise, use the *Pulse Coil* node, which enables a coil, and automatically disables it after a short delay.

A typical use case for this node is linking the flipper coil to a switch event. Here an example of a game that has an upper flipper and a lower flipper, both linked to the same *left flipper* switch.

### Pulse Coil

This node enables one or multiple coils, and disables them after a given delay. This is useful when you only care about the "enabled" event, which usually the case. Here an example of three droptarget banks being reset after the last target was dropped.