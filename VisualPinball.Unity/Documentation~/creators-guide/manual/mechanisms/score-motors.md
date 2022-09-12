---
uid: score-motors
title: Score Motors
description: Simulate EM reel timing during gameplay
---

# Score Motors

Score Motors are used in EM games to add multiple points to a player's score. For example, if a player scores 50 points, the score motor runs and enables a 10 point relay to pulse five times. With each pulse of the 10 point relay, the 10's score reel coil fires which advances the score reel one position.  

For an in depth look at score motors, check out the fantastic article [Animated Score Motor circuits from EM Pinball Machines](https://www.funwithpinball.com/learn/animated-score-motor-circuits) at [Fun With Pinball](https://www.funwithpinball.com/).

VPE comes with a score motor mechanism that simulates the behavior of a score motor. It handles score resets and add points all while performing accurate timing that can be specified by the table author.

# Setup

To setup a score motor, select the table, click on *Add Component* in the inspector and select *Visual Pinball -> Mechs -> Score Motor*.

<img src="score-motor-inspector.png" width="303" class="img-responsive pull-right" style="margin-left: 15px">

Next, configure the score motor.

The Score Motor inspector shows the following options:

- **Steps** defines how many steps the score motor pulses for one turn.
- **Duration** defines the length of time (in milliseconds) it takes the score motor to completely cycle.
- **Block Scoring** defines if single point scoring is blocked while the score motor is running.  Please note that all multiple point scores are always blocked while the score motor is running.

Reel timing by increase:

- **Increase by #** defines the behavior of the score motor for all of its the possible outputs.  This give the table author control over the timing and execution of `Wait` (pause) or `Increase` (add points) actions.  For example if the schematic shows that the table scores 30 points by pulsing on the first three actions of the score motor then the author can set the score motor like this.

INSERT IMAGE OF SCORE MOTOR SET TO PULSE ON STEPS 1,2,3 AND WAIT ON STEPS 0,4,5

> [!NOTE]
> The minimum amount of `Steps` for a score motor is `5`. `Increase by 5` will not be shown under `Reel timing by increase` if `Steps` is set to 5, as all actions would be `Increase`.  

<img src="score-motor-gottlieb.png" width="335" class="img-responsive pull-right" style="margin-left: 15px">

By default, the score motor is configured to:

- 6 Steps
- 769 ms total run time

Next, associate the score motor with the score reel display by selecting it in the Score Reel Display inspector:

<img src="score-motor-score-reel-display.png" width="318" class="img-responsive">

Optionally, use the [Switch Manager](xref:switch_manager) to associate the `Motor Running` and `Motor Step` switches the score motor exposes:

<img src="score-motor-switch-manager.png" width="1044" class="img-responsive">

# Usage

If a score reel display is [cleared](xref:uvs_node_reference#displays) or [updated](xref:uvs_node_reference#displays), the associated score motor will be automatically activated. 
