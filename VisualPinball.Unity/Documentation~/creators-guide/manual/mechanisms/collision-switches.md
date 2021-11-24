---
uid: collision-switches
title: Collision Switches
description: VPE supports collisions switches, which turn hittable game objects into switch devices.
---

# Collision Switches

A Collision Switch turns a hittable game object into a switch device. Example's of hittable game objects are Walls, Rubbers, and Primitives.

## Setup

To create a Collision Switch:

- Add the Collision Switch directly to a hittable game object. Select the game object you want to add it to, click on *Add Component* in the inspector and select *Visual Pinball -> Mechs -> Collision Switch*. 

To associate the collision switch with a game logic engine switch, use the [Switch Manager](xref:switch_manager) and select the switch in the *Element* column.

## Runtime

During gameplay, the inspector shows you the status of the switch. 
