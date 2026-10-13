// SPDX-License-Identifier: MIT
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
	/// <summary>Adapts engine output to VPE-owned resources before the inactive table starts.</summary>
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
		private static long _instance;
		private readonly IGamelogicEngine _engine;
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
				format => (engine as IDisplayFrameFormatPreference)?.RequestDisplayFrameFormat("", ToLegacy(format))));
			_scope.Own(_controller);
			_scope.Own(registry.Publish(new PluginResourceDescriptor(_controllerId, _provider, _controllerId, engine.Name, "controller", ResourceKind.Controller, session, Interlocked.Increment(ref _instance)), _controller));
			if (engine is IGamelogicAudioResourceProvider audio) _scope.Own(registry.Publish(new PluginResourceDescriptor(_controllerId + "/audio/rom", _provider, _controllerId, engine.Name + " audio", "rom-mix", ResourceKind.Audio, session, Interlocked.Increment(ref _instance)), audio.AudioSource));
			engine.OnDisplaysRequested += DisplaysRequested;
			engine.OnDisplayUpdateFrame += DisplayFrame;
			engine.OnCoilChanged += Coil;
			engine.OnLampChanged += Lamp;
			engine.OnLampsChanged += Lamps;
			engine.OnSwitchChanged += Switch;
			foreach (var display in table.GetComponentsInChildren<DotMatrixDisplayComponent>(true)) AddDisplay(display.Id, display.Width, display.Height);
		}
		public string ControllerId => _controllerId;
		private void DisplaysRequested(object sender, RequestedDisplays args)
		{
			_scheduler.VerifyOwner();
			foreach (var display in args.Displays) if (display.Height > 1) AddDisplay(display.Id, display.Width, display.Height);
		}
		private void AddDisplay(string id, int width, int height)
		{
			if (_disposed || width <= 0 || height <= 0) return;
			if (_displays.TryGetValue(id, out var existing)) {
				if (existing.Width == width && existing.Height == height) return;
				existing.Dispose(); _displays.Remove(id);
			}
			var format = _group ?? new DisplayFormatNegotiator(_formats, _defaultFormat,
				encoding => (_engine as IDisplayFrameFormatPreference)?.RequestDisplayFrameFormat(id, ToLegacy(encoding)));
			var source = new DisplayResourceSource(format.Acquire, _fault);
			long instance = Interlocked.Increment(ref _instance);
			var descriptor = new PluginResourceDescriptor(_controllerId + "/display/" + id, _provider, _controllerId, id, "dmd", ResourceKind.Display, _session, instance, ResourceView.Original, _formats);
			_displays.Add(id, new Display(source, _registry.Publish(descriptor, source), width, height, instance, _group == null ? format : null));
		}
		private void DisplayFrame(object sender, DisplayFrameData args)
		{
			_scheduler.VerifyOwner();
			if (_disposed || !_displays.TryGetValue(args.Id, out var display)) return;
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
		private void Coil(object sender, CoilEventArgs args) => Emit(ControllerEventKind.Coil, args.Id, args.Value);
		private void Lamp(object sender, LampEventArgs args) => Emit(args.Source == LampSource.GI ? ControllerEventKind.GeneralIllumination : ControllerEventKind.Lamp, args.Id, args.Value);
		private void Lamps(object sender, LampsEventArgs args) { foreach (var lamp in args.LampsChanged) Lamp(sender, lamp); }
		private void Switch(object sender, SwitchEventArgs2 args) => Emit(ControllerEventKind.Switch, args.Id, args.IsEnabled ? 1 : 0);
		private static long Timestamp() => (long)(Stopwatch.GetTimestamp() * (1000000d / Stopwatch.Frequency));
		public static DisplayEncoding FromLegacy(DisplayFrameFormat format) => format switch {
			DisplayFrameFormat.Dmd2 => DisplayEncoding.Gray2, DisplayFrameFormat.Dmd4 => DisplayEncoding.Gray4,
			DisplayFrameFormat.Dmd8 => DisplayEncoding.Luminance8, DisplayFrameFormat.Dmd24 => DisplayEncoding.Rgb24,
			DisplayFrameFormat.Video24 => DisplayEncoding.Rgb24, _ => DisplayEncoding.None
		};
		public static DisplayFrameFormat ToLegacy(DisplayEncoding format) => format switch {
			DisplayEncoding.Gray2 => DisplayFrameFormat.Dmd2, DisplayEncoding.Gray4 => DisplayFrameFormat.Dmd4,
			DisplayEncoding.Rgb24 => DisplayFrameFormat.Dmd24, _ => DisplayFrameFormat.Dmd8
		};
		public void Dispose()
		{
			if (_disposed) return; _disposed = true;
			_engine.OnDisplaysRequested -= DisplaysRequested; _engine.OnDisplayUpdateFrame -= DisplayFrame;
			_engine.OnCoilChanged -= Coil; _engine.OnLampChanged -= Lamp; _engine.OnLampsChanged -= Lamps; _engine.OnSwitchChanged -= Switch;
			foreach (var display in _displays.Values) display.Dispose(); _displays.Clear(); _scope.Dispose();
		}
	}
}
