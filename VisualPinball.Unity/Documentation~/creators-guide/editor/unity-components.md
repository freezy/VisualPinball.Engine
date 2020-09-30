# Unity Components

When loading or creating a table in Unity, what you're creating is a hierarchy of [GameObjects](https://docs.unity3d.com/Manual/GameObjects.html). By default we group game items by their type, but you can arrange them however you want.

In order to give the GameObjects behavior during gameplay, we add [Components](https://docs.unity3d.com/Manual/Components.html) onto them. VPE comes with large amount of Components that are used to set up the game mechanics of the table.

> [!note]
> During runtime, VPE converts the GameObjects and Components into Unity's [DOTS](https://unity.com/dots) elements. So we use Components in the editor to define the game logic implemented in DOTS. That's also why we call them *Authoring Components*.

If you've never heard about GameObjects or Components, we recommmend you read through the links in the first two paragraphs, they are short, to the point, and better than what we could provide here.

## Components vs Game Items

In Visual Pinball, Components would be what you see in the options panel when you click on a game item. You'll typically find sections for physics behavior, rendering, and form and shape of the game item you're editing. Internally, all those sections belong to the same game item.

In VPE, we have separate Components for separate things. For example, a rubber might be visible on the playfield, so its GameObject includes a *Rubber Mesh Component*. And it collides with the ball, so there is a *Rubber Collision Component* as well. And of course it has a position and a name, which are part of what we call the "main component", in this case the *Rubber Component*.

This separation of logic comes with a lot of advantages, as you will see later.

> [!note]
> In general, you don't have to manually manage all this. When creating game items via the toolbox, the created GameObject will already have all the necessary components. Also, importing a `.vpx` file applies the necessary components.


## Component Types

Here's a quick overview of which Components VPE provides to set up your game items.

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

As you can see, there are three types of Components for each game item:

- **Mesh Components** generate meshes, i.e. the geometry used to render the object on the playfield. The result are procedurally generated visuals, e.g. a flipper mesh would be based on the length, inner- and outer radius and a few more attributes.
- **Collision Components** add physics behavior to the game item. They define how the ball collides with it, i.e. how the bounciness, friction and randomness is applied to the ball.
- **Movement Components** apply animation to parts of the game item. If the entire object is moved (for example a flipper), this is taken care of by the collision Component, but items where only parts move (e.g. the plate of a gate, or the socket of a bumper), these components apply the movement to the GameObject.

## Combining Components

VPE allows to mix and match the different types of components. That means, for a game item, you can use a collider or mesh from another type.

## Naming Conventions
