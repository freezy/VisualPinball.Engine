# Unity Components

When loading or creating a table in Unity, what you're creating is a hierarchy of [GameObjects](https://docs.unity3d.com/Manual/GameObjects.html). By default we create a level for every game item type, but you can arrange them however you want.

In order to give the GameObjects behavior during gameplay, we add [Components](https://docs.unity3d.com/Manual/Components.html) onto them. VPE comes with large amount of Components that are used to define the gameplay of the table.

> [!note]
> During runtime, VPE converts the game objects and components into Unity's [DOTS](https://unity.com/dots) elements. So we use Components in the editor to define the game logic implemented in DOTS. That's also why we call them *Authoring Components*.

If you've never heard about GameObjects or Components, we recommmend you read through the links in the first two paragraphs, they are short, to the point, and better than what we could provide here.

## Components vs Game Items

In Visual Pinball, Components would be what you see in the options panel when you click on a game item. You'll typically find sections for physics behavior, rendering, and form and shape of the game item you're editing. Internally, all those sections belong to the same game item.

In VPE, we have separate Components for separate things. For example, a rubber might be visible on the playfield, so its GameObject includes a *Rubber Mesh Component*. And it also collides with the ball, so there is a *Rubber Collision Component* as well. And of course it has a position and a name, which are part of what we call the "main component", in this case the *Rubber Component*.

This separation of logic comes with a lot of advantages, as you will see later.

> [!note]
> In general, you don't have to manually manage all this. When creating game items via the toolbox, the created GameObject will already have all the necessary components. Also, importing a `.vpx` file applies the necessary components.


## Collider Types

|            | Mesh | Collision       | Movement |
|------------|------|-----------------|----------|
| Bumper     |      |                 |          |
| Flipper    |      |                 |          |
| Gate       |      |                 |          |
| Hit Target |      |                 |          |
| Kicker     |      |                 |          |
| Light      |      |                 |          |
| Plunger    |      |                 |          |
| Primitive  |      |                 |          |
| Ramp       |      |                 |          |
| Rubber     |      | RubberCollider  |          |
| Spinner    |      |                 |          |
| Surface    |      | SurfaceCollider |          |
| Trigger    |      |                 |          |

*What we've implemented so far*.


