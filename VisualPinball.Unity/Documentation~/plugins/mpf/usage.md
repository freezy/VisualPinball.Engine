---
title: MPF Usage
description: How to use the Mission Pinball Framework with VPE.
---

# Usage

MPF support is implemented through a [Gamelogic Engine](../../creators-guide/manual/gamelogic-engine.md). It's a [Unity Component](https://docs.unity3d.com/Manual/Components.html), so all you'll have to do is add it to the root node of your table.

You do that by selecting the table in the hierarchy, then click *Add Component* in the inspector and select *Visual Pinball -> Game Logic Engine -> Mission Pinball Framework*.

<p><img alt="Package Manager" width="354" src="unity-add-component.png"/></p>


## Retrieve Machine Description

Since the gamelogic engine is the part of VPE that provides switch, coil and lamp definitions so VPE can link it to the table during gameplay, you'll need to retrieve them from MPF. 

You can do that by clicking on *Get Machine Description* the MPF component's inspector. This will save it to component, so unless you update the machine config itself, you only need to do it once.

> [!NOTE]
> While VPE could read the MPF machine config itself, we let MPF handle it. That means we run MPF with the given machine config and then query its hardware. 
>
> While this is a bit slower, it has the advantage of coherent behavior between edit time and runtime, and doesn't add an additional maintenance burden.

## Wire It Up

Now VPE knows which switches, coils and lamps your machine expects, you'll need to connect them using the [switch](../../editor/switch-manager.md), [coil](../../editor/coil-manager.md), and [lamp manager](../../editor/lamp-manager.md).

You can watch the entire process in a quick video here:

> [!Video https://www.youtube.com/embed/cdzvMUpdDgs]