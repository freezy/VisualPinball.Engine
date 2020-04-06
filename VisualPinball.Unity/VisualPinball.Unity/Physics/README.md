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


