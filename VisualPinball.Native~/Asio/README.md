# Native audio: ASIO output

`VpeAsio` plays the central PCM graph (`../Audio/PcmRenderer.cpp`) through an ASIO driver on Windows x64: a device vendor's driver, or a wrapper such as ASIO4ALL. ASIO skips the Windows audio engine and the device's Windows (WDM) driver buffers. On a Focusrite Scarlett 2i2 4th Gen, flipper key to the device's output measured 14 ms through Focusrite's ASIO driver at 64 frames, against 132–139 ms through WASAPI shared and exclusive mode and 115–122 ms through ASIO4ALL. ASIO4ALL drives the device through its Windows driver, so it can't avoid what that driver buffers; it helps on devices without their own ASIO driver.

## License

This library links the [Steinberg ASIO SDK](https://www.steinberg.net/developers/) under its GPLv3 option, so `VpeAsio.cpp` is GPL-3.0-or-later and kept apart from the MIT-licensed `VpePcm`. It calls `VpePcmRender` through the already loaded `VpePcm` module and shares no code with it. The SDK is not checked in.

## Design

One owner thread loads, initializes, starts, stops and exits the driver, with a message pump, as ASIO drivers expect. The driver's buffer callback renders the graph straight into the driver's buffers and converts to its sample type (16, 24 or 32-bit integer, or float). The device runs at Unity's output rate, since the graph's DSP is prepared for it. A reset request from the driver, such as a new buffer size chosen in its control panel, rebuilds the buffers. The SDK host layer keeps one driver per process, so there is at most one stream; a session that only shows a driver's control panel (no table playing) gives way when a stream starts.

`VpeAsioOutputMetrics` uses the layout of `VpePcmOutputMetrics`. For latency measurements, `VPE_ASIO_CAPTURE="<input channel>;<file>"` records one input channel with QPC timestamps; a Focusrite's Loopback input then captures what the device really plays.

## Build

```powershell
cmake -S VisualPinball.Native~/Asio -B build/asio -G "Visual Studio 17 2022" -A x64
cmake --build build/asio --config Release
```

CMake downloads the official SDK archive and checks its SHA-256. Pass `-DASIO_SDK_DIR=<folder with common/ and host/>` to use a local copy. Copy `VpeAsio.dll` into `VisualPinball.Unity/Plugins/win-x64`. Without it, the Player's ASIO choice falls back to normal output.
