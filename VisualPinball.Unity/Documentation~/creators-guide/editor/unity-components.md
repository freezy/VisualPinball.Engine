# Unity Components

When loading or creating a table in Unity, what you're creating is a hierarchy of [GameObjects](https://docs.unity3d.com/Manual/GameObjects.html). By default we group game items by their type, but you can arrange them however you want.

In order to give the GameObjects behavior during gameplay, we add [components](https://docs.unity3d.com/Manual/Components.html) onto them. VPE comes with large amount of components that are used to set up the game mechanisms of the table.

> [!note]
> During runtime, VPE converts the GameObjects and components into Unity's [DOTS](https://unity.com/dots) entities. So we use components in the editor to define the game logic implemented in DOTS. That's also why we call them *Authoring Components*.

If you've never heard about GameObjects or components, we recommmend you read through the links in the first two paragraphs. They are short, to the point, and better than what we could provide here.

## Components vs Game Items

In *Visual Pinball*, components would be what you see in the options panel when you click on a game item. You'll typically find sections for physics behavior, visuals, and form and shape of the game item you're editing. Internally, those sections belong to the same game item.

In *VPE*, we have separate components for separate things. There are four different types of components:

1. The **Main Component** represents the actual game item.
2. The **Collider Component** adds physical behavior to the game item. It defines how the ball collides with it, i.e. how the bounciness, friction and randomness is applied to the ball.
3. **Mesh Components** generate meshes, i.e. the geometry used to render the object on the playfield. The result are procedurally generated visuals.
4. **Animation components** apply movement to the game item. If the entire object is moved (for example a flipper), this is taken care of by the collision component, but items where only parts move (e.g. the plate of a gate, or the ring of a bumper), these components apply the movement to the GameObject.

Let's look at a flipper:

![VPE vs VP](properties-vpe-vs-vp.png)

Here, we see the main component (*Flipper*), the collider component (*Flipper Collider*), and two mesh components (*Flipper Base Mesh* and *Flipper Rubber Mesh*). While the main- and collider component sit on the same GameObject, the mesh components have each their own child. This is how Unity works - a GameObject has at most one mesh component.

> [!note]
> Internally, VPE still keeps a single set of data. That's why you see the collapsed *Base Mesh*, *Rubber Mesh* and *Physics* sections in the main component. When you change values there, the corresponding values in the other components will update at the same time.

This separation of logic comes with a few advantages. First, it's more obvious how a game item behaves. No collider component? That means the game item is not collidable. No mesh component? It's (permanently) invisible. But there are other advantages, as you will see in the next section.

> [!note]
> In general, you don't have to manually manage all this. When creating game items via the toolbox, the created GameObject will already have all the necessary components (as will importing a `.vpx` file).

## Combining Components

VPE allows to mix and match components of different game items. For example, for a given game item, you can assign a collider or mesh from another type. The most common use case is replacing built-in meshes with primitives. For this, you would remove the original mesh component and replace it with a primitive mesh component. But there are other usages, like using a primitive collider on a rubber. You can also have *multiple* children with colliders (or meshes) for a game item. 

We call this **parenting**. The game item that overrides a given behavior is still created, but *parented* to another game item.

The advantage compared to Visual Pinball where you would create individual game items is that VPE treats them as one single logical entity. For example, VPE will automatically rotate a primitive flipper item when it's parented under a flipper. Or events from multiple colliders will be emitted on the same parent object.

### Supported Combinations

Not every game item can be parented any other game items. For example it doesn't make much sense to use a flipper collider for a bumper. In fact, most of the combinations are unsupported. Here's what VPE does support so far:

|                  | Supported Meshes                                              | Supported Colliders        | Supported Animators       |
|------------------|---------------------------------------------------------------|----------------------------|---------------------------|
| **Bumper**       | Bumper Base, Bumper Cap, Bumper Ring, Bumper Skirt, Primitive | Bumper                     | Bumper Ring, Bumper Skirt |
| **Flipper**      | Flipper Base, Flipper Rubber, Primitive                       | Flipper                    |                           |
| **Gate**         | Gate Bracket, Gate Wire, Primitive                            | Gate                       | Gate Wire                 |
| **Hit Target**   | Hit Target, Primitive                                         | Hit Target                 | Hit Target, Drop Target   |
| **Kicker**       | Kicker, Primitive                                             | Kicker                     |                           |
| **Light** (bulb) | Light, Primitive                                              |                            |                           |
| **Plunger**      | Flat Plunger, Plunger Rod, Plunger Spring                     | Plunger                    | Plunger                   |
| **Primitive**    | Primitive                                                     | Primitive, Ramp, Wall      |                           |
| **Ramp**         | Ramp, Primitive                                               | Ramp                       |                           |
| **Rubber**       | Rubber, Primitive                                             | Rubber, Surface, Primitive |                           |
| **Spinner**      | Spinner Bracket, Spinner Plate, Primitive                     | Spinner                    | Spinner Plate             |
| **Surface**      | Surface, Primitive                                            | Surface, Primitive         |                           |
| **Trigger**      | Trigger, Primitive                                            | Trigger                    | Trigger                   |


## Naming Conventions

In order to keep backward compatibility with Visual Pinball, VPE relies on naming conventions to parent a game item to another.

There are two suffixes (text you add *after* the game item's name) that have special meaning in VPE:

- `_Mesh` applies the game item's mesh to its parent
- `_Collider` applies the game item's collider to its parent

For example, if in Visual Pinball, you name a primitive `LeftFlipper_Mesh`, VPE will look for a `LeftFlipper` game item and replace its mesh with the mesh of `LeftFlipper_Mesh`. In other words, it will *parent* `LeftFlipper_Mesh` to `LeftFlipper` and disable `LeftFlipper`'s mesh.

Another example: If in Visual Pinball, you name a rubber `LeftSlingshot` and two primitives `LeftSlingshot_Collider_Soft` and  `LeftSlingshot_Collider_Hard`, VPE will disable the collider of `LeftSlingshot` and use the colliders of both primitives. During gameplay when the ball hits either `LeftSlingshot_Collider_Soft` or `LeftSlingshot_Collider_Hard`, the `Hit` event will be emitted on `LeftSlingshot`.

> [!warning]
> When you *export* to `.vpx` and you have parented items but didn't follow the naming convention, the parenting will get lost when re-importing the table into VPE. In the future, VPE might propose to rename the parented children or just do it on export, but that's still on our TODO list.

## Visibility

In order to determine whether a game item is visible, VPE looks at the hierarchy and the mesh components of its game object. If a game item has no mesh component, its visibility is automatically set to *invisible*. It's also invisible if the game object is set to inactive (the top left checkbox in the inspector).
