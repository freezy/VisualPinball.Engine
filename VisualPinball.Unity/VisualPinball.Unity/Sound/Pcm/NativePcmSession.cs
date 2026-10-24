// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Runtime.InteropServices;
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
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmOutputDestroy(IntPtr output);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern void VpePcmOutputSetPaused(IntPtr output, int paused);
		[DllImport(Library, CallingConvention = CallingConvention.Cdecl)] private static extern int VpePcmOutputMetrics(IntPtr output, [Out] float[] values, int count);
		internal const int OutputMetricCount = 13;
		private IntPtr _context, _output;
		private int _readers, _blocks;
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

		/// <summary>
		/// Opens the native device stream (Windows WASAPI). While it runs, it is the graph's only renderer, so
		/// <see cref="Render"/> must not be fed by a Unity callback. False when unsupported or no device opened.
		/// </summary>
		internal bool StartDeviceOutput(int rate, bool paused)
		{
			StopDeviceOutput();
			IntPtr output;
			try { output = VpePcmOutputCreate(Context, rate, paused ? 1 : 0); }
			catch (EntryPointNotFoundException) { return false; }
			if (output == IntPtr.Zero) return false;
			Volatile.Write(ref _output, output);
			Interlocked.Increment(ref _blocks); // creation renders the stream's prefill
			return true;
		}
		internal void StopDeviceOutput()
		{
			var output = Interlocked.Exchange(ref _output, IntPtr.Zero);
			if (output != IntPtr.Zero) VpePcmOutputDestroy(output);
		}
		internal void SetDeviceOutputPaused(bool paused)
		{
			var output = Volatile.Read(ref _output);
			if (output != IntPtr.Zero) VpePcmOutputSetPaused(output, paused ? 1 : 0);
		}
		internal bool DeviceOutputMetrics(float[] values)
		{
			var output = Volatile.Read(ref _output);
			return output != IntPtr.Zero && VpePcmOutputMetrics(output, values, values.Length) != 0;
		}
		internal void Render(float[] data, int channels, int rate, bool paused)
		{
			Interlocked.Increment(ref _readers);
			try {
				var context = Context;
				if (context == IntPtr.Zero || VpePcmRender(context, data, data.Length / channels, channels, rate, paused ? 1 : 0) == 0) Array.Clear(data, 0, data.Length);
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
