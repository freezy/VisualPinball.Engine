// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VisualPinball.Plugins;
using VisualPinball.Engine.Game.Engines;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Publishes a table's gamelogic engine to plugins: its controller events, displays, segment displays, native outputs and audio
	/// become resources in the <see cref="PluginResourceRegistry"/>, where plugins such as PinUP, the B2S backglass and DMD bind them.
	/// </summary>
	/// <remarks>
	/// The Player creates one per game after loading the table, before the table starts and before
	/// <see cref="PluginHost.PrepareAndStartAsync"/>, so the resources exist when plugins prepare. It owns the instance in its game
	/// scope and disposes it after the plugins have stopped. Every resource carries the game session, and its ID starts with
	/// <see cref="ControllerId"/>:
	/// <list type="table">
	/// <listheader><term>Resource ID</term><description>Kind, role and when it is published</description></listheader>
	/// <item>
	/// <term><c>&lt;controller&gt;</c></term>
	/// <description>
	/// <see cref="ResourceKind.Controller"/>, role <c>controller</c>: coil, lamp, GI and switch changes by mapped ID. Always.
	/// </description>
	/// </item>
	/// <item>
	/// <term><c>&lt;controller&gt;/outputs</c></term>
	/// <description>
	/// <see cref="ResourceKind.Outputs"/>, role <c>outputs</c>: every output by native number, when the engine implements
	/// <see cref="IGamelogicNativeOutputs"/>.
	/// </description>
	/// </item>
	/// <item>
	/// <term><c>&lt;controller&gt;/audio/rom</c></term>
	/// <description>
	/// <see cref="ResourceKind.Audio"/>, role <c>rom-mix</c>, when the engine implements <see cref="IGamelogicAudioResourceProvider"/>.
	/// </description>
	/// </item>
	/// <item>
	/// <term><c>&lt;controller&gt;/display/&lt;id&gt;</c></term>
	/// <description>
	/// <see cref="ResourceKind.Display"/>, role <c>dmd</c>: one per <see cref="DotMatrixDisplayComponent"/> in the table, and one
	/// per dot-matrix display the engine announces through <see cref="IGamelogicEngine.OnDisplaysRequested"/>.
	/// </description>
	/// </item>
	/// <item>
	/// <term><c>&lt;controller&gt;/segments/&lt;id&gt;</c></term>
	/// <description>
	/// <see cref="ResourceKind.Segments"/>, role <c>segments</c>: one per segment display the engine announces (a display with a
	/// height of 1 or less), with its layout from <see cref="ISegmentDisplayMetadata"/>.
	/// </description>
	/// </item>
	/// </list>
	/// Display frames are converted with <see cref="FromLegacy"/>; frames of unknown displays or formats are dropped. The display
	/// encodings come from <see cref="IDisplayResourceMetadata"/>. Without it, displays offer
	/// <see cref="DisplayEncoding.Luminance8"/> and <see cref="DisplayEncoding.Rgb24"/>, plus <see cref="DisplayEncoding.Gray2"/>
	/// and <see cref="DisplayEncoding.Gray4"/> when the engine implements <see cref="IDisplayFrameFormatPreference"/>, through which
	/// a consumer's request for raw identification frames is forwarded.
	/// <para>
	/// Create and dispose it on the owner thread. The engine must raise its display events there (they throw otherwise); coil,
	/// lamp, switch and native output changes raised on other threads are posted to the owner thread.
	/// </para>
	/// <example>
	/// From the Player's plugin controller:
	/// <code>
	/// if (engine != null)
	///     _gameScope.Own(new GamelogicPluginResources(table, engine, identity, session, Host.Scheduler, Host.Resources, Debug.LogException));
	/// await Host.PrepareAndStartAsync(identity, path, title, identity.Id + "/controller/0", session, cancellation);
	/// </code>
	/// </example>
	/// </remarks>
	public sealed class GamelogicPluginResources : IDisposable
	{
		private sealed class Display : IDisposable
		{
			internal readonly DisplayResourceSource Source;
			internal readonly IDisposable Publication;
			internal readonly int Width;
			internal readonly int Height;
			internal readonly long Instance;
			private readonly IDisposable _format;
			internal Display(DisplayResourceSource source, IDisposable publication, int width, int height, long instance, IDisposable format)
			{ Source = source; Publication = publication; Width = width; Height = height; Instance = instance; _format = format; }
			public void Dispose() { Publication.Dispose(); Source.Dispose(); _format?.Dispose(); }
		}
		/// <summary>A segment display published as <see cref="ResourceKind.Segments"/>.</summary>
		private sealed class Segments : IDisposable
		{
			internal readonly SegmentResourceSource Source;
			internal readonly IDisposable Publication;
			internal readonly string Layout;
			internal readonly string Hardware;
			internal float[] Brightness = Array.Empty<float>();
			internal Segments(SegmentResourceSource source, IDisposable publication, string layout, string hardware)
			{ Source = source; Publication = publication; Layout = layout; Hardware = hardware; }
			public void Dispose() { Publication.Dispose(); Source.Dispose(); }
		}
		/// <summary>Forwards native outputs without exposing the listener on the public type.</summary>
		private sealed class NativeOutputs : INativeOutputListener
		{
			private readonly GamelogicPluginResources _owner;
			internal NativeOutputs(GamelogicPluginResources owner) { _owner = owner; }
			public void NativeOutputChanged(ControllerEventKind kind, int number, double value, ControllerValueSemantics semantics, double maximum) => _owner.EmitNative(kind, number, value, semantics, maximum);
			public void NativeOutputPollEnd() => _owner.EmitNative(ControllerEventKind.PollEnd, -1, 0, ControllerValueSemantics.Binary, 1);
		}
		private static long _instance;
		private readonly IGamelogicEngine _engine;
		private readonly Dictionary<string, Segments> _segments = new Dictionary<string, Segments>(StringComparer.Ordinal);
		private readonly ControllerResourceSource _native;
		private readonly string[] _numberNames = new string[1024];
		private readonly OwnerScheduler _scheduler;
		private readonly PluginResourceRegistry _registry;
		private readonly Dictionary<string, Display> _displays = new Dictionary<string, Display>(StringComparer.Ordinal);
		private readonly ResourceScope _scope = new ResourceScope();
		private readonly ControllerResourceSource _controller;
		private readonly DisplayFormatNegotiator _group;
		private readonly string _controllerId;
		private readonly string _provider;
		private readonly long _session;
		private readonly Action<Exception> _fault;
		private readonly DisplayEncoding _formats;
		private readonly DisplayEncoding _defaultFormat;
		private long _sequence;
		private bool _disposed;
		/// <summary>
		/// Publishes the engine's controller, native outputs and audio and the table's dot-matrix displays, and subscribes to the
		/// engine's events. Displays and segment displays that the engine announces later are published when it announces them.
		/// </summary>
		/// <param name="table">
		/// The table's root object. Every <see cref="DotMatrixDisplayComponent"/> under it, including inactive ones, is published up
		/// front, so plugins can bind the DMD before the engine announces its displays.
		/// </param>
		/// <param name="engine">The table's gamelogic engine.</param>
		/// <param name="identity">
		/// The game. Its <see cref="GameIdentity.Provider"/> becomes the resources' provider, and its <see cref="GameIdentity.Id"/>
		/// the start of <see cref="ControllerId"/>.
		/// </param>
		/// <param name="session">The game session number, the same one passed to <see cref="PluginHost.PrepareAndStartAsync"/>.</param>
		/// <param name="scheduler">The host's owner-thread scheduler, used to check for and post to the owner thread.</param>
		/// <param name="registry">The registry to publish into, normally <see cref="PluginHost.Resources"/>.</param>
		/// <param name="fault">
		/// Receives exceptions thrown by subscribers of the published sources, and errors for display frames whose buffer is too
		/// small. The Player passes <c>Debug.LogException</c>.
		/// </param>
		public GamelogicPluginResources(GameObject table, IGamelogicEngine engine, GameIdentity identity, long session,
			OwnerScheduler scheduler, PluginResourceRegistry registry, Action<Exception> fault)
		{
			_engine = engine; _scheduler = scheduler; _registry = registry; _provider = identity.Provider; _controllerId = identity.Id + "/controller/0"; _session = session; _fault = fault;
			_controller = new ControllerResourceSource(fault);
			var metadata = engine as IDisplayResourceMetadata;
			_formats = metadata?.DisplayFormats ?? (engine is IDisplayFrameFormatPreference
				? DisplayEncoding.Gray2 | DisplayEncoding.Gray4 | DisplayEncoding.Luminance8 | DisplayEncoding.Rgb24
				: DisplayEncoding.Luminance8 | DisplayEncoding.Rgb24);
			_defaultFormat = metadata?.DefaultDisplayFormat ?? DisplayEncoding.Luminance8;
			if (metadata?.FormatAppliesToAllDisplays == true) _group = _scope.Own(new DisplayFormatNegotiator(_formats, _defaultFormat,
				format => (engine as IDisplayFrameFormatPreference)?.RequestDisplayFrameFormat("", ToLegacy(format)),
				() => { foreach (string display in _displays.Keys.ToArray()) _registry.NotifyChanged(DisplayResourceId(display)); }));
			_scope.Own(_controller);
			_scope.Own(registry.Publish(new PluginResourceDescriptor(_controllerId, _provider, _controllerId, engine.Name, "controller", ResourceKind.Controller, session, Interlocked.Increment(ref _instance)), _controller));
			if (engine is IGamelogicNativeOutputs native) {
				// Every output by native number, for consumers such as backglasses that address ROM lamps directly.
				_native = _scope.Own(new ControllerResourceSource(ResourceKind.Outputs, fault));
				_scope.Own(registry.Publish(new PluginResourceDescriptor(_controllerId + "/outputs", _provider, _controllerId, engine.Name + " outputs", "outputs", ResourceKind.Outputs, session, Interlocked.Increment(ref _instance)), _native));
				native.SetNativeOutputListener(new NativeOutputs(this));
				_scope.Own(new Subscription(() => native.SetNativeOutputListener(null)));
			}
			if (engine is IGamelogicAudioResourceProvider audio) _scope.Own(registry.Publish(new PluginResourceDescriptor(_controllerId + "/audio/rom", _provider, _controllerId, engine.Name + " audio", "rom-mix", ResourceKind.Audio, session, Interlocked.Increment(ref _instance)), audio.AudioSource));
			engine.OnDisplaysRequested += DisplaysRequested;
			engine.OnDisplayUpdateFrame += DisplayFrame;
			engine.OnCoilChanged += Coil;
			engine.OnLampChanged += Lamp;
			engine.OnLampsChanged += Lamps;
			engine.OnSwitchChanged += Switch;
			foreach (var display in table.GetComponentsInChildren<DotMatrixDisplayComponent>(true)) AddDisplay(display.Id, display.Width, display.Height);
		}
		/// <summary>
		/// ID of the published controller resource and prefix of every other resource ID: the game's <see cref="GameIdentity.Id"/>
		/// followed by <c>/controller/0</c>, such as <c>pinmame:t2_l8/controller/0</c>. It matches the
		/// <see cref="PluginGameContext.ControllerId"/> the Player passes to plugins.
		/// </summary>
		public string ControllerId => _controllerId;
		private void DisplaysRequested(object sender, RequestedDisplays args)
		{
			_scheduler.VerifyOwner();
			foreach (var display in args.Displays) {
				if (display.Height > 1) AddDisplay(display.Id, display.Width, display.Height);
				else if (display.Width > 0) AddSegments(display.Id, display.Width);
			}
		}
		private void AddSegments(string id, int elements)
		{
			if (_disposed || _segments.ContainsKey(id)) return;
			string layout = "seg16", hardware = "";
			if (_engine is ISegmentDisplayMetadata metadata && metadata.TryGetSegmentLayout(id, out var declared, out var declaredHardware)) { layout = declared; hardware = declaredHardware; }
			// Displays are numbered in announcement order, so consumers can lay out digits before any frame arrives.
			var source = new SegmentResourceSource(_segments.Count, elements, layout, hardware, _fault);
			var descriptor = new PluginResourceDescriptor(_controllerId + "/segments/" + id, _provider, _controllerId, id, "segments", ResourceKind.Segments, _session, Interlocked.Increment(ref _instance));
			_segments.Add(id, new Segments(source, _registry.Publish(descriptor, source), layout, hardware));
		}
		private void SegmentFrame(Segments display, DisplayFrameData args)
		{
			// One 16-bit segment mask per element, as PinMAME reports it.
			if (args.Data == null || args.Data.Length < 2) return;
			int elements = args.Data.Length / 2;
			if (display.Brightness.Length != elements * 16) display.Brightness = new float[elements * 16];
			var brightness = display.Brightness;
			for (int element = 0; element < elements; element++) {
				int mask = args.Data[element * 2] | args.Data[element * 2 + 1] << 8;
				for (int bit = 0; bit < 16; bit++) brightness[element * 16 + bit] = (mask >> bit & 1) != 0 ? 1f : 0f;
			}
			display.Source.Publish(new PluginSegmentFrame(display.Layout, elements, 16, brightness, display.Hardware));
		}
		private void AddDisplay(string id, int width, int height)
		{
			if (_disposed || width <= 0 || height <= 0) return;
			if (_displays.TryGetValue(id, out var existing)) {
				if (existing.Width == width && existing.Height == height) return;
				existing.Dispose(); _displays.Remove(id);
			}
			var format = _group ?? new DisplayFormatNegotiator(_formats, _defaultFormat,
				encoding => (_engine as IDisplayFrameFormatPreference)?.RequestDisplayFrameFormat(id, ToLegacy(encoding)),
				() => _registry.NotifyChanged(DisplayResourceId(id)));
			var source = new DisplayResourceSource(format.Acquire, _fault);
			long instance = Interlocked.Increment(ref _instance);
			var descriptor = new PluginResourceDescriptor(DisplayResourceId(id), _provider, _controllerId, id, "dmd", ResourceKind.Display, _session, instance, ResourceView.Original, _formats);
			_displays.Add(id, new Display(source, _registry.Publish(descriptor, source), width, height, instance, _group == null ? format : null));
		}
		private string DisplayResourceId(string display) => _controllerId + "/display/" + display;
		private void DisplayFrame(object sender, DisplayFrameData args)
		{
			_scheduler.VerifyOwner();
			if (_disposed) return;
			if (args.Format == DisplayFrameFormat.Segment && _segments.TryGetValue(args.Id, out var segments)) { SegmentFrame(segments, args); return; }
			if (!_displays.TryGetValue(args.Id, out var display)) return;
			var format = FromLegacy(args.Format);
			if (format == DisplayEncoding.None) return;
			int stride = display.Width * (format == DisplayEncoding.Rgb24 ? 3 : 1);
			if (args.Data == null || args.Data.Length < stride * display.Height) { _fault(new ArgumentException("Invalid frame buffer for " + args.Id)); return; }
			display.Source.Publish(new PluginDisplayFrame(args.Data, display.Width, display.Height, stride, format, ++_sequence, Timestamp(), display.Instance));
		}
		private void Emit(ControllerEventKind kind, string id, double value)
		{
			if (_disposed) return;
			if (!_scheduler.IsOwner) { _scheduler.Post(() => Emit(kind, id, value)); return; }
			_controller.Publish(new PluginControllerEvent(kind, id, value, ++_sequence, Timestamp()));
		}
		private void EmitNative(ControllerEventKind kind, int number, double value, ControllerValueSemantics semantics, double maximum)
		{
			if (_disposed || _native == null) return;
			if (!_scheduler.IsOwner) { _scheduler.Post(() => EmitNative(kind, number, value, semantics, maximum)); return; }
			_native.Publish(new PluginControllerEvent(kind, NumberName(number), value, ++_sequence, Timestamp(), number, semantics, maximum));
		}
		private string NumberName(int number)
		{
			if (number < 0) return "";
			if (number >= _numberNames.Length) return number.ToString(System.Globalization.CultureInfo.InvariantCulture);
			return _numberNames[number] ??= number.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
		private void Coil(object sender, CoilEventArgs args) => Emit(ControllerEventKind.Coil, args.Id, args.Value);
		private void Lamp(object sender, LampEventArgs args) => Emit(args.Source == LampSource.GI ? ControllerEventKind.GeneralIllumination : ControllerEventKind.Lamp, args.Id, args.Value);
		private void Lamps(object sender, LampsEventArgs args) { foreach (var lamp in args.LampsChanged) Lamp(sender, lamp); }
		private void Switch(object sender, SwitchEventArgs2 args) => Emit(ControllerEventKind.Switch, args.Id, args.IsEnabled ? 1 : 0);
		private static long Timestamp() => (long)(Stopwatch.GetTimestamp() * (1000000d / Stopwatch.Frequency));
		/// <summary>
		/// Converts a gamelogic engine's <see cref="DisplayFrameFormat"/> to the plugin <see cref="DisplayEncoding"/>:
		/// <see cref="DisplayFrameFormat.Dmd2"/> to <see cref="DisplayEncoding.Gray2"/>, <see cref="DisplayFrameFormat.Dmd4"/> to
		/// <see cref="DisplayEncoding.Gray4"/>, <see cref="DisplayFrameFormat.Dmd8"/> to <see cref="DisplayEncoding.Luminance8"/>,
		/// and <see cref="DisplayFrameFormat.Dmd24"/> and <see cref="DisplayFrameFormat.Video24"/> to <see cref="DisplayEncoding.Rgb24"/>.
		/// </summary>
		/// <param name="format">The engine's frame format.</param>
		/// <returns>
		/// The matching encoding, or <see cref="DisplayEncoding.None"/> for the segment, alphanumeric and numeric formats, which are
		/// not published as display frames.
		/// </returns>
		public static DisplayEncoding FromLegacy(DisplayFrameFormat format) => format switch {
			DisplayFrameFormat.Dmd2 => DisplayEncoding.Gray2, DisplayFrameFormat.Dmd4 => DisplayEncoding.Gray4,
			DisplayFrameFormat.Dmd8 => DisplayEncoding.Luminance8, DisplayFrameFormat.Dmd24 => DisplayEncoding.Rgb24,
			DisplayFrameFormat.Video24 => DisplayEncoding.Rgb24, _ => DisplayEncoding.None
		};
		/// <summary>
		/// Converts a plugin <see cref="DisplayEncoding"/> to the <see cref="DisplayFrameFormat"/> to request from a gamelogic engine
		/// through <see cref="IDisplayFrameFormatPreference.RequestDisplayFrameFormat"/>: <see cref="DisplayEncoding.Gray2"/> to
		/// <see cref="DisplayFrameFormat.Dmd2"/>, <see cref="DisplayEncoding.Gray4"/> to <see cref="DisplayFrameFormat.Dmd4"/>,
		/// <see cref="DisplayEncoding.Rgb24"/> to <see cref="DisplayFrameFormat.Dmd24"/>, and anything else to
		/// <see cref="DisplayFrameFormat.Dmd8"/>. The DMD plugin also uses it to hand frames to its native pipeline.
		/// </summary>
		/// <param name="format">
		/// A single encoding. <see cref="DisplayEncoding.Luminance8"/>, <see cref="DisplayEncoding.Rgba32"/>,
		/// <see cref="DisplayEncoding.None"/> and combined flags all map to <see cref="DisplayFrameFormat.Dmd8"/>.
		/// </param>
		/// <returns>The matching engine frame format.</returns>
		public static DisplayFrameFormat ToLegacy(DisplayEncoding format) => format switch {
			DisplayEncoding.Gray2 => DisplayFrameFormat.Dmd2, DisplayEncoding.Gray4 => DisplayFrameFormat.Dmd4,
			DisplayEncoding.Rgb24 => DisplayFrameFormat.Dmd24, _ => DisplayFrameFormat.Dmd8
		};
		/// <summary>
		/// Unsubscribes from the engine, unpublishes every resource, removes the native output listener and, if raw identification
		/// frames were requested, asks the engine for its default display format again. Safe to call more than once. Call it on the
		/// owner thread after the plugins have stopped, as the Player does when it disposes its game scope.
		/// </summary>
		public void Dispose()
		{
			if (_disposed) return; _disposed = true;
			_engine.OnDisplaysRequested -= DisplaysRequested; _engine.OnDisplayUpdateFrame -= DisplayFrame;
			_engine.OnCoilChanged -= Coil; _engine.OnLampChanged -= Lamp; _engine.OnLampsChanged -= Lamps; _engine.OnSwitchChanged -= Switch;
			foreach (var display in _displays.Values) display.Dispose(); _displays.Clear();
			foreach (var segments in _segments.Values) segments.Dispose(); _segments.Clear(); _scope.Dispose();
		}
	}
}
