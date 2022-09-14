---
uid: score-motors
title: Score Motors
description: Simulate EM reel timing during gameplay
---

# Score Motors

Score Motors are used in electro-mechanical games to add points to a player's score. They consist of multiple cams that are stacked on top of each other. Each cam has different patterns around the edges, and switches sit at different positions in order to open or close at specific times when the motor runs and thus the cams rotate.

![Photo and schema of a score motor](score-motor-schema.jpg)
<small>*A typical score motor, found in Gottlieb and early Bally machines.*</small>

The score motor assembly sits typically at the bottom of the cabinet. The produced switch sequences are used when the game needs to do several things in a specific order. Although its main purpose is triggering the score reel relays, it is often used to drive other mechanisms as well.

## Scoring in an EM

There are two different modes of operation:

1. The player scores **single points**, e.g. one, ten, hundred, and so on. In this case, a pulse is directly sent to the coil driving the corresponding score wheel, which increases its position by one.
2. The player scores **multiple points**, like five, twenty, or 300. In this case, the score motor starts and the appropriate numbers of coil pulses are triggered by the switches around cams. For example, if a player scores fifty points, the score motor runs and enables a ten point relay to pulse five times. With each pulse of the ten point relay, the 10's score reel coil fires, which advances the score reel one position. 

Another property of a score motor is that it has no state, i.e. it doesn't know the actual score. This means that while the motor is running and the player scores *multiple* points, they are ignored. For *single* points, it depends on the machine, some allow single-point scoring while the motor is running, some don't.

> [!NOTE]
> For an in depth look at score motors, check out the fantastic article [Animated Score Motor circuits from EM Pinball Machines](https://www.funwithpinball.com/learn/animated-score-motor-circuits) at [Fun With Pinball](https://www.funwithpinball.com/).

## Player Experience

The way the scoring works results in a very particular timing of when exactly the score reels move during the game. Since in most games, chimes and bells are fired when the reel position changes, the player not only sees but also hears when points are scored. This means that accurate timing is essential for an authentic gaming experience.

# Setup

VPE comes with a component that accurately simulates the behavior described above. It handles score resets and add points all while performing accurate timing that can be specified by the table author.

To setup a score motor, select the table, click on *Add Component* in the inspector and select *Visual Pinball -> Mechs -> Score Motor*.

Next, configure the score motor. The inspector shows the following options:

<img src="score-motor-inspector.png" width="363" class="img-responsive pull-right" style="margin-left: 15px">

- **Steps** defines how many steps the score motor pulses for one turn.
- **Duration** defines the length of time (in milliseconds) it takes the score motor to completely cycle.
- **Block Scoring** defines if single point scoring is blocked *while the score motor is running*. As mentioned before, multiple point scores are always blocked while the score motor is running.
- **Increase by #** defines the behavior of the score motor for all of its the possible outputs. This gives the table author control over the timing and execution of `Wait` (pause) or `Increase` (add points) actions. The example in the screenshot shows a motor where when the player scores 30 points, it pulses on the first three actions of the score motor.

> [!NOTE]
> The minimum amount of `Steps` for a score motor is `5`. `Increase by 5` will not be shown under `Reel timing by increase` if `Steps` is set to 5, as all actions would be `Increase`.  

<img src="score-motor-gottlieb.png" width="335" class="img-responsive pull-right" style="margin-left: 15px">

By default, the score motor is configured to:

- 6 Steps
- 769 ms total run time

Next, associate the score motor with the [score reel display](xref:score-reels) by selecting it in [its inspector](xref:score-reels.html#score-reel-display).

# Usage

Score motors are primarily used in EMs, so we'll focus on how to use them through the [Visual Scripting game logic engine](xref:uvs_index). Programming a game with a score motor is a bit more complicated than with traditional displays for one reason: Scores might get blocked due to the motor being active, so you cannot solely rely on a score variable being updated.

To make this less cumbersome, we've added an [On Display Changed](xref:uvs_node_reference#on-display-changed) node, that emits the actual value of the display when it has been updated.

Give you've already set up your [score reel display](xref:score-reels), the recommended approach is the following:

1. Add an *Add Score* [event](xref:uvs_setup#events) in the Visual Scripting GLE's inspector.
   <img src="score-motor-score-event.png" width="357">
2. Add a *Score* [player variable](xref:uvs_variables#setup) in the same inspector.
   <img src="score-motor-score-variable.png" width="357">
3. In your graph, whenever you do scoring, use a [Trigger Pinball Event](xref:uvs_node_reference#trigger-pinball-event) node and set the *Add Score* event to be emitted.
   ![Score Event](score-motor-uvs-score-event.png)
4. In your graph, at a centralized location, create an [On Pinball Event](xref:uvs_node_reference#on-pinball-event) node, select the *Add Score* event, and link it to an [Update Display](xref:uvs_node_reference#update-display) node.
   ![Update Display](score-motor-uvs-update-display.png)
5. Just beneath, add an [On Display Changed](xref:uvs_node_reference#on-display-changed) node, select your score reel, and link the node to [Set Player Variable](xref:uvs_node_reference#set-variable) node, with *Score* to be updated.
   ![Update Score](score-motor-uvs-update-score.png)
6. Use the [Clear Display](xref:uvs_node_reference#clear-display) node when the game starts, in order to reset the score reels to zero.
   ![Reset Score](score-motor-uvs-reset-score.png)


This setup allows you to:

- Easily add scores in your game logic by triggering an *Add Score* event
- Subscribe to the *Score* variable in order to trigger score-dependent game logic, while taking into consideration eventually blocked scores by the motor.

Finally, you might want to hook up other events to the score motor's behavior. For example, in Gottlieb's Volley, some lamps are toggled off while the motor is running. In order to achieve that, the score motor component exposes two switches:

1. The **Motor Running** switch is activated when the motors starts and deactivated when it stops.
2. The **Motor Step Switch** pulses on each step.

In order to hook into those switches, you'll have to create them in the GLE inspector and link them to the corresponding switches in the [Switch Manager](xref:switch_manager).

<img src="score-motor-switch-manager.png" width="1044" class="img-responsive">

Then, in your graphs, add your logic behind the corresponding [On Switch Changed](xref:uvs_node_reference#on-switch-changed) node(s).