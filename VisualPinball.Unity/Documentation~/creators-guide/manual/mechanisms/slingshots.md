# Slingshots

Slinghots are usually located above the flippers. They consist of two blade switches that are located at the inner side of a triangle-shaped rubber. Between the switches there is a coil that propellers the ball in the opposite direction when either switch closes.

Visual Pinball doesn't have an explicit slingshot element. Instead, it relies on walls, where a segment can be marked as *slingshot*, with the effect of an additional force being applied to the ball when the segment is hit. However, the rubber animation is up to the table script to implement.

VPE provides a slingshot component that implements the rubber animation during runtime. This allows for functional slingshots without any additional code. Note however that this approach isn't ideal and will be replaced with a proper slingshot element in the future.


# Setup

## Wall


## Rubbers

> [!NOTE]
> In VPX, tables often come with three rubbers elements that are toggled in order to fake an animation. When using VPE's slingshot component, you can delete the rubber at mid position, since only the start and end rubbers are used. The interpolation is calculated in real time depending on the speed of the slingshot.


# Howtos

## Set Up a Slingshot from an Imported Table

This howto uses the blank table, but other tables should be quite similar. Usually, slingshots consist of three rubbers for the animation, plus a wall for the physics. What we need is the following:

1. The wall with a segment set to *slingshot*
2. The rubber at idle position
3. The rubber at *activated* position

Note that both rubbers must have the same number of drag points. This is because during the animation, the rubber is linearly interpolated between the two drag points positions, which isn't possible if the number differs. When converting a table from Visual Pinball, that means that you most probably need to add two additional drag points to the rubber at idle position.

### 1. Identify and clean up the elements

Zoom in to the slingshot you want to set up. You'll probably want to temporily hide the plastic that covers up the rubbers and the wall. Find the rubber at idle position and at activated position. Delete the rubber in-between, we don't need that one because VPE automatically interpolates between the two depending on the duration of the animation.

Note the names of those rubbers. Here it's `RSling` and `RSling1`. Also look for the wall that acts as the physical slingshot, which is called `RightSlingshot`.

### 2. Add additional drag points if necessary

Now, since `RSling1` is bent and thus contains three additional control points, we'll add the same points to `RSling`.

We can now also can hide the meshes of the rubbers.

### 3. Add the slingshot component

Any game object will do, but in order to keep things together, we'll add the slingshot component to the wall's game object. Select it, click on *Add component* in the inspector, and choose *Visual Pinball -> Elements -> Slingsho*.
