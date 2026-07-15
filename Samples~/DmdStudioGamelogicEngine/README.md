# DMD Studio Gamelogic Engine

`DmdStudioSampleGamelogicEngine` is a minimal custom VPE gamelogic engine that connects a
`DmdProjectAsset` to both in-scene displays and the optional DMD bridge.

1. Import this sample from Package Manager.
2. Add `DmdStudioSampleGamelogicEngine` to the same GameObject as `Player` and select it as the
   table's gamelogic engine.
3. Assign a DMD Studio project. The optional base cue defaults to `score`.
4. Call `PlayCue`, `UpdateCue`, or `SetBaseCue` from table logic. All calls must remain on Unity's
   main thread.

The sample uses the real `IGamelogicEngine.OnInit(Player, TableApi, BallManager, CancellationToken)`
signature, forwards format preferences from the DMD bridge, and disposes its `DmdCuePlayer` during
scene teardown.
