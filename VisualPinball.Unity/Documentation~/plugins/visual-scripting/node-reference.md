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

![Get Switch](get-switch-example.png)

## Lamps

## Variables

See [Variables](xref:uvs_variables) for an overview on how variables work. We will be using examples for player variables, but apart from [creation](#create-player-state) and [changing](#change-player-state), they work the same way as table variables.

### Create Player State

This node adds variables for a new player. If the game starts and you want to use player variables, you need to create a player state, even if your game has only one player. 

You would typically do it when the game starts. Multiplayer games would execute this node when *start* is pressed during the first ball.

This node has the following options:

- **Auto-Increment** automatically sets the player ID. It does that by increasing the largest existing player ID by one.
- **Player ID** is only visible if auto-increment is not set, and lets you specify the player ID.
- **Set as Active** will make the newly created player state the current state. This makes sense when a new game is started, but not when new players are added.
- **Destroy Previous** deletes all player states before creating the new one. This is useful when starting a new game.

Here an example of the player state being created right after the state machine enters the game state, i.e. when the game starts.

![Create Player State](create-player-state-example.png)

> [!note]
> This one of two nodes that doesn't exist for table variables.

### Change Player State

This nodes swaps out the current player variables with the ones from another player. You do this when a player has finished playing and it's the next player's turn.

The following options are available:

- **Next Player** automatically choses the next player. If the current player is the last player, the next player is the first player. You typically enable this option when using *auto-increment* during player state creation. It means that VPE handles the player IDs.
- **Player ID** lets you explicitly set the ID of the player you want to change to (only visible of *Next Player* is disabled).

The following is an example of a multiplayer game with infinite balls (i.e. remaining balls are not checked). The flow starts with the drain switch, which then checks whether the current player has any extra balls left. If that's not the case, then the player state is changed to the next player, otherwise the number of extra balls is decreased instead, and finally the eject coil of the trough is pulsed.

![Change Player State](change-player-state-example.png)

> [!note]
> This the second node that doesn't exist for table variables.

### Get Player ID

This node gives you access to the player ID. There are three different modes:

- *Current* returns the ID of the current player
- *First* return the smallest player ID
- *Last* returns the largest player ID

A typical example is shown in the [next section](#get-variable).

### Get Variable

This node returns the value of a given variable. To build on the previous example, let's do a check whether we should end the game if a ball was drained.

To do that, we retrieve the player variable *Current Ball Number* and check if it's the same as the global variable *Balls per Game*. If that's the case, we assume that it's the last ball. Then we compare the current player ID to the last player ID. The final *And* node checks if both conditions are true, and what comes out is whether we should end the game or not.

![Get Variable](get-variable-example.png)

### Set Variable

This node applies a given value to a variable. It's very straightforward. Here an example of a trigger enabling the lit status of a bumper.

![Set Variable](set-variable-example.png)

### Increase Variable

More often than not, you want to increase a variable by a given value rather than setting an absolute value. This node does exactly that, for integer and float typed variables. For string types, it concatenates the value to the current one. For boolean types, it inverts the current value, *if* the input value is `true`.

A typical example for this node is scoring. This example adds 1000 points to the score when the bumper switch is enabled.

![Increase Variable](increase-variable-example.png)

### On Variable Changed

One of the main advantages of using VPE's variable system is that you get events when they change. That makes it easy to separate how the variable is updated from what effect updating it causes. That's great, because you shouldn't care *why* a variable was updated, only *when* and *to which value* ([see also](xref:uvs_variables#synchronizing-state)).

In this example, we listen to the score variable and fetch it into our *Update Display* node, which sends the data to our score reel component, which then rotates the reels accordingly. Note that you'll also get the previous value of the variable, before it changed.

![On Variable Changed](on-variable-changed-example.png)
