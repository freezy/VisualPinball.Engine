# Troughs / Ball Drains

If you are unfamiliar with ball troughs, have a quick look at [MPF's documentation](https://mpf-docs.readthedocs.io/en/latest/mechs/troughs/), which does an excellent job explaining them.

VPE comes with a trough mechanism that simulates the behaviour of a real-world ball trough. This is especially important when emulating existing games, since the [gamelogic engine](../gamelogic-engine.md) expects the trough's switches to be in a plausible state, or else it may have errors.

## Creating a Trough

When importing a `.vpx` file that doesn't have any troughs (which is likely, because Visual Pinball doesn't currently handle them in the same way as VPE), VPE will automatically add a main trough to the root of the table. If you're creating a trough for a new game, click on the *Trough* button in the toolbox.

## Linking to the Playfield

<img src="trough-inspector.png" width="418" class="img-responsive pull-right" style="margin-left: 15px">

To interact with the game, you'll need to setup an entry kicker to drain the ball into the trough, and an exit kicker to release a new ball from the trough. This terminology may seem weird, since the ball *exits* the playfield when draining, but from the the trough's perspective, that's where the ball *enters*.

You can setup the kickers by selecting the trough in the hierarchy panel and linking them to the desired kickers using the inspector.

## Switch Setup

The number of simulated switches in the trough depends on the *Switch Count* property in the inspector panel. For recreations, you can quickly determine the number of trough switches by looking at the switch matrix in the operation manual, it usually matches the number of balls installed in the game.

Open the [switch manager](../../editor/switch-manager.md) and add the trough switches if they're not already there. As *Destination* select "Device", under *Element*, select the trough you've created and which switch to connect. For a five-ball trough, it will look something like this:

![Switch Manager](trough-switches.png)

## Coil Setup

VPE's trough supports two coils, an entry coil which drains the ball from the outhole into the trough, and an eject coil which pushes a new ball into the plunger lane. Open the [coil manager](../../editor/coil-manager.md), find or add the coils, and link them to the trough like you did with the switches:

![Coil Manager](trough-coils.png)