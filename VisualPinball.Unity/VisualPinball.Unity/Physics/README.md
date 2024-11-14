# Visual Pinball Physics

*How physics work in Visual Pinball.*

## Static Collisions

In terms of physics, all objects but balls are static. This section describes
how collisions between the ball(s) and the static objects are handled. To
simplify, ball-ball collisions are ignored here and explained in the next
section.

### Initialization

When the runtime boots, a [KD-Tree](https://en.wikipedia.org/wiki/K-d_tree)
is created based on the game items. The KD-Tree consist of *Hit Objects*, which
are usually primitive shapes such as points, lines, circles or lines. A game 
item can produce hundreds of Hit Objects depending on its complexity.

The goal of the KD-Tree is to rapidly discard Hit Objects that are out of range
for the ball to be hit (broad phase), in order to concentrate on calculating
an exact hit time for those who are (narrow phase).

Once the KD-Tree is set up, it doesn't change anymore for the rest of the game.
That doesn't mean that nothing moves (which is obviously not the case), it just
means that the bounding boxes of the Hit Objects don't change and thus the tree
structure remains the same.

### Gameplay

Let's look at what happens during a frame. We're in the render loop, and we get
a delta time since the last frame (typically 16.6 milliseconds). The physics 
loop runs at 1kHz (or 1000 cycles per second), so now, the physics loop 
simulates in one-millisecond steps what happened since the last frame.

#### 1. Process Inputs

First, key presses are processed. For example, if the flipper button was 
pressed, the flipper's solenoid state would be set to `on`.

#### 2. Update Velocities

Secondly, velocities of the movable Hit Objects are updated.

There are four different types of movable game elements: The plunger, gates,
flippers, and spinners (technically, the ball is a movable as well). These
game elements have special Hit Objects that are dependent on the game item's
movement when being hit. 

So updating velocities means that whatever movement these objects are in (or if
movement just started due to a key press), they are updated for the current
frame.

#### 3. Simulate Cycle

Lastly, and this is the main part of the physics engine, Visual Pinball enters
another loop which does the following:

1. Calculate the next collision time for each ball
2. Update displacements of all movables
3. Collide the balls 
4. Handle ball contacts

This loop is repeated until no more balls are hit. For example, if in the 1000
microseconds of the physics step, a flipper is hit at 700 microseconds, these
four phases are repeated for time at 700 microseconds, to obtain more precise
results (although below 5 microseconds is ignored).

It's also worth noting that if a flipper collides with its stop (meaning it 
just finished moving), the loop starts with that time directly (optimization,
I guess?).

So let's look at each of those four phases individually.

##### 3.1 Calculate Next Collision

What happens here is that for each ball, the KD-Tree is used to check if the
ball's [AABB](https://en.wikipedia.org/wiki/Minimum_bounding_box#Axis-aligned_minimum_bounding_box)
overlaps with the AABB of a Hit Object. If that's the case, the hit time is
calculated. The Hit Object with the smallest hit time wins.

But that's just the hit, meaning the ball touched something with some minimal
velocity. If the ball is within a certain distance of a Hit Object, it is 
considered a *contact*. These contacts get stored while calculating the hit
time of all the Hit Objects.

Both matches are stored as *Collision Events*. The next "hit" gets a Collision
Event linked to the ball, while the "contact" gets a Collision Event saved
globally so it can be handled in phase 4 (the ball is also linked to the
Collision Event, which in the case of the hit is redundant, because here the
event links already to the ball). 

##### 3.2 Update Displacements

This phase only concerns the movable game items. For example, if the Hit Object
of a gate was hit in (the previous cycle's) phase 4, the plate would have
received a new velocity. This phase calculates the new rotation angle of the 
plate.

It also emits events to the scripting engine.

##### 3.3 Collide the balls

For each ball that has an assigned Hit Object it collides with, the collision 
is now resolved. This is specific to each Hit Object and its physical 
properties such as elasticity and scatter. 

The result is each ball getting a new position and velocity.

##### 3.4 Handle ball contacts

Lastly, the recorded contacts from phase 1 are handled. This includes applying
gravity and friction based on the normals of the contact to each ball.

## Dynamic Collision

The only dynamic collision in Visual Pinball is collision between balls. For
this, another KD-Tree is re-created on each physics cycle. Then, during phase
3.1, balls are not only checked against static objects, but also against other
balls.

# VPE Physics

While VPE uses the same formulas and heuristics to resolve collisions and simulate 
behavior as VPX, we've made some changes to make it more flexible. Here's a quick
overview.

- **Kinematic Objects**: In VPX, as described above, the only objects that can be 
  freely transformed within the physics world are the balls, all other objects are
  static. In VPE, you can mark any object as kinematic, which means it can be moved
  freely at runtime.
- **Unrestricted Transformations**: In VPX, every type of object has its limitations
  how it can be transformed. For example a flipper must always be parallel to the
  playfield. In VPE, you can rotate, move and scale objects freely.
- **World Space**: VPX defines its own way of measuring units. It defines a unit as
  50th of the diameter of a ball (1 VP unit = 0.02125" = 0.53975mm). VPE uses real 
  world units (meters) and converts them to VP units at runtime for the physics
  simulation only. This means we get the advantages of real world units, but can still
  rely on the heuristics of the physics engine that has been tuned over the years.

> [!NOTE]  
> In VPE, we don't use the term *Hit Object*. We call them *Colliders*.

## Unrestricted Transformations

The key to transforming objects lies in how their transformations are stored. In
VPX, due to its restrictions, transformations are generally stored as separate
values. In Unity (and game engines in general), transformations are stored as 
matrices. Instead of converting between the transformations that the physics 
engine understands and the transformation matrix — and thereby restricting the 
editor's transformation tools to those limitations — we decided to rely exclusively
on the transformation matrix. This approach allows complete freedom in transforming
objects.

It also enables VPE to support parenting objects to others, allowing 
transformations of parent objects without disrupting the physics simulation.

So, how can the VPX physics code handle arbitrary transformations? The solution is a 
simple trick: rather than transforming the colliders, we transform the balls: To 
resolve a collision between a ball and a collider that is transformed in a 
non-supported way, we temporarily move the ball into the collider's local space, 
resolve the collision, and then return the ball to the playfield space.

### Collider Data

Our colliders don't and won't know about our transformation matrices, i.e. the
data used to calculate the simulation is still the same as in VPX. So, we need to
get this data from the transformation matrices into the colliders. The chosen
approach is the following:

- Each collider gets a `Transform(float4x4)` method that transforms the collider
  in the playfield space. Transforming means updating the collider's position, 
  rotation, and scale, whatever is supported. That's the data the VPX physics code 
  uses.
- This comes with restrictions as mentioned above. For example, a Line collider, 
  by definition, is aligned parallel and orthogonally to the playfield. In this case 
  we'd transform its two points and retrieve their new xy position. This works for 
  translate and scale, but rotating around anything else than the z-axis would still
  result in a rectangle parallel and orthogonal to the playfield, which wouldn't be 
  desired.
- But more on that problem later. What's important is that for transformations 
  *supported by the VPX physics code*, we have a method that allows to transform  
  each collider, based on a matrix.
- Additionally, colliders are instantiated without a transformation matrix. That means  
  by default, they are placed at the origin and have no rotation or scale.
- Finally, each collider gets a `TransformAABBs(float4x4)` method that only transforms
  the collider's axis-aligned bounding boxes.
  
So, with all of the above, we do the following when the game starts:

1. We retrieve the overall world-to-local matrix of an item.
2. Using the playfield's world-to-local matrix, we calculate the playfield-to-local matrix
   of the item.
3. We check which kind of transformation the VPX physics code supports for the type of
   collider and compare it to the transformation of playfield-to-local matrix.
   - If all transformations are supported, we simply transform the collider with the item's
	 transformation matrix.
   - If not, we check whether this collider might be replaceable by another type of collider
     that supports the transformation. For example, a line collider can be replaced by two 
     triangle colliders, which then are 100% transformable.
   - If neither of the above is possible, we fall back to ball transformation trick,
     which is to transform the ball during collision resolution. Note that the AABBs of the
     object still need to be transformed, so the broad phase can correctly sort out the 
     items that are out of range.

This relatively simple approach gives us an incredible amount of flexibility. We now have a
true 3D physics engine that can handle any kind of transformation, including parenting, while
still being able to rely on the heuristics of the VPX physics code that has been tuned over 
the years.

### Code Changes

Let's dive into the code. We'll need three more methods for each collider:

1. `IsTransformable(float4x4)`: This method checks whether the collider can be transformed
   with the given matrix, i.e. if the physics code supports the transformation natively.
2. `Transform(float4x4)`: This method transforms the collider in VPX space.
3. `TransformAABBs(float4x4)`: This method transforms the collider's axis-aligned bounding boxes.

In our `ColliderReference` class, we'll add a `float4x4` to each `Add()` method. This method goes
through the three use cases described above.
   - Using `IsTransformable(float4x4)`, it'll determine whether the collider can be transformed
     natively and uses `Transform(float4x4)` if that's the case.
   - Otherwise, it'll check whether the collider can be replaced by another collider, converts it
     and transforms the converted collider(s) instead.
   - Otherwise, it only transforms its AABBs and marks the collider as non-transformable by
     setting the `IsTransformed` of the collider to `false`. It also stores the transformation 
     matrix so it can be used during runtime for the ball transformation trick.

## Coordinate Systems

Give the above, we'll be transforming between multiple coordinate systems, or *spaces*:

- **World Space**: This is the space in which the game objects are defined. It's also the
  space in which you should model your assets.
- **Playfield Space**  (or VPX space): This is the space in which the physics engine operates.
- **Local Space**: This is the space of the collider. For elements that are transformed in a way
  not supported by the physics engine, we'll move the ball into this space to resolve the collision.

## Kinematic Objects

Kinematic objects need to be enabled manually.

> [!NOTE]  
> Maybe, in the future, we could leverage Unity's "static" flag for this.
