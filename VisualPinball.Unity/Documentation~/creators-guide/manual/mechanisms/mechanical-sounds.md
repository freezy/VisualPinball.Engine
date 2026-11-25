# Mechanical sounds

Sound banks are reusable SoundEffectAsset assets containing clips and playback variation. Assign generic mechanism sounds on library prefabs and keep table-specific sound banks and ROM mappings in the table project. Mechanical master gain is applied once by the playback service.

- Coil Sound plays on coil commands, including attempts with no ball. Use this for actuators whose physical noise occurs regardless of occupancy.
- Switch Sound plays on switch transitions or pulses. Repeated close pulses remain audible. For a trough exit, use the physical exit kicker switch opening rather than the trough coil command.
- Hit Sound can play bumper activation sounds.
- Kicker Release Sound plays only after a captured ball has actually been released. An optional coil ID filters multi-coil kickers. Direct API kicks have no originating coil ID.
- Pinball Event Sound selects one named event from a unique emitter on the same object. It starts on a false-to-true predicate transition and stops immediately when false. A finite sample finishing does not rearm the predicate.
- Flipper Sound layers Attack and Up on a full actuation, chooses Reflip near the end position, and plays Down on release after sufficient travel. Angles are captured when physical actuation changes and delivered with physics events on the main thread. Collision history adjusts actuator gain; this component does not play rubber collision sounds. Native dual-wound hold behavior retains the existing physics limitations.
- Input Action Sound observes canonical cabinet input action edges, including native polling. It does not dispatch game controls. Use a separate emitter per action and position it on the cabinet.
- Solenoid supplies a passive mapped coil destination, for example a knocker. Attach Coil Sound and use Stop Previous if repeated commands should restart its sound.

Cooldown belongs to each sound component and bank, so two mechanisms can share a bank without silencing each other. Set zero cooldown when every repeated pulse should play. Four flipper banks, source predicates, thresholds and device IDs survive table packaging.

Rolling, surface and ball impact audio is handled separately. Audio filters and bus effects are also independent of these mechanism triggers.
