# Cabinet display outputs

The Player owns adopted desktop output placement. Plugins own content generation and device drivers. Existing installations retain Custom placement until the user adopts an output role; an unavailable managed target never silently falls back to a plugin-owned window.

## Shared boundary

`CabinetDisplaySnapshot` is an immutable game-scoped capability (`vpe.cabinet-displays.v1`) published before plugin preparation. It contains resolved destinations and explicit per-plugin role assignments. Missing capability or missing assignment means Custom. Player and Off suppress that role's legacy physical output independently of scene rendering. A factory can advertise supported roles through `ICabinetDisplayPlugin` without creating a game instance.

`CabinetSurfaceSelector` provides deterministic provider ranking with caller-supplied readiness and eligibility. Scene consumers retain engine-DMD precedence. Legacy Unity texture surfaces remain scene-eligible; an updated `ICabinetSurface` must declare Scene and be ready. Producers call `PluginResourceRegistry.NotifyChanged` whenever readiness or consumer eligibility changes. Updated resources expose a desktop texture through `IUnityDesktopSurface`; its color-space property describes shader-sampled values after any hardware sRGB conversion. Producers must withdraw the publication or announce replacement before releasing a borrowed texture. The Unity test files follow the existing test assembly's GPL-3.0-or-later convention; the additive plugin API and presentation helper use MIT like their neighboring integration files.

## Presentation ownership

`CabinetOutputTexture` copies content into an owned, opaque SDR RenderTexture. A native window may borrow this output only. It never receives a producer texture pointer. Close or detach the native window and drain native retirement before releasing the owned output. NativeWindowHost remains the sole render-event pump during play.

The Player must drive plugin BeforeRender, then its cabinet copy pass in the same LateUpdate, before NativeWindowHost at execution order 10000. Texture Changed events describe allocation/replacement, not each video frame. Copy live surfaces every frame unless a producer revision proves unchanged. The cabinet copy call must run even when no gameplay plugins are active.

## Delivery and validation

The initial shared boundary has pure contract tests, GPU-copy tests, and an EditMode native probe for two windows, pointer replacement and retirement. Run the native probe on each supported platform/backend before certifying it; a single Windows graphics backend does not certify Metal or Vulkan.

The next Player phase adds persisted layout, Windows topology, browsing backglass and playfield monitor selection. Playfield placement stays in the Video transaction, not an auxiliary MainWindow endpoint. The asynchronous move, verification, Keep/Revert and cancellation must use one version token. Later phases adapt B2S, PinUP and DMD in that order, preserving legacy output for unsupported layouts and device configurations.
