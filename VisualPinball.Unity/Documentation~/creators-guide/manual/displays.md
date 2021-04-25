---
description: How VPE handles the dot matrix display and segment displays.
---
# Displays

Every pinball machine has some sort of display where the score, usually among other things, is displayed. In the 80s, mostly numeric [7-segment displays](https://en.wikipedia.org/wiki/Seven-segment_display) where used, which were upgraded in the 90s with alpha-numeric [16-segment](https://en.wikipedia.org/wiki/Sixteen-segment_display) and [dot matrix](https://en.wikipedia.org/wiki/Dot-matrix_display) displays.

![DMD](dmd-game_over.jpg)
<small>*A dot matrix display used in the late 90s - Photo © 2009 by [ElHeineken](https://commons.wikimedia.org/wiki/File:Pinball_Dot_Matrix_Display_-_Demolition_Man.JPG)*</small>

VPE supports segment displays as well as dot matrix displays (the latter are also called DMDs). During game play, displays are driven by the [Gamelogic Engine](gamelogic-engine.md). VPE supports multiple displays per game.

> [!note]
> While the first electro-mechanical pinball machines were using score motors, recent machines today are using high resolution LCDs. Both of these types are not yet supported in VPE.

## Setup

Displays are lazily bound, meaning it's when the game starts that the Gamelogic Engine announces its displays and VPE connects them to the objects in your scene that actually render them. Matching is done with an ID and depends on how the Gamelogic Engine deals with displays. 

For example, in [MPF](../../plugins/mpf/index.md) you name your displays yourself in the machine configuration, while PinMAME uses IDs like `dmd0` and `display0` to identify its DMDs and segment displays.

### Editor

<img src="display-add-component.png" width="243" alt="Add display component" class="img-responsive pull-right" style="margin-left: 15px"/>

VPE provides two display components, a segment display and a DMD component. Both components create the underlying geometry and apply the shader that renders the content of the display. In order to create one, create an empty game object in your scene, and add the desired component under *Visual Pinball -> Display*.

Or even more easily, create the game object with the already assigned component by right-clicking in the hierarchy and choosing *Visual Pinball -> Dot Matrix Display*. This will place the display into your scene right behind your playfield.

<img src="display-dmd-inspector.png" width="354" alt="DMD Inspector" class="img-responsive pull-right" style="margin-left: 15px"/>

Selecting the game object will let you customize it in the inspector, but more importantly, it lets you set the ID that links it to the Gamelogic Engine.

### Runtime

You've noticed that the inspector lets you customize parameters like DMD resolution or number of segment columns in the editor that affected the geometry of the display. This is useful, because it allows you to see the correct geometry without running the game and place the display where it fits in the scene.

However, during runtime, these parameters are provided by the Gamelogic Engine and the displays are reconfigured as soon as they are received. This means that if you've set the wrong number of chars in your segment display, it will be resized and look differently than in the editor.

> [!note]
> There are additional settings that don't affect geometry that aren't configurable in the editor but will be automatically set during gameplay, such as number of segments per column.