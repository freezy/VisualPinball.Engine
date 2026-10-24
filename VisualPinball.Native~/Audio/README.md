# Native audio: routing probe and PCM renderer

This is the first implementation milestone of the [audio processing and SSF plan](../../VisualPinball.Unity/Documentation~/developer-guide/audio-processing-and-ssf-plan.md). It compares Unity's selected native spatializer with an ordinary eight-channel generator for carrying four cabinet outputs plus backbox stereo through a master mixer. It is an isolated developer probe, not production audio processing or an SSF feature in the player.

The native source and CMake fixtures are portable C++17. The standalone routing-probe build helper targets Windows x64 with Unity 6000.5.0f1 and Visual Studio 2022. No binaries are checked in. The desktop package build now additionally compiles `VpePcm` for Windows x64/x86, Linux x64 and universal macOS; those builds still need platform playback qualification.

## Central stereo PCM renderer

`PcmRenderer.cpp` is the dry production candidate used by the player's opt-in **New stereo mixer** setting. It is separate from the isolated spatializer/generator probe. Build with the CMake commands below, then copy `VpePcm.dll` into `VisualPinball.Unity/Plugins/win-x64` for local Windows development; CI packages each desktop binary with its platform importer metadata. Reference playback remains the default and the managed adapter falls back when the native library or supported output is unavailable. Do not replace an already loaded native DLL in an editor session: restart the editor after rebuilding it.

The C ABI has one main-thread command producer, one audio-thread renderer and one serialized live-PCM producer. The control queue has 4096 entries, voices have 512 generation-tagged slots, and the stereo stream ring has 8192 frames. Native render does not allocate, lock, log, call Unity or wait for the main thread. Stop is an out-of-band atomic so a full queue cannot lose it. Clip preparation happens on the main thread, with 32 MiB per clip and 256 MiB per table limits; oversized/unreadable/custom-mixer sources keep Unity playback. Destroy requires producer quiescence and detaches the context before waiting for an existing bounded callback to finish.

Seven stable buses receive transient mechanics, rolling, ROM, table music, callouts, synthetic effects and player UI. Mono uses constant-power stereo panning; centered authored stereo preserves both channels. Per-voice pitch/sample-rate conversion retains fractional cursors and looping phase. Pause freezes clip cursors, flushes the live ring, and resumes without stale stream audio. ROM takeover requires an exclusive playback capability and explicit integration gain metadata; raw capture subscribers remain unscaled. Counters expose underflow, overflow, sequence discontinuity and command drops. This milestone uses linear interpolation and bounded discontinuity recovery; adaptive clock drift compensation remains follow-up work before this path becomes the default.

On Windows, `PcmOutput.cpp` plays the graph through its own event-driven WASAPI shared-mode stream on an MMCSS "Pro Audio" thread instead of a Unity audio callback. It keeps one engine period plus a 3 ms margin queued (13 ms on a 10 ms engine), where Unity's route adds its own 4-block mixer ring and FMOD's device buffer. On a Focusrite USB interface this cut flipper key to Windows-loopback latency from about 110 ms to about 68 ms, on par with VPX 10.8. The stream renders at Unity's output rate and lets Windows convert to the mix format, follows default-device changes, reopens lost devices while keeping the graph's clock running, and widens its margin by 1 ms per underrun once the stream settled. `PcmAudioRenderer.PreferDeviceOutput` selects it; Unity's carrier callback remains the fallback when no stream opens and on other platforms. While a device stream exists it is the graph's only renderer. `PcmRendererTests --live-output` exercises it against the default playback device.

Besides the ROM stream, seven more live PCM inputs can be opened (`VpePcmStreamOpen`), each on a bus with a single producer, for plugin audio such as video soundtracks; the managed side wraps them as `PcmAudioStream`. Gamelogic engines that pull their emulator's queue in a Unity filter hand it over through `GamelogicAudioPump` instead, which feeds the ROM stream. Buffered streams wait for a jitter buffer sized from the producer's and the renderer's largest recent blocks before playing, and are steered to it: up to 0.3% faster or slower against clock drift, and a jump back to it after a burst. The ROM stream uses the adaptive mode, which plays at once until its first underflow; before this, a ROM stream that ran ahead stayed late (141 ms measured on ASIO) until the ring overflowed.

Only stereo output is exposed by this renderer. Neither the C ABI nor the player claims physical SSF support. Effects, linked processing, discrete channel delivery, hardware calibration, latency qualification and listening comparisons remain later milestones in the plan. Existing `.vpe` files use compatible automatic bus selection; new bank IDs are packaged without recording any user/device configuration or depending on the authoring assets library at runtime.

`Importers/*.meta.template` supplies stable platform importer GUIDs when CI assembles native artifacts. Keeping non-host importer templates outside the Unity asset tree avoids Unity deleting them as orphan metadata before those platform binaries have been built. Windows links the C++ runtime statically, so this library adds no external VC++ runtime DLL requirement.

## Build and run

Run from the engine checkout with PowerShell 7, CMake, Visual Studio 2022's C++ tools, and the Unity Windows build module installed:

```powershell
./VisualPinball.Native~/Audio/BuildProbe.ps1 -OutputDirectory E:/temp/vpe-audio-probe
E:/temp/vpe-audio-probe/player/VpeAudioProbe.exe --layout stereo --report E:/temp/vpe-audio-probe/stereo.json --quit-on-complete
E:/temp/vpe-audio-probe/player/VpeAudioProbe.exe --layout 7.1 --report E:/temp/vpe-audio-probe/surround.json --quit-on-complete
E:/temp/vpe-audio-probe/player/VpeAudioProbe.exe --backend generator --layout 7.1 --report E:/temp/vpe-audio-probe/generator.json --quit-on-complete
```

Quote paths containing spaces. `-UnityEditor` overrides the build helper's editor path. The helper builds and tests the native library, creates a marked `ProbeProject` under the output directory, copies the developer sample and plugin there, and builds the executable. It refuses to modify an existing unmarked project. Its Unity build log is `unity-build.log`. The generated project is disposable; the script never opens or edits a table or player project. Unity's internal mixer-construction API is isolated in this version-specific editor sample and checked by the standalone build.

Omit `--quit-on-complete` to keep the result window open. One bounded low-level tone plays at a time, at 0.02 peak amplitude (about -34 dBFS). The window identifies the current case and offers Stop tests. Cabinet corners and backbox left/right use different frequencies. A report is always attempted, including on native-load or setup failures. Exit code 0 means the run completed, possibly with an inconclusive surround gate; 2 means setup, cancellation or report-writing failed; 3 means a software gate failed. The spatializer negative control is expected to fail its eight-input gate and exit 3 on a discrete 7.1 driver, because the documented source input remains stereo; inspect individual cases to distinguish that expected failure from an unexpected raw-signal failure. Always inspect the JSON, rather than treating exit code 0 as SSF certification.

To run only the offline fixtures on another supported compiler:

```text
cmake -S VisualPinball.Native~/Audio -B /tmp/vpe-audio-native
cmake --build /tmp/vpe-audio-native --config Release
ctest --test-dir /tmp/vpe-audio-native -C Release --output-on-failure
```

The CTest executable checks registration, distance attenuation, exact bypass, offline mono power recovery, corner/center mapping, stereo preservation, in-place buffers, invalid parameters/samples, unsupported-layout muting, the master meter and six isolated generator outputs across four sample rates and four block lengths. Compilation on another platform alone does not establish Unity or device support there.

## What it measures

`VPE Cabinet Panner` is the selected native spatializer in the isolated build. A flat rolloff, unity distance attenuation, zero Doppler and high voice priority remove ordinary camera-distance effects. `VPE Routing Meter` is a pass-through effect after the master group's attenuation. The audio callback uses fixed storage and lock-free scalar atomics; it never allocates, logs, blocks or calls managed code. Creation and release own the small per-effect allocation. Diagnostics reserve slot 0 for the master, slot 1 for the sole active source and slot 2 for the generator; simultaneous voices must use distinct slots.

| Mode | Candidate behavior |
| --- | --- |
| Pass-through | Preserve each finite input sample when input/output counts agree, including the ordinary stereo path. |
| Cabinet | Require eight inputs and eight outputs; recover the measured dual-mono input at L/R, then distribute a mono signal to slots 4–7 with constant-power corner gains. |
| Backbox | Require eight inputs and eight outputs; retain original L/R in slots 0/1 and silence every other slot. |
| Generator | Declare eight channels, ignore input audio, and emit one bounded test tone at a time into the selected output slot; measure the actual output count Unity supplies. |

Slots 4/5 represent cabinet front left/right and 6/7 cabinet back left/right in this candidate wiring preset. Slots 2/3 are silent. These are buffer slots, not verified physical speaker labels. The cabinet matrix is an offline reference using an equal-power dual-mono pair. Its eight-input spatializer mode is retained as a negative control: [Unity's documented spatializer input is stereo](https://docs.unity.com/en-us/engine/6000.5/manual/audio/mixer-native-audio-plugin/spatializer-sdk), so the original eight-input source assumption cannot support production SSF. The central generator/PCM renderer is now the production candidate based on that contract, not merely on stereo hardware being unavailable. The tone generator proves only transport when actual callbacks allow it; it implements no PCM cursor, pitch/resampling, loop/fade behavior or authored stereo-mechanical fold-down.

The JSON records requested/effective/driver layouts, Unity version, sample rate, DSP buffer size, selected plugin, callback channel counts, block lengths, host API version, virtualization, parameter acceptance, and per-channel peak levels before and after the panner and at the master. Raw mono/stereo and ordinary 2D program tests verify gain and channel isolation, rather than merely detecting nonzero audio. Cabinet cases cover every corner, center, and a distant listener; backbox cases isolate left/right. Unsupported spatializer cabinet/backbox layouts produce silence. The generator emits only within available buffer slots, so its cabinet tests are also silent when Unity supplies two channels. Its control snapshot is one lock-free 64-bit atomic value, preventing mixed channel/frequency/gain reads.

For the spatializer run, `stereoRoutingPassed` covers the five raw/program cases and `softwareRoutingPassed` requires every case, including the deliberately strict eight-channel negative controls. For the generator run, `stereoRoutingPassed` covers generator backbox left/right preservation and `softwareRoutingPassed` requires all six outputs at the generator and post-attenuation meter. `surroundGate` is `inconclusive` if the only failures are missing eight-channel paths on a driver with fewer than eight channels; a gain/isolation failure remains failed. A requested 7.1 setting alone proves nothing about callback layout. The spatializer cannot be chosen as a production eight-input route on the basis of this test. `physicalOutputsVerified` remains false: software cannot prove wiring, downstream OS downmixing, exciter placement or end-to-end latency. `JsonUtility` serializes a null error as an empty string, so report consumers must test for a nonempty `error`.

## Initial measured result

On Windows with Unity 6000.5.0f1, 48 kHz, a 1024-frame DSP buffer and a stereo driver, both requested Stereo and requested 7.1 runs passed raw mono, isolated stereo left/right and 2D program left/right. The source and master callbacks remained 2 → 2 even when `AudioSettings.GetConfiguration()` reported `Mode7point1`. Mono arrived as L/R peaks of 0.014142 from a 0.02 source; isolated stereo retained the 0.02 peak in the correct channel. Cabinet/backbox candidates muted as intended because the eight-channel path was unavailable. The ordinary generator was also tested with both layouts: its callbacks and master meter remained 2 → 2 despite the eight-channel declaration. Its backbox tones retained their gain and left/right separation; unavailable cabinet slots stayed silent. Discrete transport is inconclusive on this hardware. The documented stereo source contract, independently of this hardware limitation, selects the central PCM renderer candidate.

Repeat the probe with an OS-configured discrete 7.1 device and four cabinet exciters plus backbox stereo before claiming discrete transport or shipping SSF. Verify actual source and master counts, input recovery/gain, all physical outputs and silence on unrelated outputs, camera-independent behavior, continuous moving-voice panning, virtualization under realistic multiball load, and measured impact latency. This probe's static positions and fixed low voice count do not certify those moving/load cases. Prove the generator's actual eight-channel output and downstream preservation, then implement and test production PCM voice semantics before exposing SSF settings. The ordinary spatializer remains a stereo diagnostic, not a future eight-input backend awaiting different hardware.

## Current audio ingress audit

| Existing path | Production integration point for the next milestone |
| --- | --- |
| Mechanical and synthetic effects, including collision and ball-ball impacts | `SoundEffectAsset.ConfigureAudioSource` and `Play` create/configure each voice. Preserve cooldowns, pitch, authored gain, fades and per-impact positioning. |
| Rolling loops and material transitions | `RollingSoundComponent.CreateSource` creates persistent/per-ball sources and registers mechanical gain. Bind a route once, update table coordinates as the ball moves, and preserve lifecycle/slot behavior. |
| Music and callouts | `MusicPlayer` and `CalloutCoordinator` create sources through their bank's configuration. Verify pre-upmix stereo recovery before backbox routing. |
| ROM/game integration | PinMAME publishes raw PCM through `IPluginAudioSource` and also has legacy `OnAudioFilterRead` playback guarded by `PlaybackReplaced`. Establish one playback owner and an optional integration gain snapshot before routing; do not scale captured raw PCM or leave duplicate playback active. |
| Player mechanical slider and integration controls | The player currently applies `MechanicalSoundGain` independently from integration controls. Preserve those authored/user gain units during migration; no new controls or config migration are installed by this prototype. |
| Authoring previews | The editor Sound Manager owns a preview AudioSource. Keep preview/session ownership separate from table routes and avoid loading this probe into the authoring editor. |

The isolated routing probe does not modify production voices or recordings. The central PCM milestone above adds production voice adapters, semantic buses, gain-aware ROM takeover, player trims and `.vpe` bank metadata independently. The prior T2 package verification established 41 banks and 214 clips, including seven rolling banks with 29 clips; that result alone is not a dry T2 PCM capture or physical output certification. EQ/compression/reverb/limiting and cabinet calibration remain subsequent milestones. The player and engine have no runtime dependency on the assets library for this work.

## Third-party source

`ThirdParty/Unity/AudioPluginInterface.h` is the unmodified Unity Native Audio Plugin SDK header pinned in `source.json`, accompanied by Unity's MIT license. The plugin requests only the 1.4.2 ABI and does not misuse the SDK's decoder-only `UseOutputChannelCount` flag to expand an ordinary spatializer.


## Stereo DSP

`AudioProcessing` adds eight filter slots (high pass, low pass, two shelves and four bells) and linked soft-knee compression to each semantic bus and the stereo output. A shared Signalsmith FDN room return feeds the final output chain. A linked Signalsmith limiter uses 1 ms lookahead, a fixed matching dry delay and a final sample-peak guard. PCM ABI 1 remains compatible; optional DSP extension ABI 1 exposes preparation, bounded settings snapshots, response queries, meters and a quiet three-second preview. Older native libraries continue dry playback.

All allocation, coefficient design and graph preparation occur on the control thread. Rendering adopts prepared graphs at a block boundary and retires them for control-thread deletion. Never destroy a context while its renderer or stream producer is active. Callbacks use fixed buffers and bounded queues; DSP meters are atomic. The same native fixtures run on the desktop CI targets. Third-party revisions and local patches are recorded in `ThirdParty/Signalsmith`; runtime license notices are included in the Unity package's Resources folder.
