# Flipper

How flippers interact with the ball is what makes or breaks a pinball simulation. A few years ago, a community member named Mukuste spent time on improving Visual Pinball's flipper mechanics. The result was what has become known as the *physmod*, which provided a much more realistic simulation, and which was later merged into Visual Pinball's mainline. In his [own words](https://github.com/c-f-h/vpinball/wiki/VP10-Physics):

> Flippers are now simulated as true dynamic rigid bodies which have forces from the solenoid, the return spring and the ball acting on them and accelerate accordingly. They will also properly bounce off their stoppers instead of just moving to maximum extension and then stopping. In practice, this means that flipper/ball interaction is now much more realistic and less binary. Post passes, light taps, cradle separations, drop and live catches are now all possible. Furthermore, the simulation of the friction of the flipper rubbers greatly improves aiming.

Later, another member of the community named nFozzy managed to measure ball trajectory angles of real pinball machines, and developed a script where authors could provide a profile that would then slightly correct the ball trajectories during game play to match his measurements. This resulted in tables with the most realistic flipper behavior the community has produced yet.

VPE's flipper behavior is identical to Visual Pinball's. However, VPE also provides native support for the nFozzy corrections.

## Setup

The easiest way to create a flipper is clicking on the flipper icon in the toolbar. This will instantiate the prefab and place it on the playfield. 

### Mesh

VPE provides a procedurally generated flipper mesh. It consist of a *base mesh* (the plastic), and a *rubber mesh*.

It's possible to provide a custom mesh for the flipper by replacing the game objects that generate the procedural meshes with others. However, the physics simulation will still use the original colliders, so make sure to adapt the parameters to match the custom flipper's dimensions.

### Colliders

<img src="flipper-collider-inspector.png" width="438" alt="Add display component" class="img-responsive pull-right" style="margin-left: 15px"/>

Adding the collider component to the flipper makes it part of the physics simulation. Here you can tweak the various parameters. Most of the following is taken directly from [Mukuste's Wiki](https://github.com/c-f-h/vpinball/wiki/VP10-Physics#flipper-parameters).

#### Mass

This is the mass of the flipper (where 1 corresponds to standard ball mass, 80g). It basically describes how much the flipper interacts with the ball. A very heavy flipper will barely feel the impact of the ball and keep moving at almost the same velocity as it was before the hit. A very light flipper, on the other hand, will move much slower with the ball on it than it does without the ball, and will be deflected by the impact of the ball significantly.

A good default value for this parameter is 1 - 1.25.


#### Strength

This is the force (actually, torque) with which the solenoid accelerates the flipper. The higher this value, the faster the flipper will move. But be aware that this is directly linked to flipper mass: if the flipper is twice as heavy, it also needs twice the force to get it to move at the same speed.

A reasonable range for this is 1000-3000, obviously depending a lot on the era of flippers being simulated and the desired speed of the game.


#### Elasticity and Elasticity Falloff

This is basically the bounciness of the flipper rubber. Since real rubber is less bouncy when it is hit at a higher velocity, the Falloff parameter allows decreasing the elasticity for faster impacts. A value of 0 for falloff means no falloff, i.e., elasticity does not depend on velocity, and a value of 1.0 means that elasticity is halved at an impact velocity of 1 m/s.

Good defaults for elasticity seem to be 0.8-0.9, with a falloff of 0.3.

#### Friction

This describes how much the rubber "grips" the ball. This value is very important for enabling center shots on the playfield with a moving ball, as well as backhands. In general it affects the aiming on all shots, but also makes a spinning ball deflect off the flipper in the proper direction.

A good default seems to be 0.8.

#### Return Strength Ratio

This is the force of the return spring which pulls the flipper back down, relative to the solenoid force which pulls the flipper forward. For instance, at 0.10, the force of the return spring will be 1/10th of that of the solenoid. Due to how acceleration and velocity work, the time the flipper needs to return to its home position is about three times longer than that for the forward stroke in this example (square root of 10, to be precise).

If you make this smaller, not only will the flipper return slower, but it will also pick up less speed if you briefly release the flipper and then press it again since it has less time to accelerate. A smaller value therefore makes it easier to do flipper tricks which involve light taps, such as cradle separations and flick passes.

Try the range 0.07-0.10 to start with and experiment from there.

#### Coil Ramp Up

This simulates the fact that the magnetic field in the flipper solenoid takes a while to build up when the flipper button is pressed, and to fall off again when the button is released (also known as hysteresis). This means that the flipper will not have its full acceleration immediately as the coil needs some time to ramp up to the full magnetic field.

At a value of 0, there is no ramp up, and the full acceleration takes effect immediately. At a nonzero value, this is the approximate time the solenoid needs to reach its full acceleration. For instance, if set to 3, the flipper coil will take around 30 ms to ramp up to full force.

Gameplay-wise, the effect of this parameter is most strongly felt in situations where the flipper button is pressed only for a very short time, or released for a short time and then pressed again. In other words, it will make light taps much easier and therefore help with moves such as cradle separations and flick passes. Even tap passes can be achieved with the proper setting.

Note that increasing this setting will decrease the speed of the flipper a bit and may need to be compensated with a higher Strength setting. Also, if this parameter is chosen too high, the flipper may feel sluggish and laggy.

That's why per default, we recommend setting this value to 0.

---

-> [API Reference](xref:VisualPinball.Unity.FlipperApi)