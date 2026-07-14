// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Main-thread cue scheduler, compositor, and GLE display emitter for one DMD project.
	/// </summary>
	public sealed class DmdCuePlayer : IDisposable
	{
		public const double ReannounceDelaySeconds = 2.0;

		private readonly DmdProjectAsset _project;
		private readonly IDmdFrameSink _sink;
		private readonly CueScheduler _scheduler = new CueScheduler();
		private readonly Dictionary<string, DmdCueAsset> _cues =
			new Dictionary<string, DmdCueAsset>(StringComparer.Ordinal);
		private readonly List<string> _constructorDiagnostics = new List<string>();
		private readonly HashSet<string> _publishedDiagnostics = new HashSet<string>(StringComparer.Ordinal);
		private readonly Queue<CueHandle> _finishedEvents = new Queue<CueHandle>();
		private readonly int _threadId;
		private readonly int _frameRate;
		private readonly int _width;
		private readonly int _height;
		private readonly bool _canRender;
		private readonly CueRenderer _renderer;
		private readonly DmdSurface _output;
		private readonly DmdSurface _from;
		private readonly DmdSurface _to;
		private readonly RequestedDisplays _requestedDisplays;

		private DisplayFrameFormat _format;
		private byte[] _emitBuffer;
		private DisplayFrameData _frameData;
		private Transition _transition;
		private bool _started;
		private bool _disposed;
		private bool _cleared;
		private bool _dirty = true;
		private bool _hasLastTime;
		private double _lastTime;
		private double _accumulator;
		private bool _hasFirstTickTime;
		private double _firstTickTime;
		private bool _reannounced;

		public event EventHandler<CueHandle> OnCueFinished;
		public event EventHandler<string> OnValidationError;

		public DmdCuePlayer(DmdProjectAsset project, IDmdFrameSink sink)
		{
			_project = project ?? throw new ArgumentNullException(nameof(project));
			_sink = sink ?? throw new ArgumentNullException(nameof(sink));
			_threadId = Thread.CurrentThread.ManagedThreadId;
			_frameRate = math.clamp(project.FrameRate, DmdValidation.MinFrameRate, DmdValidation.MaxFrameRate);
			_width = math.clamp(project.Width, 1, DmdValidation.MaxWidth);
			_height = math.clamp(project.Height, 1, DmdValidation.MaxHeight);
			_canRender = project.Width == _width && project.Height == _height &&
			             Enum.IsDefined(typeof(DmdColorMode), project.ColorMode);
			var pixelFormat = project.ColorMode == DmdColorMode.Rgb24
				? DmdPixelFormat.Rgb24
				: DmdPixelFormat.I8;
			_renderer = new CueRenderer(project);
			_output = new DmdSurface(_width, _height, pixelFormat);
			_from = new DmdSurface(_width, _height, pixelFormat);
			_to = new DmdSurface(_width, _height, pixelFormat);
			_requestedDisplays = new RequestedDisplays(new DisplayConfig(
				string.IsNullOrWhiteSpace(project.DisplayId) ? "dmd0" : project.DisplayId, _width, _height));
			_format = DefaultFormat(project.ColorMode);
			CreateEmitBuffer();
			IndexProject();
		}

		public void Start()
		{
			EnsureUsable();
			if (_started) {
				return;
			}
			_started = true;
			_sink.RequestDisplays(_requestedDisplays);
			PublishConstructorDiagnostics();
			_output.Clear();
			if (_scheduler.Active != null) {
				RenderInstance(_to, _scheduler.Base);
				_output.CopyFrom(_to);
				BeginEnter(_scheduler.Active);
				if (!_transition.Active) {
					TryStartNaturalExit();
				}
			}
			RenderAndEmit();
			DrainFinishedEvents();
		}

		public void SetBase(string cueId, DmdParams parameters = null)
		{
			EnsureUsable();
			if (!TryGetCue(cueId, out var cue)) {
				return;
			}
			if (_scheduler.Base != null && string.Equals(_scheduler.Base.Cue.EffectiveId, cue.EffectiveId,
				StringComparison.Ordinal)) {
				_scheduler.Base.Params.MergeFrom(parameters);
			} else {
				_scheduler.SetBase(_scheduler.Create(cue, CreateParameters(cue, parameters)));
			}
			_dirty = true;
		}

		public CueHandle Play(string cueId, DmdParams parameters = null)
		{
			EnsureUsable();
			if (!TryGetCue(cueId, out var cue)) {
				return default;
			}
			var transitioningInstance = _started && _transition.Active ? _scheduler.Active : null;
			var incoming = _scheduler.Create(cue, CreateParameters(cue, parameters));
			var admission = _scheduler.Admit(incoming);
			if (admission.Kind == CueAdmissionKind.Coalesced) {
				_dirty = true;
				return admission.Instance.Handle;
			}
			if (_started && (admission.Kind == CueAdmissionKind.Activated ||
			                 admission.Kind == CueAdmissionKind.Preempted ||
			                 admission.Kind == CueAdmissionKind.Replaced)) {
				AbandonTransition(transitioningInstance);
				BeginEnter(admission.Instance);
				if (!_transition.Active) {
					TryStartNaturalExit();
				}
			}
			_dirty = true;
			DrainFinishedEvents();
			return admission.Instance.Handle;
		}

		public bool UpdateCue(CueHandle handle, DmdParams parameters)
		{
			EnsureUsable();
			if (!TryFindInstance(handle, out var instance)) {
				return false;
			}
			instance.Params.MergeFrom(parameters);
			_dirty = true;
			return true;
		}

		public bool UpdateCue(string cueIdOrKey, DmdParams parameters)
		{
			EnsureUsable();
			var instance = _scheduler.Resolve(cueIdOrKey);
			if (instance == null) {
				return false;
			}
			instance.Params.MergeFrom(parameters);
			_dirty = true;
			return true;
		}

		public bool StopCue(CueHandle handle)
		{
			EnsureUsable();
			if (!TryFindInstance(handle, out var instance)) {
				return false;
			}
			var stopped = ReferenceEquals(instance, _transition.Outgoing) || StopInstance(instance);
			DrainFinishedEvents();
			return stopped;
		}

		public bool StopCue(string cueIdOrKey)
		{
			EnsureUsable();
			var instance = _scheduler.Resolve(cueIdOrKey);
			var stopped = instance != null && StopInstance(instance);
			DrainFinishedEvents();
			return stopped;
		}

		public void StopAll()
		{
			EnsureUsable();
			AbandonTransition();
			_scheduler.ClearWaiting(Finish);
			if (_scheduler.Active != null) {
				BeginExitActive();
				if (!_transition.Active) {
					TryStartNaturalExit();
				}
			}
			_dirty = true;
			DrainFinishedEvents();
		}

		public void Tick(double timeSeconds)
		{
			EnsureUsable();
			if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds) || timeSeconds < 0d) {
				throw new ArgumentOutOfRangeException(nameof(timeSeconds));
			}
			if (!_started) {
				return;
			}
			if (!_hasFirstTickTime) {
				_hasFirstTickTime = true;
				_firstTickTime = timeSeconds;
			}
			if (!_reannounced && timeSeconds - _firstTickTime >= ReannounceDelaySeconds) {
				_sink.RequestDisplays(_requestedDisplays);
				_reannounced = true;
				// A late subscriber also missed the initial frame. Re-emit the current surface once
				// so a newly created display pipeline does not remain blank for static content.
				_dirty = true;
			}
			if (!_hasLastTime) {
				_hasLastTime = true;
				_lastTime = timeSeconds;
				return;
			}
			if (timeSeconds < _lastTime) {
				_lastTime = timeSeconds;
				_accumulator = 0d;
				return;
			}

			_accumulator += timeSeconds - _lastTime;
			_lastTime = timeSeconds;
			var frameDuration = 1d / _frameRate;
			var advanced = false;
			while (_accumulator + 1e-12 >= frameDuration) {
				_accumulator -= frameDuration;
				AdvanceOneFrame();
				advanced = true;
			}
			if (advanced && _dirty) {
				RenderAndEmit();
			}
			if (advanced) {
				DrainFinishedEvents();
			}
		}

		public void RequestFormat(DisplayFrameFormat format)
		{
			EnsureUsable();
			if (!AcceptsFormat(format) || format == _format) {
				return;
			}
			_format = format;
			CreateEmitBuffer();
			_dirty = true;
		}

		public void Dispose()
		{
			EnsureThread();
			if (_disposed) {
				return;
			}
			if (_started && !_cleared) {
				_sink.Clear(_requestedDisplays.Displays[0].Id);
				_cleared = true;
			}
			_disposed = true;
			_transition = default;
			_finishedEvents.Clear();
			_emitBuffer = null;
			_frameData = null;
		}

		private bool StopInstance(CueInstance instance)
		{
			if (!ReferenceEquals(_scheduler.Active, instance)) {
				if (!_scheduler.RemoveWaiting(instance)) {
					return false;
				}
				Finish(instance);
				return true;
			}
			AbandonTransition();
			BeginExitActive();
			if (!_transition.Active) {
				TryStartNaturalExit();
			}
			_dirty = true;
			return true;
		}

		private bool TryFindInstance(CueHandle handle, out CueInstance instance)
		{
			if (_scheduler.TryFind(handle, out instance)) {
				return true;
			}
			instance = _transition.Outgoing;
			return instance != null && instance.Handle == handle;
		}

		private void AdvanceOneFrame()
		{
			if (_scheduler.Base != null) {
				_scheduler.Base.ElapsedFrames++;
			}
			if (_transition.Active) {
				if (_scheduler.Active != null) {
					_scheduler.Active.ElapsedFrames++;
				}
				if (_transition.OutgoingLive && _transition.Outgoing != null) {
					_transition.Outgoing.ElapsedFrames++;
				}
				_transition.ElapsedFrames++;
				if (_transition.OutgoingLive) {
					AdvanceTransitionTargets();
				}
				_dirty = true;
				if (_transition.ElapsedFrames >= _transition.Spec.DurationFrames) {
					var outgoing = _transition.Outgoing;
					_transition = default;
					if (outgoing != null) {
						Finish(outgoing);
					}
				}
			} else if (_scheduler.Active != null) {
				_scheduler.Active.ElapsedFrames++;
			}

			if (!_transition.Active) {
				TryStartNaturalExit();
			}
			var visible = _scheduler.Active ?? _scheduler.Base;
			if (visible != null && _renderer.IsAnimated(visible.Cue, RenderFrame(visible), visible.State)) {
				_dirty = true;
			}
		}

		private void TryStartNaturalExit()
		{
			while (true) {
				var active = _scheduler.Active;
				if (active == null || active.Cue.DurationFrames <= 0 || active.Cue.Loop) {
					return;
				}
				if (active.ExitSuppressed) {
					if (active.ElapsedFrames < active.Cue.DurationFrames) {
						return;
					}
					CompleteSuppressedActive(active);
					continue;
				}
				var exitFrames = EffectiveExitFrames(active.Cue);
				if (exitFrames > 0) {
					if (active.Phase != CuePhase.Exiting &&
					    active.ElapsedFrames >= active.Cue.DurationFrames - exitFrames) {
						BeginExitActive();
						if (!_transition.Active) {
							continue;
						}
					}
				} else if (active.ElapsedFrames >= active.Cue.DurationFrames) {
					BeginExitActive();
					if (!_transition.Active) {
						continue;
					}
				}
				return;
			}
		}

		private void AdvanceTransitionTargets()
		{
			while (true) {
				var active = _scheduler.Active;
				if (active == null || active.Cue.DurationFrames <= 0 || active.Cue.Loop) {
					return;
				}
				var exitFrames = EffectiveExitFrames(active.Cue);
				if (exitFrames > 0 &&
				    active.ElapsedFrames >= active.Cue.DurationFrames - exitFrames) {
					active.ExitSuppressed = true;
				}
				if (active.ElapsedFrames < active.Cue.DurationFrames) {
					return;
				}
				CompleteSuppressedActive(active);
			}
		}

		private void CompleteSuppressedActive(CueInstance active)
		{
			var incoming = _scheduler.EndActive();
			if (!_transition.Active && incoming != null &&
			    incoming.Cue.EnterTransition.Type != DmdTransitionType.Cut &&
			    incoming.Cue.EnterTransition.DurationFrames > 0) {
				BeginEnter(incoming);
			}
			Finish(active);
			_dirty = true;
		}

		private static int EffectiveExitFrames(DmdCueAsset cue)
		{
			return cue.ExitTransition.Type == DmdTransitionType.Cut
				? 0
				: cue.ExitTransition.DurationFrames;
		}

		private void BeginExitActive()
		{
			var outgoing = _scheduler.Active;
			if (outgoing == null) {
				return;
			}
			outgoing.Phase = CuePhase.Exiting;
			var spec = outgoing.Cue.ExitTransition;
			var incoming = _scheduler.EndActive();
			if (spec.Type != DmdTransitionType.Cut && spec.DurationFrames > 0) {
				_transition = new Transition(spec, outgoing, true);
				_dirty = true;
				return;
			}

			if (incoming != null && incoming.Cue.EnterTransition.Type != DmdTransitionType.Cut &&
			    incoming.Cue.EnterTransition.DurationFrames > 0) {
				BeginEnter(incoming);
			}
			Finish(outgoing);
			_dirty = true;
		}

		private void BeginEnter(CueInstance incoming)
		{
			var spec = incoming.Cue.EnterTransition;
			if (spec.Type == DmdTransitionType.Cut || spec.DurationFrames <= 0) {
				incoming.Phase = CuePhase.Running;
				_transition = default;
				return;
			}
			_from.CopyFrom(_output);
			incoming.Phase = CuePhase.Entering;
			_transition = new Transition(spec, null, false);
		}

		private void AbandonTransition(CueInstance transitioningInstance = null)
		{
			if (!_transition.Active) {
				return;
			}
			var outgoing = _transition.Outgoing;
			_transition = default;
			if (outgoing != null) {
				Finish(outgoing);
			}
			transitioningInstance = transitioningInstance ?? _scheduler.Active;
			if (transitioningInstance != null) {
				transitioningInstance.Phase = CuePhase.Running;
				transitioningInstance.ExitSuppressed = false;
			}
			_dirty = true;
		}

		private void RenderAndEmit()
		{
			if (_transition.Active) {
				if (_transition.OutgoingLive) {
					RenderInstance(_from, _transition.Outgoing);
				}
				RenderVisible(_to);
				var progress = _transition.Spec.DurationFrames <= 0
					? 1f
					: (float)_transition.ElapsedFrames / _transition.Spec.DurationFrames;
				DmdTransitions.Compose(_output, _from, _to, _transition.Spec.Type,
					_transition.Spec.Direction, progress);
			} else {
				RenderVisible(_output);
			}
			Emit();
			_dirty = false;
			// Publish only after the frame is self-consistent. Handlers may dispose the player
			// or admit new work; emitting first avoids invalidating buffers mid-frame, and
			// clearing dirty first preserves dirtiness set by a reentrant admission.
			PublishVisibleDiagnostics();
		}

		private void RenderVisible(DmdSurface destination)
		{
			RenderInstance(destination, _scheduler.Active ?? _scheduler.Base);
		}

		private void RenderInstance(DmdSurface destination, CueInstance instance)
		{
			destination.Clear();
			if (!_canRender || instance == null) {
				return;
			}
			var frame = RenderFrame(instance);
			_renderer.Render(destination, instance.Cue, math.max(0, frame), instance.Params, instance.State,
				instance.Diagnostics);
		}

		private static int RenderFrame(CueInstance instance)
		{
			var frame = instance.ElapsedFrames;
			return instance.Cue.DurationFrames > 0 && instance.Cue.Loop
				? frame % instance.Cue.DurationFrames
				: frame;
		}

		private void Emit()
		{
			switch (_format) {
				case DisplayFrameFormat.Dmd2:
					DmdQuantizer.I8ToDmd2(_output.Data, _emitBuffer);
					break;
				case DisplayFrameFormat.Dmd4:
					DmdQuantizer.I8ToDmd4(_output.Data, _emitBuffer);
					break;
				case DisplayFrameFormat.Dmd8:
				case DisplayFrameFormat.Dmd24:
					Buffer.BlockCopy(_output.Data, 0, _emitBuffer, 0, _emitBuffer.Length);
					break;
			}
			_sink.UpdateFrame(_frameData);
		}

		private void CreateEmitBuffer()
		{
			var length = checked(_width * _height * (_format == DisplayFrameFormat.Dmd24 ? 3 : 1));
			_emitBuffer = new byte[length];
			_frameData = new DisplayFrameData(
				string.IsNullOrWhiteSpace(_project.DisplayId) ? "dmd0" : _project.DisplayId, _format, _emitBuffer);
		}

		private bool AcceptsFormat(DisplayFrameFormat format)
		{
			switch (_project.ColorMode) {
				case DmdColorMode.Mono4:
					return format == DisplayFrameFormat.Dmd2 || format == DisplayFrameFormat.Dmd4 ||
					       format == DisplayFrameFormat.Dmd8;
				case DmdColorMode.Mono16:
					return format == DisplayFrameFormat.Dmd4 || format == DisplayFrameFormat.Dmd8;
				case DmdColorMode.Rgb24:
					return format == DisplayFrameFormat.Dmd24;
				default:
					return false;
			}
		}

		private static DisplayFrameFormat DefaultFormat(DmdColorMode mode)
		{
			return mode == DmdColorMode.Mono4 ? DisplayFrameFormat.Dmd2 :
				mode == DmdColorMode.Rgb24 ? DisplayFrameFormat.Dmd24 : DisplayFrameFormat.Dmd4;
		}

		private DmdParams CreateParameters(DmdCueAsset cue, DmdParams supplied)
		{
			var result = new DmdParams();
			if (cue.Parameters != null) {
				foreach (var parameter in cue.Parameters) {
					var value = parameter.DefaultValue;
					switch (value.Type) {
						case DmdParamType.Integer:
							result.Set(value.Name, value.IntValue);
							break;
						case DmdParamType.Float:
							result.Set(value.Name, value.FloatValue);
							break;
						case DmdParamType.String:
							result.Set(value.Name, value.StringValue);
							break;
						case DmdParamType.Boolean:
							result.Set(value.Name, value.BoolValue);
							break;
					}
				}
			}
			result.MergeFrom(supplied);
			return result;
		}

		private void IndexProject()
		{
			DmdValidationResult projectValidation = null;
			try {
				projectValidation = _project.Validate();
			} catch (Exception exception) when (!(exception is OutOfMemoryException)) {
				_constructorDiagnostics.Add($"Project validation failed: {exception.Message}");
			}
			if (projectValidation != null) {
				foreach (var diagnostic in projectValidation.Diagnostics) {
					_constructorDiagnostics.Add(diagnostic.ToString());
				}
			}
			if (_project.Cues == null) {
				return;
			}
			var seen = new HashSet<string>(StringComparer.Ordinal);
			var duplicates = new HashSet<string>(StringComparer.Ordinal);
			foreach (var cue in _project.Cues) {
				if (cue == null || string.IsNullOrWhiteSpace(cue.EffectiveId)) {
					continue;
				}
				if (!seen.Add(cue.EffectiveId)) {
					duplicates.Add(cue.EffectiveId);
					continue;
				}
				if (cue.Validate().IsValid) {
					_cues.Add(cue.EffectiveId, cue);
				}
			}
			foreach (var duplicate in duplicates) {
				_cues.Remove(duplicate);
			}
		}

		private bool TryGetCue(string cueId, out DmdCueAsset cue)
		{
			cue = null;
			if (string.IsNullOrEmpty(cueId) || !_cues.TryGetValue(cueId, out cue)) {
				ReportValidation($"Cue '{cueId ?? "<null>"}' is unknown or invalid.");
				return false;
			}
			return true;
		}

		private void PublishConstructorDiagnostics()
		{
			foreach (var diagnostic in _constructorDiagnostics) {
				ReportValidation(diagnostic);
			}
			_constructorDiagnostics.Clear();
		}

		private void PublishVisibleDiagnostics()
		{
			PublishDiagnostics(_scheduler.Active);
			PublishDiagnostics(_scheduler.Base);
			if (_transition.Outgoing != null) {
				PublishDiagnostics(_transition.Outgoing);
			}
		}

		private void PublishDiagnostics(CueInstance instance)
		{
			if (instance == null) {
				return;
			}
			while (instance.PublishedDiagnostics < instance.Diagnostics.Count) {
				var diagnostic = instance.Diagnostics.Diagnostics[instance.PublishedDiagnostics++];
				var message = $"Cue '{instance.Cue.EffectiveId}': {diagnostic.Message}";
				Debug.LogWarning($"DMD Studio: {message}");
				OnValidationError?.Invoke(this, message);
			}
		}

		private void ReportValidation(string message)
		{
			if (!_publishedDiagnostics.Add(message)) {
				return;
			}
			Debug.LogWarning($"DMD Studio: {message}");
			OnValidationError?.Invoke(this, message);
		}

		private void Finish(CueInstance instance)
		{
			_finishedEvents.Enqueue(instance.Handle);
		}

		private void DrainFinishedEvents()
		{
			while (_finishedEvents.Count > 0) {
				var handle = _finishedEvents.Dequeue();
				OnCueFinished?.Invoke(this, handle);
			}
		}

		private void EnsureUsable()
		{
			EnsureThread();
			if (_disposed) {
				throw new ObjectDisposedException(nameof(DmdCuePlayer));
			}
		}

		private void EnsureThread()
		{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
			if (Thread.CurrentThread.ManagedThreadId != _threadId) {
				throw new InvalidOperationException("DmdCuePlayer is main-thread-only.");
			}
#endif
		}

		private struct Transition
		{
			public readonly DmdTransitionSpec Spec;
			public readonly CueInstance Outgoing;
			public readonly bool OutgoingLive;
			public int ElapsedFrames;
			public bool Active => Spec.DurationFrames > 0;

			public Transition(DmdTransitionSpec spec, CueInstance outgoing, bool outgoingLive)
			{
				Spec = spec;
				Outgoing = outgoing;
				OutgoingLive = outgoingLive;
				ElapsedFrames = 0;
			}
		}
	}
}
