// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	internal enum CueAdmissionKind
	{
		Activated,
		Queued,
		Preempted,
		Replaced,
		Coalesced,
	}

	internal readonly struct CueAdmission
	{
		public readonly CueAdmissionKind Kind;
		public readonly CueInstance Instance;
		public readonly CueInstance Displaced;

		public CueAdmission(CueAdmissionKind kind, CueInstance instance, CueInstance displaced = null)
		{
			Kind = kind;
			Instance = instance;
			Displaced = displaced;
		}
	}

	internal sealed class CueScheduler
	{
		private readonly List<CueInstance> _holdStack = new List<CueInstance>();
		private readonly List<CueInstance> _queue = new List<CueInstance>();
		private uint _nextId = 1;
		private uint _generation = 1;
		private long _sequence;

		public CueInstance Base { get; private set; }
		public CueInstance Active { get; private set; }
		internal IReadOnlyList<CueInstance> HoldStack => _holdStack;
		internal IReadOnlyList<CueInstance> Queue => _queue;

		public CueInstance Create(DmdCueAsset cue, DmdParams parameters)
		{
			if (_nextId == 0) {
				_nextId = 1;
				unchecked { _generation++; }
				if (_generation == 0) {
					_generation = 1;
				}
			}
			return new CueInstance(new CueHandle(_nextId++, _generation), cue, parameters, _sequence++);
		}

		public void SetBase(CueInstance instance)
		{
			Base = instance;
		}

		public CueAdmission Admit(CueInstance incoming)
		{
			if (incoming == null) {
				throw new ArgumentNullException(nameof(incoming));
			}
			if (!string.IsNullOrEmpty(incoming.Cue.CoalesceKey)) {
				var existing = Resolve(incoming.Cue.CoalesceKey, true);
				if (existing != null) {
					existing.Params.MergeFrom(incoming.Params);
					return new CueAdmission(CueAdmissionKind.Coalesced, existing);
				}
			}
			if (Active == null) {
				Active = incoming;
				return new CueAdmission(CueAdmissionKind.Activated, incoming);
			}

			var active = Active;
			if (incoming.Cue.Interrupt == CueInterruptPolicy.Queue) {
				Enqueue(incoming);
				return new CueAdmission(CueAdmissionKind.Queued, incoming);
			}
			if (incoming.Cue.Priority > active.Cue.Priority) {
				if (active.Cue.Interrupt == CueInterruptPolicy.NonInterruptible &&
				    incoming.Cue.Priority != CuePriority.System) {
					Enqueue(incoming);
					return new CueAdmission(CueAdmissionKind.Queued, incoming);
				}
				_holdStack.Add(active);
				Active = incoming;
				return new CueAdmission(CueAdmissionKind.Preempted, incoming, active);
			}
			if (incoming.Cue.Priority == active.Cue.Priority &&
			    incoming.Cue.Interrupt == CueInterruptPolicy.Replace &&
			    active.Cue.Interrupt != CueInterruptPolicy.NonInterruptible) {
				Active = incoming;
				return new CueAdmission(CueAdmissionKind.Replaced, incoming, active);
			}

			Enqueue(incoming);
			return new CueAdmission(CueAdmissionKind.Queued, incoming);
		}

		public CueInstance EndActive()
		{
			Active = SelectNext();
			return Active;
		}

		public bool TryFind(CueHandle handle, out CueInstance instance)
		{
			instance = null;
			if (!handle.IsValid) {
				return false;
			}
			if (Active?.Handle == handle) {
				instance = Active;
				return true;
			}
			for (var index = _holdStack.Count - 1; index >= 0; index--) {
				if (_holdStack[index].Handle == handle) {
					instance = _holdStack[index];
					return true;
				}
			}
			for (var index = 0; index < _queue.Count; index++) {
				if (_queue[index].Handle == handle) {
					instance = _queue[index];
					return true;
				}
			}
			return false;
		}

		public CueInstance Resolve(string cueIdOrKey)
		{
			return Resolve(cueIdOrKey, true) ?? Resolve(cueIdOrKey, false);
		}

		public bool RemoveWaiting(CueInstance instance)
		{
			for (var index = _holdStack.Count - 1; index >= 0; index--) {
				if (ReferenceEquals(_holdStack[index], instance)) {
					_holdStack.RemoveAt(index);
					return true;
				}
			}
			return _queue.Remove(instance);
		}

		public void ClearWaiting(Action<CueInstance> removed)
		{
			for (var index = _holdStack.Count - 1; index >= 0; index--) {
				removed?.Invoke(_holdStack[index]);
			}
			_holdStack.Clear();
			for (var index = 0; index < _queue.Count; index++) {
				removed?.Invoke(_queue[index]);
			}
			_queue.Clear();
		}

		private CueInstance Resolve(string value, bool coalesceKey)
		{
			if (string.IsNullOrEmpty(value)) {
				return null;
			}
			if (Matches(Active, value, coalesceKey)) {
				return Active;
			}
			for (var index = _holdStack.Count - 1; index >= 0; index--) {
				if (Matches(_holdStack[index], value, coalesceKey)) {
					return _holdStack[index];
				}
			}
			for (var index = 0; index < _queue.Count; index++) {
				if (Matches(_queue[index], value, coalesceKey)) {
					return _queue[index];
				}
			}
			return null;
		}

		private static bool Matches(CueInstance instance, string value, bool coalesceKey)
		{
			return instance != null && string.Equals(coalesceKey ? instance.Cue.CoalesceKey : instance.Cue.EffectiveId,
				value, StringComparison.Ordinal);
		}

		private void Enqueue(CueInstance incoming)
		{
			var index = 0;
			while (index < _queue.Count && (_queue[index].Cue.Priority > incoming.Cue.Priority ||
			       _queue[index].Cue.Priority == incoming.Cue.Priority &&
			       _queue[index].Sequence < incoming.Sequence)) {
				index++;
			}
			_queue.Insert(index, incoming);
		}

		private CueInstance SelectNext()
		{
			CueInstance held = null;
			while (_holdStack.Count > 0) {
				held = _holdStack[_holdStack.Count - 1];
				_holdStack.RemoveAt(_holdStack.Count - 1);
				if (held.Cue.Return != CueReturnPolicy.Discard) {
					break;
				}
				held = null;
			}
			var queued = _queue.Count > 0 ? _queue[0] : null;
			if (held != null && (queued == null || held.Cue.Priority >= queued.Cue.Priority)) {
				if (held.Cue.Return == CueReturnPolicy.Restart) {
					held.Restart();
				}
				return held;
			}
			if (held != null) {
				_holdStack.Add(held);
			}
			if (queued != null) {
				_queue.RemoveAt(0);
			}
			return queued;
		}
	}
}
