---
description: VPE's wire manager lets you directly hook up any switch to any coil or lamp.
---
# Wire Manager

Using the [Switch Manager](switch-manager.md), you can wire playfield and cabinet switches to the [Gamelogic Engine](../manual/gamelogic-engine.md). In the same way, the [Coil Manager](coil-manager.md) and the [Lamp Manager](#) let you connect playfield elements to the outputs of the Gamelogic Engine.

The **Wire Manager** allows you to *by-pass* the gamelogic engine and link switches to coils and lamps directly. This can be useful for debugging, but also for game logic that might not be covered by the gamelogic engine.

You can open the wire manager under *Visual Pinball -> Wire Manager*.

![Wire Manager](wire-manager.png)

## Setup

Every row in the wire manager's table corresponds to a wire connecting a switch to any element that takes an input. You can link multiple switches to one element or a single switch to multiple elements. In the following, we call the switch the *source* and the element it's linked to the *destination*.

### Description

The first column **Description** is optional. It's to help to better organize all the connections, but you can leave it empty if you want.

### Source

The **Source** column defines what kind of source you're linking to. There are four options:

- *Playfield* lets you choose any game item that qualifies as source from the playfield.
- *Input System* lets you choose an input action from a pre-defined list, e.g. cabinet switches.
- *Constant* sets the destination to a constant value.
- *Device* lets you choose a source device. Such devices are mechanisms that include multiple sources, for example [troughs](../manual/mechanisms/troughs.md).

### Source Element

The **Source Element** column is where you choose which element acts as source.

For **Playfield** sources, you can choose a game item that triggers switch events. Currently, VPE emits switch events for items that would do so in real life, i.e. bumpers, flippers, gates, targets, kickers, spinners and triggers.

If **Input System** is selected, you choose which input action to use. Actions may have default key bindings, but the final bindings to a key or other input will be defined in the host application (the VPE player).

If the source is a **Device**, then there are two values to select. The actual source device, and which switch of that device should be connected to the gamelogic engine.

Finally, if **Constant** is selected, you choose the value that will be permanently set at the beginning of the game. This might me useful for lamps that are always on.

### Destination

Under **Destination** you select the type of the element that will *receive* the switch changes. There are two options to choose from:

- *Playfield* lets you choose any game item that qualifies as destination from the playfield
- *Device* lets you choose a destination device. Such devices are mechanisms that include multiple coils or lamps, for example [troughs](../manual/mechanisms/troughs.md).

### Destination Element

The **Destination Element** column is where you choose which specifc element in the destination column should receiving switch changes. If *Device* was selected in the previous column, both the actual device as well as the element within the device has to be selected.

### Pulse Delay

Internally, VPE connects switches to events. Some switchable game items only emit the *switch closed* event. Such items are spinners or targets. They are elements where the re-opening of the switch doesn't have any semantic value.

In order for those items not to stay closed forever, VPE closes them after given delay. We call this the **Pulse Delay**. This field is only visible if the input source is pulse-driven source.
