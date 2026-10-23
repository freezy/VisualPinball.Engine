# Audio routing prototype

This is the first implementation milestone of the [audio processing and SSF plan](../../VisualPinball.Unity/Documentation~/developer-guide/audio-processing-and-ssf-plan.md). It compares Unity's selected native spatializer with an ordinary eight-channel generator for carrying four cabinet outputs plus backbox stereo through a master mixer. It is an isolated developer probe, not production audio processing or an SSF feature in the player.

The native source and CMake fixtures are portable C++17. The standalone build helper currently targets Windows x64 with Unity 6000.5.0f1 and Visual Studio 2022. Linux and macOS builds and hardware validation remain pending. No native binaries are checked in or installed into an existing Unity project.

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

Production sources, mixer selection, gain behavior, `.vpe` serializers and packaged recordings are untouched. The prior T2 package verification established 41 banks and 214 clips, including seven rolling banks with 29 clips; that prior result is a baseline, not a new export or a dry T2 PCM capture performed by this probe. A fresh dry capture, semantic buses, ROM ownership, `.vpe` routing metadata, EQ/compression/reverb/limiting, player settings and calibration remain subsequent milestones. The player and engine still have no runtime dependency on the assets library for this work.

## Third-party source

`ThirdParty/Unity/AudioPluginInterface.h` is the unmodified Unity Native Audio Plugin SDK header pinned in `source.json`, accompanied by Unity's MIT license. The plugin requests only the 1.4.2 ABI and does not misuse the SDK's decoder-only `UseOutputChannelCount` flag to expand an ordinary spatializer.
