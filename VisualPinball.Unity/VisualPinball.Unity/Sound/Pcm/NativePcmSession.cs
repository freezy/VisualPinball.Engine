// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace VisualPinball.Unity
{
	/// <summary>Owns one native graph. Control calls are main-thread only; the PCM producer is serialized by its adapter.</summary>
	internal sealed class NativePcmSession : IDisposable
	{
		internal const string Library = "VpePcm";
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmAbi();
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr VpePcmCreate();
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmDestroy(IntPtr context);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int VpePcmRegisterClip(IntPtr context, float[] samples, int frames, int channels, int rate);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern ulong VpePcmPlay(IntPtr context, int clip, int bus, float gain, float pitch, float pan, int loop);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int VpePcmUpdate(IntPtr context, ulong voice, float gain, float pitch, float pan, int paused);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void VpePcmStop(IntPtr context, ulong voice);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int VpePcmActive(IntPtr context, ulong voice);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern float VpePcmPosition(IntPtr context, ulong voice);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int VpePcmSetBusGain(IntPtr context, int bus, float gain);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void VpePcmSetStreamGain(IntPtr context, float gain);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int VpePcmStreamPush(IntPtr context, IntPtr samples, int frames, int channels, int rate, int encoding, long sequence);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void VpePcmFlushStream(IntPtr context);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void VpePcmEnableStream(IntPtr context, int enabled);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmStreamOpen(IntPtr context, int bus);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmStreamClose(IntPtr context, int stream);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmStreamPushTo(IntPtr context, int stream, IntPtr samples, int frames, int channels, int rate, int encoding, long sequence);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmStreamSetGain(IntPtr context, int stream, float gain);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmStreamFlush(IntPtr context, int stream);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmStreamSetBuffer(IntPtr context, int stream, int frames);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmStreamMetrics(IntPtr context, int stream, [Out] float[] values, int count);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmRender(IntPtr context, [In, Out] float[] output, int frames, int channels, int rate, int paused);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int VpePcmMetrics(IntPtr context, [Out] float[] values, int count);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmDspAbi();
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmPrepareDsp(IntPtr context, int rate);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmSetProcessing(IntPtr context, float[] values, int count);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmProcessingMeters(IntPtr context, [Out] float[] values, int count);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmPreview(IntPtr context, int bus, int enabled);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmPreviewClip(IntPtr context, int clip, int bus);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern float VpePcmEqResponse(IntPtr context, int route, float hz);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr VpePcmOutputCreate(IntPtr context, int rate, int paused);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr VpePcmOutputCreateEx(IntPtr context, int rate, int paused, int mode, int bufferFrames);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmOutputDestroy(IntPtr output);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmOutputSetPaused(IntPtr output, int paused);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern uint VpePcmOutputLastError();
		/// <summary>HRESULT of the last device stream that failed to open, for diagnostics.</summary>
		internal static uint LastDeviceOutputError { get { try { return VpePcmOutputLastError(); } catch (EntryPointNotFoundException) { return 0; } } }
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmOutputMetrics(IntPtr output, [Out] float[] values, int count);
		// Optional GPLv3 ASIO output (VisualPinball.Native~/Asio). Same metrics layout as the WASAPI stream.
		private const string AsioLibrary = "VpeAsio";
		[DllImport(AsioLibrary, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr VpeAsioOutputCreate(IntPtr context, string driver, int rate, int bufferFrames, int paused, StringBuilder error, int errorLength);
		[DllImport(AsioLibrary, CallingConvention = CallingConvention.Cdecl)] private static extern void VpeAsioOutputDestroy(IntPtr output);
		[DllImport(AsioLibrary, CallingConvention = CallingConvention.Cdecl)] private static extern void VpeAsioOutputSetPaused(IntPtr output, int paused);
		[DllImport(AsioLibrary, CallingConvention = CallingConvention.Cdecl)] private static extern int VpeAsioOutputMetrics(IntPtr output, [Out] float[] values, int count);
		[DllImport(AsioLibrary, CallingConvention = CallingConvention.Cdecl)] private static extern int VpeAsioDriverCount();
		[DllImport(AsioLibrary, CallingConvention = CallingConvention.Cdecl)] private static extern int VpeAsioDriverName(int index, StringBuilder name, int length);
		[DllImport(AsioLibrary, CallingConvention = CallingConvention.Cdecl)] private static extern int VpeAsioShowControlPanel(string driver);
		[DllImport(AsioLibrary, CallingConvention = CallingConvention.Cdecl)] private static extern void VpeAsioCloseControlPanel();
		internal const int OutputMetricCount = 13;
		private IntPtr _context, _output;
		private bool _asio; // written before _output is published, only by the main thread
		private int _readers, _blocks, _renderClosed;
		internal IntPtr Context => Volatile.Read(ref _context);
		internal int RenderedBlocks => Volatile.Read(ref _blocks);
		internal bool ProcessingSupported { get; private set; }
		internal NativePcmSession()
		{
			if (VpePcmAbi() != 1) throw new InvalidOperationException("Unsupported native PCM ABI.");
			_context = VpePcmCreate();
			if (_context == IntPtr.Zero) throw new InvalidOperationException("The native PCM graph could not be allocated.");
			VpePcmEnableStream(_context, 0);
			try { ProcessingSupported = VpePcmDspAbi() == 1; }
			catch (EntryPointNotFoundException) { ProcessingSupported = false; }
		}
		internal bool PrepareProcessing(int rate) => ProcessingSupported && VpePcmPrepareDsp(Context, rate) != 0;
		internal bool SetProcessing(float[] values) => ProcessingSupported && VpePcmSetProcessing(Context, values, values.Length) != 0;
		internal void ProcessingMeters(float[] values) { if (ProcessingSupported) VpePcmProcessingMeters(Context, values, values.Length); }
		internal void Preview(int bus, bool enabled) { if (ProcessingSupported) VpePcmPreview(Context, bus, enabled ? 1 : 0); }
		internal bool PreviewClip(int clip, int bus)
		{
			if (!ProcessingSupported) return false;
			try { return VpePcmPreviewClip(Context, clip, bus) != 0; }
			catch (EntryPointNotFoundException) { return false; }
		}
		internal float EqResponse(int route, float hz) => ProcessingSupported ? VpePcmEqResponse(Context, route, hz) : 0;

		/// <summary>Whether the graph plays through its own low-latency device stream instead of a Unity audio callback.</summary>
		internal bool HasDeviceOutput => Volatile.Read(ref _output) != IntPtr.Zero;
		internal bool HasAsioOutput => HasDeviceOutput && _asio;

		/// <summary>
		/// Opens the native device stream (Windows WASAPI). While it runs, it is the graph's only renderer, so
		/// <see cref="Render"/> must not be fed by a Unity callback. False when unsupported or no device opened.
		/// </summary>
		internal bool StartDeviceOutput(int rate, bool paused, bool exclusive = false, int bufferFrames = 0)
		{
			StopDeviceOutput();
			IntPtr output;
			try { output = VpePcmOutputCreateEx(Context, rate, paused ? 1 : 0, exclusive ? 1 : 0, bufferFrames); }
			catch (EntryPointNotFoundException) {
				if (exclusive) return false;
				try { output = VpePcmOutputCreate(Context, rate, paused ? 1 : 0); }
				catch (EntryPointNotFoundException) { return false; }
			}
			if (output == IntPtr.Zero) return false;
			_asio = false;
			Volatile.Write(ref _output, output);
			Interlocked.Increment(ref _blocks); // creation renders the stream's prefill
			return true;
		}

		/// <summary>
		/// Opens an ASIO driver for the graph. The device runs at <paramref name="rate"/>; 0 frames keeps the
		/// driver's preferred buffer size. Only one ASIO stream can exist per process.
		/// </summary>
		internal bool StartAsioOutput(string driver, int rate, int bufferFrames, bool paused, out string error)
		{
			StopDeviceOutput();
			var message = new StringBuilder(256);
			IntPtr output;
			try { output = VpeAsioOutputCreate(Context, driver, rate, bufferFrames, paused ? 1 : 0, message, message.Capacity); }
			catch (DllNotFoundException) { error = "the ASIO library is not installed"; return false; }
			catch (EntryPointNotFoundException) { error = "the ASIO library is outdated"; return false; }
			error = message.ToString();
			if (output == IntPtr.Zero) return false;
			_asio = true;
			Volatile.Write(ref _output, output);
			Interlocked.Increment(ref _blocks);
			return true;
		}

		/// <summary>
		/// Opens an ASIO driver's own settings window, through the playing stream when it uses that driver,
		/// otherwise by loading the driver just for the panel until <see cref="CloseAsioControlPanel"/>.
		/// </summary>
		internal static bool ShowAsioControlPanel(string driver)
		{
			try { return VpeAsioShowControlPanel(driver) != 0; }
			catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) { return false; }
		}
		internal static void CloseAsioControlPanel()
		{
			try { VpeAsioCloseControlPanel(); }
			catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) { }
		}

		/// <summary>
		/// Stops Unity callbacks from rendering and waits for one already inside <see cref="Render"/>, so
		/// a device stream can become the graph's only renderer. A callback that captured the session
		/// earlier counts itself in before it checks the gate, so it either renders before this returns or not at all.
		/// </summary>
		internal void CloseCallbackRendering()
		{
			Interlocked.Exchange(ref _renderClosed, 1);
			SpinWait.SpinUntil(() => Volatile.Read(ref _readers) == 0);
		}
		/// <summary>Lets Unity callbacks render again, for a fallback carrier.</summary>
		internal void OpenCallbackRendering() => Interlocked.Exchange(ref _renderClosed, 0);

		/// <summary>Opens one more live PCM input on a bus (1..7), or -1 when they are all in use or the library is older.</summary>
		internal int OpenStream(int bus)
		{
			try { return VpePcmStreamOpen(Context, bus); }
			catch (EntryPointNotFoundException) { return -1; }
		}
		internal void CloseStream(int stream) { var context = Context; if (context != IntPtr.Zero) VpePcmStreamClose(context, stream); }
		// These may run on a producer's thread, so they count as readers: Dispose waits for them before freeing the graph.
		internal void SetStreamGain(int stream, float gain)
		{
			Interlocked.Increment(ref _readers);
			try { var context = Context; if (context != IntPtr.Zero) VpePcmStreamSetGain(context, stream, gain); }
			finally { Interlocked.Decrement(ref _readers); }
		}
		internal void FlushStream(int stream)
		{
			Interlocked.Increment(ref _readers);
			try { var context = Context; if (context != IntPtr.Zero) VpePcmStreamFlush(context, stream); }
			finally { Interlocked.Decrement(ref _readers); }
		}
		/// <summary>Jitter buffer of a stream in input frames: 0 none, -1 sized from the producer's blocks, -2 that after the first underflow.</summary>
		internal bool SetStreamBuffer(int stream, int frames)
		{
			Interlocked.Increment(ref _readers);
			try {
				var context = Context;
				return context != IntPtr.Zero && VpePcmStreamSetBuffer(context, stream, frames) != 0;
			}
			catch (EntryPointNotFoundException) { return false; }
			finally { Interlocked.Decrement(ref _readers); }
		}
		/// <summary>Queued frames, buffer target, underflows, overflows, discontinuities and catch-up drops of a stream.</summary>
		internal bool StreamMetrics(int stream, float[] values)
		{
			var context = Context;
			if (context == IntPtr.Zero) return false;
			try { return VpePcmStreamMetrics(context, stream, values, values.Length) != 0; }
			catch (EntryPointNotFoundException) { return false; }
		}
		/// <summary>Pushes into an opened stream from its producer's thread. Counted like a render call, so disposal waits for it.</summary>
		internal bool PushStream(int stream, IntPtr samples, int frames, int channels, int rate, int encoding, long sequence)
		{
			Interlocked.Increment(ref _readers);
			try {
				var context = Context;
				return context != IntPtr.Zero && VpePcmStreamPushTo(context, stream, samples, frames, channels, rate, encoding, sequence) == frames;
			} finally { Interlocked.Decrement(ref _readers); }
		}

		/// <summary>Whether this build ships the optional ASIO library.</summary>
		internal static bool AsioAvailable
		{
			get {
				try { VpeAsioDriverCount(); return true; }
				catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) { return false; }
			}
		}

		/// <summary>Installed ASIO drivers, empty when the ASIO library is missing.</summary>
		internal static string[] AsioDrivers()
		{
			try {
				var names = new string[VpeAsioDriverCount()];
				for (var i = 0; i < names.Length; i++) {
					var name = new StringBuilder(64);
					names[i] = VpeAsioDriverName(i, name, name.Capacity) != 0 ? name.ToString() : string.Empty;
				}
				return names;
			} catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException) {
				return Array.Empty<string>();
			}
		}
		internal void StopDeviceOutput()
		{
			var output = Interlocked.Exchange(ref _output, IntPtr.Zero);
			if (output == IntPtr.Zero) return;
			if (_asio) VpeAsioOutputDestroy(output); else VpePcmOutputDestroy(output);
		}
		internal void SetDeviceOutputPaused(bool paused)
		{
			var output = Volatile.Read(ref _output);
			if (output == IntPtr.Zero) return;
			if (_asio) VpeAsioOutputSetPaused(output, paused ? 1 : 0); else VpePcmOutputSetPaused(output, paused ? 1 : 0);
		}
		internal bool DeviceOutputMetrics(float[] values)
		{
			var output = Volatile.Read(ref _output);
			if (output == IntPtr.Zero) return false;
			return (_asio ? VpeAsioOutputMetrics(output, values, values.Length) : VpePcmOutputMetrics(output, values, values.Length)) != 0;
		}
		internal void Render(float[] data, int channels, int rate, bool paused)
		{
			Interlocked.Increment(ref _readers);
			try {
				var context = Context;
				if (context == IntPtr.Zero || Volatile.Read(ref _renderClosed) != 0 || VpePcmRender(context, data, data.Length / channels, channels, rate, paused ? 1 : 0) == 0) Array.Clear(data, 0, data.Length);
				else Interlocked.Increment(ref _blocks);
			} finally { Interlocked.Decrement(ref _readers); }
		}
		public void Dispose()
		{
			StopDeviceOutput(); // joins the device thread, the graph's other renderer
			var context = Interlocked.Exchange(ref _context, IntPtr.Zero);
			if (context == IntPtr.Zero) return;
			// No audio callback waits for this main-thread teardown. The bounded native
			// render has no locks or main-thread dependencies; new callbacks see zero.
			SpinWait.SpinUntil(() => Volatile.Read(ref _readers) == 0);
			VpePcmDestroy(context);
		}
	}
}
