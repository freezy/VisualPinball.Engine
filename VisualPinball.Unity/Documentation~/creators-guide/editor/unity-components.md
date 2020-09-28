# Unity Components

When loading or creating a table in Unity, what you're creating is a hierarchy of [GameObjects](https://docs.unity3d.com/Manual/GameObjects.html). By default we create a level for every game item type, but you can arrange them however you want.

In order to give the GameObjects behavior during gameplay, we add [Components](https://docs.unity3d.com/Manual/Components.html) onto them. VPE comes with large amount of Components that are used to define the gameplay of the table.

> [!note]
> During runtime, VPE converts the game objects and components into Unity's [DOTS](https://unity.com/dots) elements. We use Components in the editor to define the game logic implemented in DOTS. That's also why we call them *Authoring Components*.

If you've never heard about GameObjects or Components, we strong suggest you read through the links in the first two paragraphs, they are short, to the point, and better than what we could provide here.

## Components vs Game Items

In Visual Pinball, Components would be what you see in the options panel when you click on a game item. You'll typically find sections for physics behavior, rendering, and form and shape of the game item you're editing. Internally, all those section belong to the same game item.

In VPE, we have separate Components for separate things. For example, a rubber might be visible on the playfield, so its GameObject has a *Rubber Mesh Component*. And it also collides with the ball, so there is a *Rubber Collision Component* as well. And of course it has a position and a name, which are part of what we call the "main component", in this case the *Rubber Component*.

In general, you don't have to manually manage all this. When creating a rubber via the toolbox, the created GameObject will already have all the necessary components.

