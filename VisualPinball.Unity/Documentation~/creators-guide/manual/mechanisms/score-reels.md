---
uid: score-reels
title: Score Reels
description: How to use EM-style reels to display the score.
---

# Score Reel Displays

<img src="score-reels.jpg" width="350" alt="Score Reels a of a Gottlieb Volley" class="img-responsive pull-right" style="margin-left: 15px"/>

In electro-mechanical games, score reels are very common for displaying the player score. Typically, four to six units are mounted behind the backglass. Each reel is driven by a coil that advances the reel by one position when pulsed. The coils are driven by the playfield elements in the game, often through a score motor for multi-point scoring.

VPE includes components that simulate the [score motor](xref:score-motors) and render the score reel animation. This page is about the score reel, which presents itself to the [GLE](xref:gamelogic-engine) as a *display* that takes in the "numerical" frame format (i.e. numbers only). The score motor is an optional component that provides accurate timing when animating the reels.

## Setup

Typically you would drop the desired score reel variant from the asset library into your scene. But you can also set it up manually.

A score reel display consists of two separate components.

1. The *Score Reel Display* component, which represents the logical display that takes in a number and then sets the reels to display that number.
2. The *Score Reel* component, which represents one single reel and handles the animation.

### Model

The best geometry for a score reel is a simple, open cylinder. Make sure the local origin is in the middle, and that it rotates on the Z-axis.

![Score reel geometry](score-reels-geometry.jpg)

The texture should contain the numbers 0-9, each taking up 36°. The order (and thus the direction of rotation) depends on the game, so both are valid, and can be configurated later.