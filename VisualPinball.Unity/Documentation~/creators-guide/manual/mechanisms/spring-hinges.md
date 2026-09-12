---
uid: spring_hinges
title: Spring Hinge Bash Toys
description: Author a spring-returning toy with reciprocal ball impact and an optional moving magnet.
---

# Spring Hinge Bash Toys

A Spring Hinge simulates a rigid toy rotating about one fixed axis. Balls push its analytic box with finite inertia, and its spring and damping return it toward an equilibrium angle. An optional child Spatial magnet can attract and carry one ball while applying the equal reaction torque to the toy.

## Create a Toy

For an existing model, make the rotating object's local origin the physical pivot, select that object, and choose **GameObject > Pinball > Add Spring Hinge**. VPE adds Spring Hinge and Spring Hinge Collider to the selected object and disables its independent colliders. Other selected moving parts are parented below the active object while preserving their world poses. Keep fixed brackets outside this hierarchy, then use the scene handles to set the axis, centre of mass, analytic box, and travel limits.

Choose **GameObject > Pinball > Spring Hinge Bash Toy** to create a complete example object with a cube visual, analytic box, and owned magnet. The bash preset starts at 0 degrees and stops at 20 degrees with a low-elasticity box.

Spring Hinge Collider also applies the physics angle directly to the rotating object around its local origin. It uses the Hinge Axis from Spring Hinge, caches the authored local rotation when play starts, and treats that pose as zero degrees. Do not animate or move the object from another behavior during play.

## Understand the Angles

Angles are signed numbers around **Hinge Axis**. Point your right thumb in the direction of the axis: the direction your fingers curl is positive. A ball can therefore produce either a positive or negative angle depending on the axis direction, where it touches the toy, and which way it is moving. The words minimum and maximum only mean the lower and higher numbers; they do not mean hit position and return position.

For a one-sided bash toy, make zero one of the stops. If the hit produces a negative angle, use a range such as **Minimum Angle = -19** and **Maximum Angle = 0**. If you prefer the hit to display as a positive angle, reverse the Hinge Axis and use **Minimum Angle = 0** and **Maximum Angle = 19**. A range of -19 to 19 lets the spring pass through zero and swing to the other side.

## Spring Hinge Inspector

### Pivot and Mass

| Setting | What it does |
| --- | --- |
| **Hinge Axis** | Sets the line around which the toy rotates, using the GameObject's local X, Y, and Z directions. The local origin is always the pivot point. Reverse all three values to reverse which physical movement is called positive without changing the pivot line. |
| **Centre Of Mass** | Places the toy's balance point relative to the pivot, in VPX units. Its position controls how gravity pulls on the toy. Moving it farther from the pivot generally gives gravity more turning power. The yellow Scene view handle can be used to place it visually. |
| **Toy Mass (ball-relative)** | Sets the unloaded toy's weight relative to a standard ball. A value of 1 means the toy weighs as much as one standard ball. With automatic inertia, a heavier toy reacts more slowly to the same hit. When **Override Inertia** is enabled, this value still affects gravity, while **Manual Inertia** controls resistance to impacts and turning. A captured ball adds its own weight and resistance automatically. |
| **Override Inertia** | Chooses how VPE calculates the toy's resistance to being rotated. Leave it disabled for an estimate based on Toy Mass, Centre Of Mass, and Mass Box Half Extents. Enable it when you want to tune that resistance directly. |
| **Manual Inertia** | Appears when **Override Inertia** is enabled. Higher values make the toy harder to start and stop, so the same ball hit moves it less and it swings more slowly. Lower values make it react more quickly. This is a tuning value rather than a weight in kilograms. |
| **Mass Box Half Extents** | Appears when **Override Inertia** is disabled. Describes half the width, height, and depth of a simple box used only to estimate how the toy's mass is spread around the pivot. For example, X = 25 means a total width of 50 VPX units. This box does not collide with the ball. |

### Spring and Stops

| Setting | What it does |
| --- | --- |
| **Spring Stiffness** | Controls how strongly the spring pulls the toy toward Equilibrium Angle. A higher value returns the toy harder and usually faster. A lower value feels softer and allows the ball or captured weight to push it farther. |
| **Spring Damping** | Removes swinging over time. A higher value settles the toy sooner but can make it feel sluggish. A lower value allows it to swing back and forth for longer. Increase this if the toy keeps oscillating too much. |
| **Equilibrium Angle** | Sets the angle toward which the spring pulls. Gravity and a captured ball can make the final settled angle differ from this value. The value may be outside the allowed range when the spring should keep the toy pressed against a stop. |
| **Minimum Angle** | Sets the lower hard stop. The toy cannot rotate to a smaller signed angle. This is a number limit, not automatically the hit side. |
| **Maximum Angle** | Sets the upper hard stop. The toy cannot rotate to a larger signed angle. This is a number limit, not automatically the return side. |
| **Initial Angle** | Sets the toy's angle when play starts. Zero uses the local rotation authored in the Unity scene. The value must lie between Minimum Angle and Maximum Angle. |

### Angle Switch

| Setting | What it does |
| --- | --- |
| **Enable Angle Switch** | Adds a switch that game logic can use to detect that the toy has moved far enough in the positive-angle direction. Leave it disabled when the table does not need an angle switch. |
| **Switch Close Angle** | Closes the switch when the toy reaches or rises above this angle. For a bash toy that moves into negative angles, reverse Hinge Axis so the hit direction is positive before using this switch. |
| **Switch Open Angle** | Opens the switch again when the toy returns to or below this angle. Set it lower than Switch Close Angle so tiny movements near the threshold do not rapidly turn the switch on and off. |

### Setup Buttons

| Button | What it does |
| --- | --- |
| **Add Analytic Box Proxy** | Appears when the GameObject has no Spring Hinge Collider and adds one. The toy needs this collider for balls to hit it and for its visible transform to follow the simulated angle. |
| **Fit From Renderers** | Fits Centre Of Mass, Mass Box Half Extents, and the collision box around the object's renderers and their children. Treat the result as a starting point: the renderer bounds cannot tell whether a model is hollow or where its real weight is concentrated. |
| **Apply Bash Preset** | Replaces the current hinge and collider settings with useful starting values for a simple one-sided bash toy. If the toy contains an owned magnet, the button also applies the recommended magnet settings. Review the fitted size, axis, travel direction, and magnet position afterward. |

## Spring Hinge Collider Inspector

### Analytic Box

| Setting | What it does |
| --- | --- |
| **Local Centre** | Positions the middle of the collision box relative to the hinge pivot, in VPX units along the GameObject's local axes. Move this until the box covers the part of the model that the ball can hit. |
| **Local Rotation** | Rotates the collision box relative to the GameObject, in degrees. Use it when the hittable face is tilted or does not line up with the object's local axes. |
| **Half Extents** | Sets half the collision box's width, height, and depth in VPX units. The full size is twice these values. Keep all three values above zero. |
| **Show Collider** | Draws the collision box in green in the Scene view. In Play Mode it follows the current simulated angle, making it useful for checking that the visible toy and collision box move together. It has no effect on physics. |
| **Hit Event** | Allows the collider to send a Hit event to game logic when a ball strikes it. Turning this off does not stop the physical collision. |
| **Hit Threshold** | Sets the minimum impact speed required to send a Hit event. A value of zero reports every new impact. Raising it filters out gentle touches and resting contact. It does not change how the ball or toy moves. |

### Physics Material

| Setting | What it does |
| --- | --- |
| **Preset** | Uses the bounce and friction values from a Physics Material asset. It is available when **Overwrite Physics** is disabled. Use a preset when several objects should share the same surface behavior. |
| **Overwrite Physics** | When enabled, the Elasticity, Elasticity Falloff, and Friction values below are used. When disabled, the selected Preset supplies those values. |
| **Elasticity** | Controls how much relative speed is returned after a collision. Zero gives almost no extra bounce; higher values make the ball and toy separate more sharply. A low value usually suits a heavy bash toy. |
| **Elasticity Falloff** | Reduces bounce for faster impacts. Zero keeps the same elasticity at every speed. Higher values make fast shots less bouncy while leaving slow contacts closer to the Elasticity setting. |
| **Friction** | Controls how strongly the surface grips a ball sliding across it. Higher values remove more sideways sliding and can transfer more sideways turning force to the toy. Lower values let the ball slide more freely. |

The collider also drives the visible rotation. It takes the angle and axis from Spring Hinge and applies them on top of the GameObject's authored local rotation. There is no separate transform or axis setting on this component.

## Scene View Guides

The cyan line and arrow show the hinge axis. The orange arc shows the allowed angle range. The yellow sphere shows Centre Of Mass. When Spring Hinge Collider is selected, the cyan wire box is the editable collision box and the two orange wire boxes show where that box will be at Minimum Angle and Maximum Angle. Enable **Show Collider** to draw the current box in green.

Version one supports one box attached to the hinge. Remove or disable Unity mesh and static colliders on the moving visual so the ball cannot hit two collision shapes for the same toy.

The box is tested continuously against the ball, including its faces, edges, corners, and motion while the toy returns. The current implementation is tested for ordinary pinball shot speeds and moderately moving toys. Very small boxes, extremely stiff springs, or unusually fast mechanisms need additional play testing; see the developer qualification document for the measured limits.

## Couple a Magnet

Create a child GameObject below the rotating object, position it at the physical magnet pole in the toy, and add a Magnet component. Here, "below" means a descendant in the Unity hierarchy; the magnet may physically sit anywhere in the toy, such as Mechagodzilla's belly. Select **Spatial**, then enable **Couple To Parent Hinge**. Spatial magnets always use the Physical response, so there is no separate response setting to configure. The child inherits the toy's rotation and resolves the Spring Hinge from its parent. Only one owned magnet and one attached ball are supported per hinge; the inspector rejects unsupported types or duplicates.

The magnet transform is the moving pole. **Held Ball Centre Offset** is a separate target in VPX units along the magnet's local axes at the authored rest pose. These distances ignore GameObject and parent scale, just like the magnet's radius and other dimensions, so scaling the visual model does not move the hold point. The green Scene view sphere shows the size and position of a standard held ball.

Click **Fit Hold Point to Collider** to place a standard 25-unit-radius ball against the nearest face, edge, or corner of the Spring Hinge Collider. The inspector warns when the target would put that ball inside the collider or leave a gap, because either placement prevents capture. Adjust the offset manually after fitting when the table uses a different ball radius.

**Hold Stiffness** and **Hold Damping** control attachment compliance. **Max Hold Force** is the current-dependent capacity. These settings are independent of Influence Radius. The inspector estimates the fastest standard ball the magnet can capture at full power while the toy is stationary. This is a starting point rather than a guarantee because coil rise time and toy motion also affect a real hit. Increase **Strength** when the ball reaches the hold point but bounces away without being captured; increase **Max Hold Force** when it captures and then immediately breaks free. The bash preset supplies values intended for ordinary pinball shot speeds. Turning the coil off honors coil decay before release. Release preserves ball and hinge velocity.

## Validate in Play Mode

Test both magnet-off impacts and magnet-on capture, a timed release in each travel direction, a second-ball strike, and balls resting on passive playfield geometry.

## First-release Limits

- The pivot frame is fixed and must have nonzero orthogonal axes. Moving bases, shear, nested hinges, hinge-to-hinge contact, motors, and flexible toys are not supported.
- Collision uses one box proxy. Triangle, compound, and arbitrary mesh proxies are not supported.
- One Spatial magnet may own one ball. Other balls remain free and can strike the toy or held ball.
- Playfield and passive-surface support are qualified by the current sequential solver. Simultaneous squeezed contacts are an approximation and can retain a one-tick support lag.
- If an attached ball reaches a flipper, plunger, kicker, bumper, slingshot, or turntable, VPE releases it before the existing active mechanism runs and emits a rate-limited diagnostic. Place the held-ball sweep away from those mechanisms.
- Runtime save states and an isolated editor physics preview are not included. Packaged tables preserve authored values and hierarchy, not captured-ball IDs or warm solver state.
