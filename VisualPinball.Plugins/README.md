# Player plugin contracts

This MIT-licensed .NET Standard 2.1 library defines the player plugin API without a Unity dependency. Unity compiles the same source through `VisualPinball.Plugins.asmdef`; portable tests run through `dotnet test VisualPinball.Plugins.Test/VisualPinball.Plugins.Test.csproj`.

An optional integration package registers an `IPlayerPluginFactory`. Its descriptor declares a stable ID, default enablement, platforms, hard dependencies, required/provided capabilities, numeric order, and optional before/after relationships. Its schema and settings provider describe configuration without loading native libraries. The shared player creates the settings UI and constructs runtime instances only for enabled, supported plugins.

## Lifecycle

The host resolves dependencies before numeric order, checks every phase for cycles, and blocks affected plugins while independent integrations continue. Hooks start on the owner thread. An asynchronous hook returning from worker code must switch through `PluginApplicationContext.Scheduler` before accessing Unity. Prepare and Start run while the restored table is inactive, before its gamelogic can emit its first event. Ready follows `Player.OnPlayerStarted`; pause, resume, focus, tick and render use their own phase order.

Instances live for one game. The application context scope covers initialization through shutdown; the game scope covers preparation through stop. Own subscriptions, publications and handles in these scopes. Cleanup unwinds actual startup order in reverse, uses fresh cancellation deadlines, and continues after failures. An unresponsive asynchronous hook cannot hold the host forever. Its plugin ID remains quarantined until the abandoned hook, deferred native work and shutdown finish, preventing overlapping instances. Healthy plugins keep ticking and rendering while an unrelated integration is being reconfigured or cleaned up. `IPluginEmergencyStop` must close event admission immediately and arrange safe deferred native disposal when a worker is still active. Native devices must retain exclusive output leases until their worker has stopped. Implement `IPluginDeferredCleanup.Quiescence` to report that lifetime to the host.

Static Unity registries reset at SubsystemRegistration. Player applications claim ownership before automatic bridges run, so removing or disabling an adapter cannot resurrect an old watcher. Table authoring projects keep their existing automatic integration behavior.

## Configuration

Each plugin owns a directory under the player's `plugins` configuration root. `settings.json` stores global values; `policy.json` stores default and per-game enablement; hashed files under `games` store sparse overrides associated with a provider-qualified `GameIdentity`. This configuration does not enter `player-config.json`.

Fields declare GlobalOnly, GameOnly or GlobalAndGame scope and Live, NextGame or ApplicationRestart behavior. Known fields stored in the wrong scope cannot affect runtime settings. Unknown values and document properties survive saves. Migrations operate on detached JSON trees; files from a future schema remain readable and cannot be overwritten. No persisted value names a CLR type.

`PluginSettingsSession` owns a detached draft. Apply freezes and validates it, rechecks dynamic catalogs, and persists atomically. With a host, live reconfiguration and persistence share the lifecycle gate. `IPluginReconfiguration` must implement transactional updates: a failure or cancellation leaves the previous runtime state intact. If persistence fails after a live update, the host requests the previous runtime settings. Other values take effect at the next game or application restart.

Dynamic choices use stable IDs, readable labels, availability reasons and a catalog revision. Missing explicit asset selections remain visible and require user action. A runtime-only catalog may declare `IsComplete = false` while its game is stopped; this keeps an existing source selection without claiming it is available. Explicit display selections are game-specific and controller-relative. `PluginResourceChoices` supplies the shared discovery and resolution rules.

The Player owns every configuration control, including standard fields, typed records/lists, radio choices, screen placement, previews, inheritance, Apply/Revert/Reset and runtime diagnostics. Plugins declare labels, types, defaults, scope, validation and dynamic providers through `PluginField`; they do not supply UIElements, templates, styles or configuration shaders.

`SettingKind.ScreenLayout` describes an array of homes with a stable `screen` identifier and integer `x`, `y`, `width`, `height` in physical desktop pixels. Signed coordinates support multiple monitors. Its `Provider` discovers the available screen IDs and names; Apply rechecks their availability. String IDs are preferred; legacy integer IDs remain accepted. Native placement creates a sparse game override when an inherited home is moved. The shared Player renders aspect-ratio guides against the full window bounds. `IUnitySettingsWindowProvider` adapts a plugin's native output transport; the Player owns the placeholder pixels and controls.

`SettingKind.Preview` declares a preview provider without persisting UI state. `IUnitySettingsPreviewProvider` returns an owned preview with textures, normalized layer rectangles, visibility and canvas dimensions through VPE-owned contracts. The Player owns the viewport, buttons and presentation. Partial startup must be released on cancellation; hiding a settings page disposes its preview. Composite fields and collection records use `Children` to declare nested data controls, reusing the same Player rendering.

## Cross-plugin resources

VPE owns the payload contracts for original and presentation DMD frames, controller events, PCM audio, segment brightness, input, triggers and GPU surfaces. Descriptors identify logical source, provider, controller, session, concrete instance, role, supported formats and derivation parents. Publish a parent before its derived resource. A parent replacement invalidates the old derived lineage; consumers cannot use frames from an earlier instance.

Bindings follow logical resources across publication/removal. Auto selects the unique compatible source and blocks ambiguity. An explicit missing source waits for that source and never substitutes a different one. Identification always uses a parentless original source. Presentation prefers a derived surface; original fallback is opt-in. Identification leases preserve native 2/4-bit shade indices and hold provider-wide raw mode until the last consumer leaves. Frames report their actual encoding; incompatible concrete frames are rejected with a diagnostic.

Display pixels, PCM samples and segment buffers are borrowed during synchronous callbacks. Copy only when crossing a thread boundary. Display/controller bridges deliver on the Unity owner thread; native PCM can be delivered on its producer thread, and its source synchronizes subscriptions. Consumers must keep producer callbacks bounded. Native destinations use `PluginOutputLeases` to reserve physical outputs. Input stages use `PluginInputPipeline`; a producer applies its ordered filters before dispatching the resulting input event.

## Package composition and validation

`VisualPinball.Plugins` contains contracts, configuration and the host. `VisualPinball.Unity/VisualPinball.Unity/Plugins` bridges scene identities, gamelogic resources and settings media transports. The shared Player renders configuration and owns transitions. Each integration's normal Unity package contains its settings schema, discovery providers and runtime plugin assembly. There is no additional Player package for PinUp or DMD. HDRP/URP shells choose optional integration packages in their manifests; shared Player code has no dependency on them.

The portable suite covers failure isolation, ordering, cancellation/deadlines, sparse profiles, migrations, future versions, missing choices, raw/presentation routing, generation changes, output exclusivity, audio replacement, input ordering and representative resource families. Native desktop and hardware behavior must also be tested on the target platform; portable tests do not validate a Metal compositor, Linux desktop or attached cabinet device.
