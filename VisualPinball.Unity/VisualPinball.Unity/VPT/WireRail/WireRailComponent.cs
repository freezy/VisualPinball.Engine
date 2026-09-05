// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;
using UnityEngine.Splines;
using VisualPinball.Engine.VPT;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VisualPinball.Unity
{
	public enum WireRailThirdRailSide
	{
		Left,
		Right,
	}

	public enum WireRailStandSide
	{
		Left,
		Right,
	}

	public enum WireRailEndpoint
	{
		Start,
		End,
	}

	/// <summary>
	/// Creates useful starting positions for wire-rail centerlines. All values are in VPX units
	/// and describe the X/Z cross-section around a route whose initial direction is +Y.
	/// </summary>
	public static class WireRailLayout
	{
		public const float ReferenceBallDiameter = 50f;
		public const float ReferenceWireDiameter = 8f;
		public const float DefaultWireDiameter = 6.5f;
		public const float BottomRailSpacing = 30f;
		public const float MiddleRailSpacing = 60f;
		public const float MiddleRailHeight = 30f;
		public const float TopRailSpacing = 30f;
		public const float TopRailHeight = 60f;

		public static Vector2[] CreateDefaultOffsets(int railCount,
			WireRailThirdRailSide thirdRailSide = WireRailThirdRailSide.Right)
		{
			if (railCount < 1) {
				throw new ArgumentOutOfRangeException(nameof(railCount), railCount,
					"A wire-rail segment needs at least one rail.");
			}

			if (railCount == 1) {
				return new[] { Vector2.zero };
			}

			var bottomHalfSpacing = BottomRailSpacing * 0.5f;
			var middleHalfSpacing = MiddleRailSpacing * 0.5f;
			var topHalfSpacing = TopRailSpacing * 0.5f;
			var offsets = new List<Vector2>(railCount) {
				new(-bottomHalfSpacing, 0f),
				new(bottomHalfSpacing, 0f),
			};

			if (railCount == 3) {
				offsets.Add(new Vector2(
					thirdRailSide == WireRailThirdRailSide.Left
						? -middleHalfSpacing : middleHalfSpacing,
					MiddleRailHeight));
				return offsets.ToArray();
			}

			if (railCount >= 4) {
				offsets.Add(new Vector2(-middleHalfSpacing, MiddleRailHeight));
				offsets.Add(new Vector2(middleHalfSpacing, MiddleRailHeight));
			}

			var topRailCount = railCount - 4;
			for (var i = 0; i < topRailCount; i++) {
				var x = topRailCount == 1
					? 0f
					: math.lerp(-topHalfSpacing, topHalfSpacing,
						i / (float)(topRailCount - 1));
				offsets.Add(new Vector2(x, TopRailHeight));
			}
			return offsets.ToArray();
		}
	}

	[Serializable]
	public sealed class WireRailTransition
	{
		[SerializeField] private bool _overridden;
		[SerializeField] private bool _continuous = true;
		[SerializeField] private AnimationCurve _curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		public bool Overridden => _overridden;
		public bool Continuous => _continuous;
		public AnimationCurve Curve => _curve;

		internal bool EnsureInitialized()
		{
			var changed = false;
			if (_curve == null || _curve.length == 0) {
				_curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
				changed = true;
			}
			if (!_overridden && (!_continuous || !IsLinear(_curve))) {
				_overridden = true;
				changed = true;
			}
			return changed;
		}

		internal void SetOverridden(bool overridden)
		{
			_overridden = overridden;
			if (overridden) {
				return;
			}
			_continuous = true;
			_curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
		}

		internal void SetContinuous(bool continuous)
		{
			_overridden = true;
			_continuous = continuous;
		}

		internal void SetCurve(AnimationCurve curve)
		{
			_overridden = true;
			_curve = CloneCurve(curve);
			EnsureInitialized();
		}

		internal float Evaluate(float curveT)
		{
			if (curveT <= 0f) {
				return 0f;
			}
			if (curveT >= 1f) {
				return 1f;
			}
			return _curve == null || _curve.length == 0
				? curveT
				: math.saturate(_curve.Evaluate(curveT));
		}

		internal WireRailTransition Clone()
			=> new() {
				_overridden = _overridden,
				_continuous = _continuous,
				_curve = CloneCurve(_curve),
			};

		internal static WireRailTransition FromLegacy(bool continuous)
			=> new() {
				_overridden = !continuous,
				_continuous = continuous,
			};

		private static bool IsLinear(AnimationCurve curve)
		{
			const int sampleCount = 16;
			for (var sampleIndex = 0; sampleIndex <= sampleCount; sampleIndex++) {
				var t = sampleIndex / (float)sampleCount;
				if (!Mathf.Approximately(curve.Evaluate(t), t)) {
					return false;
				}
			}
			return true;
		}

		private static AnimationCurve CloneCurve(AnimationCurve source)
		{
			if (source == null || source.length == 0) {
				return AnimationCurve.Linear(0f, 0f, 1f, 1f);
			}
			return new AnimationCurve(source.keys) {
				preWrapMode = source.preWrapMode,
				postWrapMode = source.postWrapMode,
			};
		}
	}

	[Serializable]
	public sealed class WireRailConnection
	{
		[SerializeField] private List<WireRailTransition> _wires = new();
		[SerializeField, HideInInspector, FormerlySerializedAs("_continuousWires")]
		private List<bool> _legacyContinuousWires;

		public int WireCount => _wires?.Count ?? 0;

		public bool IsWireOverridden(int wireIndex)
			=> GetWire(wireIndex).Overridden;

		public bool IsWireContinuous(int wireIndex)
			=> GetWire(wireIndex).Continuous;

		public AnimationCurve GetWireCurve(int wireIndex)
			=> GetWire(wireIndex).Curve;

		internal bool EnsureInitialized(int wireCount)
		{
			if (wireCount < 0) {
				throw new ArgumentOutOfRangeException(nameof(wireCount));
			}

			var changed = false;
			_wires ??= new List<WireRailTransition>();
			if (_wires.Count == 0 && _legacyContinuousWires is { Count: > 0 }) {
				foreach (var continuous in _legacyContinuousWires) {
					_wires.Add(WireRailTransition.FromLegacy(continuous));
				}
				_legacyContinuousWires = null;
				changed = true;
			}
			while (_wires.Count < wireCount) {
				_wires.Add(new WireRailTransition());
				changed = true;
			}
			if (_wires.Count > wireCount) {
				_wires.RemoveRange(wireCount, _wires.Count - wireCount);
				changed = true;
			}
			for (var wireIndex = 0; wireIndex < _wires.Count; wireIndex++) {
				if (_wires[wireIndex] == null) {
					_wires[wireIndex] = new WireRailTransition();
					changed = true;
				}
				changed |= _wires[wireIndex].EnsureInitialized();
			}
			return changed;
		}

		internal void SetWireOverridden(int wireIndex, bool overridden)
			=> GetWire(wireIndex).SetOverridden(overridden);

		internal void SetWireContinuous(int wireIndex, bool continuous)
			=> GetWire(wireIndex).SetContinuous(continuous);

		internal void SetWireCurve(int wireIndex, AnimationCurve curve)
			=> GetWire(wireIndex).SetCurve(curve);

		internal float EvaluateWireTransition(int wireIndex, float curveT)
			=> GetWire(wireIndex).Evaluate(curveT);

		internal WireRailConnection Clone()
		{
			var clone = new WireRailConnection {
				_wires = new List<WireRailTransition>(),
			};
			if (_wires != null) {
				foreach (var wire in _wires) {
					clone._wires.Add(wire?.Clone() ?? new WireRailTransition());
				}
			}
			return clone;
		}

		private WireRailTransition GetWire(int wireIndex)
		{
			if (wireIndex < 0 || wireIndex >= WireCount) {
				throw new ArgumentOutOfRangeException(nameof(wireIndex));
			}
			return _wires[wireIndex];
		}
	}

	[Serializable]
	public sealed class WireRailSegment
	{
		[SerializeField, Min(0f)] private float _distance;
		[SerializeField] private WireRailThirdRailSide _thirdRailSide = WireRailThirdRailSide.Right;
		[SerializeField] private List<Vector2> _railOffsets = new(
			WireRailLayout.CreateDefaultOffsets(4));
		[SerializeField] private List<bool> _activeRails = new();
		[SerializeField] private List<float> _wireDiameters = new();
		[SerializeField] private WireRailConnection _connectionToNext = new();

		public float Distance => _distance;
		public WireRailThirdRailSide ThirdRailSide => _thirdRailSide;
		public int RailCount => _railOffsets?.Count ?? 0;
		public IReadOnlyList<Vector2> RailOffsets => _railOffsets;
		public IReadOnlyList<float> WireDiameters => _wireDiameters;
		public WireRailConnection ConnectionToNext => _connectionToNext;

		internal void SetDistance(float distance, float splineLength)
		{
			_distance = math.clamp(distance, 0f, math.max(0f, splineLength));
		}

		public Vector2 GetRailOffset(int railIndex)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			return _railOffsets[railIndex];
		}

		public float GetWireDiameter(int railIndex)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			return _wireDiameters[railIndex];
		}

		public bool IsRailActive(int railIndex)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			return _activeRails[railIndex];
		}

		internal bool ResizeRailCount(int railCount, float defaultWireDiameter,
			bool activateAddedRails, bool updateRecommendedLayout)
		{
			EnsureInitialized(defaultWireDiameter);
			if (railCount < 1) {
				throw new ArgumentOutOfRangeException(nameof(railCount));
			}
			if (railCount == RailCount) {
				return false;
			}

			var previousDefaults = WireRailLayout.CreateDefaultOffsets(RailCount,
				_thirdRailSide);
			var usesRecommendedLayout = updateRecommendedLayout
				&& _railOffsets.SequenceEqual(previousDefaults);
			var nextDefaults = WireRailLayout.CreateDefaultOffsets(railCount, _thirdRailSide);
			var retainedCount = math.min(RailCount, railCount);
			if (usesRecommendedLayout) {
				for (var railIndex = 0; railIndex < retainedCount; railIndex++) {
					_railOffsets[railIndex] = nextDefaults[railIndex];
				}
			}
			while (_railOffsets.Count < railCount) {
				var railIndex = _railOffsets.Count;
				_railOffsets.Add(nextDefaults[railIndex]);
				_wireDiameters.Add(math.max(0.1f, defaultWireDiameter));
				_activeRails.Add(activateAddedRails);
			}
			if (_railOffsets.Count > railCount) {
				_railOffsets.RemoveRange(railCount, _railOffsets.Count - railCount);
				_wireDiameters.RemoveRange(railCount, _wireDiameters.Count - railCount);
				_activeRails.RemoveRange(railCount, _activeRails.Count - railCount);
			}
			return true;
		}

		internal void SetThirdRailSide(WireRailThirdRailSide side)
		{
			if (_thirdRailSide == side) {
				return;
			}
			_thirdRailSide = side;
			if (RailCount == 3) {
				ResetLayout();
			}
		}

		internal void SetRailOffset(int railIndex, Vector2 offset)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			_railOffsets[railIndex] = offset;
		}

		internal void SetRailActive(int railIndex, bool active)
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			_activeRails[railIndex] = active;
		}

		internal bool SetAllWireDiameters(float diameter)
		{
			diameter = math.max(0.1f, diameter);
			var changed = false;
			for (var railIndex = 0; railIndex < _wireDiameters.Count; railIndex++) {
				if (Mathf.Approximately(_wireDiameters[railIndex], diameter)) {
					continue;
				}
				_wireDiameters[railIndex] = diameter;
				changed = true;
			}
			return changed;
		}

		internal void ResetLayout()
		{
			EnsureInitialized(WireRailLayout.ReferenceWireDiameter);
			_railOffsets = new List<Vector2>(
				WireRailLayout.CreateDefaultOffsets(_railOffsets.Count, _thirdRailSide));
		}

		internal bool EnsureInitialized(float defaultWireDiameter)
		{
			var changed = false;
			if (_connectionToNext == null) {
				_connectionToNext = new WireRailConnection();
				changed = true;
			}
			if (_railOffsets == null || _railOffsets.Count == 0) {
				_railOffsets = new List<Vector2>(
					WireRailLayout.CreateDefaultOffsets(4, _thirdRailSide));
				changed = true;
			}
			_wireDiameters ??= new List<float>();
			_activeRails ??= new List<bool>();
			while (_wireDiameters.Count < _railOffsets.Count) {
				_wireDiameters.Add(math.max(0.1f, defaultWireDiameter));
				changed = true;
			}
			if (_wireDiameters.Count > _railOffsets.Count) {
				_wireDiameters.RemoveRange(_railOffsets.Count,
					_wireDiameters.Count - _railOffsets.Count);
				changed = true;
			}
			while (_activeRails.Count < _railOffsets.Count) {
				_activeRails.Add(true);
				changed = true;
			}
			if (_activeRails.Count > _railOffsets.Count) {
				_activeRails.RemoveRange(_railOffsets.Count,
					_activeRails.Count - _railOffsets.Count);
				changed = true;
			}
			for (var i = 0; i < _wireDiameters.Count; i++) {
				var clamped = math.max(0.1f, _wireDiameters[i]);
				if (!Mathf.Approximately(clamped, _wireDiameters[i])) {
					_wireDiameters[i] = clamped;
					changed = true;
				}
			}
			return changed;
		}

		internal WireRailSegment Clone(float defaultWireDiameter)
		{
			EnsureInitialized(defaultWireDiameter);
			return new WireRailSegment {
				_distance = _distance,
				_thirdRailSide = _thirdRailSide,
				_railOffsets = new List<Vector2>(_railOffsets),
				_activeRails = new List<bool>(_activeRails),
				_wireDiameters = new List<float>(_wireDiameters),
				_connectionToNext = _connectionToNext.Clone(),
			};
		}

		internal bool EnsureConnectionInitialized(int wireCount)
		{
			var changed = false;
			if (_connectionToNext == null) {
				_connectionToNext = new WireRailConnection();
				changed = true;
			}
			return _connectionToNext.EnsureInitialized(wireCount) || changed;
		}

		internal void ResetConnection()
		{
			_connectionToNext = new WireRailConnection();
		}

		internal void CopyConnectionFrom(WireRailSegment source)
		{
			_connectionToNext = source?._connectionToNext?.Clone()
				?? new WireRailConnection();
		}
	}

	[Serializable]
	public abstract class WireRailFixture
	{
		public const float DefaultSolderThreshold = WireRailLayout.ReferenceWireDiameter * 0.25f;
		public const float DefaultSolderSize = 1f;

		[SerializeField, Min(0f)] private float _distance;
		[SerializeField, Min(0f)] private float _solderThreshold = DefaultSolderThreshold;
		[SerializeField, Min(0.01f)] private float _solderSize = DefaultSolderSize;
		// Serialize-reference fixtures run field initializers on load, so pre-existing
		// fixtures deserialize as enabled. Disabling only hides the render mesh; colliders
		// are unaffected.
		[SerializeField] private bool _enabled = true;

		public float Distance => _distance;
		public float SolderThreshold => _solderThreshold;
		public float SolderSize => _solderSize;
		public bool Enabled => _enabled;

		internal void SetEnabled(bool enabled)
		{
			_enabled = enabled;
		}

		internal bool EnsureInitialized(float splineLength)
		{
			var changed = false;
			var clampedDistance = math.clamp(_distance, 0f, math.max(0f, splineLength));
			if (!Mathf.Approximately(_distance, clampedDistance)) {
				_distance = clampedDistance;
				changed = true;
			}
			var solderThreshold = math.max(0f, _solderThreshold);
			if (!Mathf.Approximately(_solderThreshold, solderThreshold)) {
				_solderThreshold = solderThreshold;
				changed = true;
			}
			var solderSize = _solderSize <= 0f
				? DefaultSolderSize : math.max(0.01f, _solderSize);
			if (!Mathf.Approximately(_solderSize, solderSize)) {
				_solderSize = solderSize;
				changed = true;
			}
			return changed;
		}

		internal void SetDistance(float distance, float splineLength)
		{
			_distance = math.clamp(distance, 0f, math.max(0f, splineLength));
		}

		internal void SetSolderThreshold(float solderThreshold)
		{
			_solderThreshold = math.max(0f, solderThreshold);
		}

		internal void SetSolderSize(float solderSize)
		{
			_solderSize = math.max(0.01f, solderSize);
		}
	}

	[Serializable]
	[MovedFrom(true, sourceClassName: "WireRailBraceFixture")]
	public sealed class WireRailRingFixture : WireRailFixture
	{
		public const int DefaultRingDensity = 32;
		public const float DefaultCutoutStartAngle = 60f;
		public const float DefaultCutoutEndAngle = 120f;
		public const float DefaultStraightStartAngle = 210f;
		public const float DefaultStraightEndAngle = 330f;

		[SerializeField, Min(0.1f)] private float _diameter = WireRailLayout.ReferenceWireDiameter;
		[SerializeField] private bool _hasCutout;
		[SerializeField, Range(0f, 360f)] private float _cutoutStartAngle = DefaultCutoutStartAngle;
		[SerializeField, Range(0f, 360f)] private float _cutoutEndAngle = DefaultCutoutEndAngle;
		[SerializeField] private bool _hasStraightSection;
		[SerializeField, Range(0f, 360f)] private float _straightStartAngle = DefaultStraightStartAngle;
		[SerializeField, Range(0f, 360f)] private float _straightEndAngle = DefaultStraightEndAngle;
		[SerializeField] private float _lateralOffset;
		[SerializeField] private float _verticalOffset;
		[SerializeField, Min(0.1f)] private float _scale = 1f;
		[SerializeField, Range(3, 128)] private int _ringDensity = DefaultRingDensity;
		[SerializeField, HideInInspector] private float _radiusOffset;
		[SerializeField, HideInInspector] private bool _scaleInitialized;

		public float Diameter => _diameter;
		public bool HasCutout => _hasCutout;
		public float CutoutStartAngle => _cutoutStartAngle;
		public float CutoutEndAngle => _cutoutEndAngle;
		public bool HasStraightSection => _hasStraightSection;
		public float StraightStartAngle => _straightStartAngle;
		public float StraightEndAngle => _straightEndAngle;
		public float LateralOffset => _lateralOffset;
		public float VerticalOffset => _verticalOffset;
		public float Scale => _scale;
		public int RingDensity => _ringDensity;
		internal bool ScaleInitialized => _scaleInitialized;

		public static Vector2 AlignAngleRangeHorizontally(float startAngle, float endAngle)
		{
			startAngle = math.clamp(startAngle, 0f, 360f);
			endAngle = math.clamp(endAngle, 0f, 360f);
			var rawSweep = endAngle - startAngle;
			if (math.abs(rawSweep) >= 359.999f) {
				return new Vector2(startAngle, endAngle);
			}
			var sweep = math.fmod(rawSweep + 360f, 360f);
			if (sweep <= 0.001f) {
				return new Vector2(startAngle, endAngle);
			}
			var halfSweep = sweep * 0.5f;
			var center = Mathf.Repeat(startAngle + halfSweep, 360f);
			var alignedCenter = math.abs(Mathf.DeltaAngle(center, 90f))
				<= math.abs(Mathf.DeltaAngle(center, 270f)) ? 90f : 270f;
			var rawStart = alignedCenter - halfSweep;
			var rawEnd = alignedCenter + halfSweep;
			var alignedStart = Mathf.Repeat(rawStart, 360f);
			var alignedEnd = Mathf.Repeat(rawEnd, 360f);
			if (Mathf.Approximately(alignedEnd, 0f) && rawEnd > 0f) {
				alignedEnd = 360f;
			}
			return new Vector2(alignedStart, alignedEnd);
		}

		internal bool EnsureRingInitialized(float splineLength)
		{
			var changed = EnsureInitialized(splineLength);
			var diameter = math.max(0.1f, _diameter);
			var startAngle = math.clamp(_cutoutStartAngle, 0f, 360f);
			var endAngle = math.clamp(_cutoutEndAngle, 0f, 360f);
			var straightStartAngle = math.clamp(_straightStartAngle, 0f, 360f);
			var straightEndAngle = math.clamp(_straightEndAngle, 0f, 360f);
			var scale = math.max(0.1f, _scale);
			var ringDensity = math.clamp(_ringDensity <= 0 ? DefaultRingDensity : _ringDensity,
				3, 128);
			if (!Mathf.Approximately(_diameter, diameter)) {
				_diameter = diameter;
				changed = true;
			}
			if (!Mathf.Approximately(_cutoutStartAngle, startAngle)) {
				_cutoutStartAngle = startAngle;
				changed = true;
			}
			if (!Mathf.Approximately(_cutoutEndAngle, endAngle)) {
				_cutoutEndAngle = endAngle;
				changed = true;
			}
			if (!Mathf.Approximately(_straightStartAngle, straightStartAngle)) {
				_straightStartAngle = straightStartAngle;
				changed = true;
			}
			if (!Mathf.Approximately(_straightEndAngle, straightEndAngle)) {
				_straightEndAngle = straightEndAngle;
				changed = true;
			}
			if (!Mathf.Approximately(_scale, scale)) {
				_scale = scale;
				changed = true;
			}
			if (_ringDensity != ringDensity) {
				_ringDensity = ringDensity;
				changed = true;
			}
			return changed;
		}

		internal bool EnsureScaleInitialized(float baseRadius)
		{
			if (_scaleInitialized) {
				return false;
			}
			var migratedRadius = math.max(_diameter * 0.5f, baseRadius + _radiusOffset);
			_scale = baseRadius > 1e-5f
				? math.max(0.1f, migratedRadius / baseRadius) : 1f;
			_radiusOffset = 0f;
			_scaleInitialized = true;
			return true;
		}

		internal bool SetDiameter(float diameter)
		{
			diameter = math.max(0.1f, diameter);
			if (Mathf.Approximately(_diameter, diameter)) {
				return false;
			}
			_diameter = diameter;
			return true;
		}

		internal void SetProperties(float distance, float splineLength, float diameter,
			bool hasCutout, float cutoutStartAngle, float cutoutEndAngle,
			bool hasStraightSection, float straightStartAngle, float straightEndAngle,
			float lateralOffset, float verticalOffset, float scale,
			int ringDensity = DefaultRingDensity)
		{
			SetDistance(distance, splineLength);
			_diameter = math.max(0.1f, diameter);
			_hasCutout = hasCutout;
			_cutoutStartAngle = math.clamp(cutoutStartAngle, 0f, 360f);
			_cutoutEndAngle = math.clamp(cutoutEndAngle, 0f, 360f);
			_hasStraightSection = hasStraightSection;
			_straightStartAngle = math.clamp(straightStartAngle, 0f, 360f);
			_straightEndAngle = math.clamp(straightEndAngle, 0f, 360f);
			_lateralOffset = lateralOffset;
			_verticalOffset = verticalOffset;
			_scale = math.max(0.1f, scale);
			_ringDensity = math.clamp(ringDensity, 3, 128);
			_radiusOffset = 0f;
			_scaleInitialized = true;
		}

		public bool TryGetStraightSection(out float startAngle, out float sweepAngle)
		{
			startAngle = math.radians(_straightStartAngle);
			var rawSweep = _straightEndAngle - _straightStartAngle;
			var sweepDegrees = math.abs(rawSweep) >= 359.999f
				? 360f
				: math.fmod(rawSweep + 360f, 360f);
			sweepAngle = math.radians(sweepDegrees);
			return _hasStraightSection && sweepDegrees > 0.001f && sweepDegrees < 359.999f;
		}

		public float2 EvaluateCenterlineOffset(float angle, float radius)
		{
			var circular = new float2(math.cos(angle), math.sin(angle)) * radius;
			if (!TryGetStraightSection(out var startAngle, out var sweepAngle)) {
				return circular;
			}
			var progress = math.fmod(angle - startAngle + math.PI * 4f, math.PI * 2f);
			if (progress > sweepAngle) {
				return circular;
			}
			var start = new float2(math.cos(startAngle), math.sin(startAngle)) * radius;
			var endAngle = startAngle + sweepAngle;
			var end = new float2(math.cos(endAngle), math.sin(endAngle)) * radius;
			return math.lerp(start, end, progress / sweepAngle);
		}

		public float2 EvaluateCenterlineTangent(float angle)
		{
			var circular = new float2(-math.sin(angle), math.cos(angle));
			if (!TryGetStraightSection(out var startAngle, out var sweepAngle)) {
				return circular;
			}
			var progress = math.fmod(angle - startAngle + math.PI * 4f, math.PI * 2f);
			if (progress > sweepAngle) {
				return circular;
			}
			var start = new float2(math.cos(startAngle), math.sin(startAngle));
			var endAngle = startAngle + sweepAngle;
			var end = new float2(math.cos(endAngle), math.sin(endAngle));
			return math.normalizesafe(end - start, circular);
		}

		public bool TryGetVisibleArc(out float startAngle, out float sweepAngle,
			out bool closed)
		{
			startAngle = 0f;
			sweepAngle = math.PI * 2f;
			closed = true;
			if (!_hasCutout) {
				return true;
			}

			var rawCutoutSweep = _cutoutEndAngle - _cutoutStartAngle;
			var cutoutSweep = math.abs(rawCutoutSweep) >= 359.999f
				? 360f
				: math.fmod(rawCutoutSweep + 360f, 360f);
			if (cutoutSweep <= 0.001f) {
				return true;
			}

			var visibleSweep = 360f - cutoutSweep;
			if (visibleSweep <= 0.001f) {
				return false;
			}
			startAngle = math.radians(_cutoutEndAngle);
			sweepAngle = math.radians(visibleSweep);
			closed = false;
			return true;
		}
	}

	/// <summary>
	/// A bottom rung with independently optional rounded arms. The historic type name is
	/// retained so existing managed-reference fixture data remains loadable.
	/// </summary>
	[Serializable]
	[MovedFrom(true, sourceClassName: "WireRailVBraceFixture")]
	public sealed class WireRailCradleFixture : WireRailFixture
	{
		internal const float MaximumCornerSpanFraction = 0.45f;

		public const int DefaultRingDensity = 32;
		public const float DefaultBottomLength = 8f;
		public const float DefaultLeftLength = 85f;
		public const float DefaultRightLength = 85f;
		public const float DefaultAngle = 53.130102f;
		public const float DefaultRotation = 0f;
		public const float DefaultCornerRadius = WireRailLayout.ReferenceWireDiameter;

		[SerializeField, Min(0.1f)] private float _diameter = WireRailLayout.ReferenceWireDiameter;
		[SerializeField, Range(3, 128)] private int _ringDensity = DefaultRingDensity;
		[SerializeField] private float _lateralOffset;
		[SerializeField] private float _verticalOffset;
		[SerializeField, Min(0.1f), FormerlySerializedAs("_straightHeight")]
		private float _bottomLength = DefaultBottomLength;
		[SerializeField, Min(0f)] private float _leftLength = DefaultLeftLength;
		[SerializeField, Min(0f)] private float _rightLength = DefaultRightLength;
		[SerializeField, Range(1f, 179f)] private float _angle = DefaultAngle;
		[SerializeField, Range(0f, 360f)] private float _rotation = DefaultRotation;
		[SerializeField, Min(0.1f)] private float _cornerRadius = DefaultCornerRadius;

		public float Diameter => _diameter;
		public int RingDensity => _ringDensity;
		public float LateralOffset => _lateralOffset;
		public float VerticalOffset => _verticalOffset;
		public float BottomLength => _bottomLength;
		public float LeftLength => _leftLength;
		public float RightLength => _rightLength;
		public float Angle => _angle;
		public float Rotation => _rotation;
		public float CornerRadius => _cornerRadius;

		internal bool EnsureCradleInitialized(float splineLength)
		{
			var changed = EnsureInitialized(splineLength);
			var diameter = math.max(0.1f, _diameter);
			var ringDensity = math.clamp(_ringDensity <= 0 ? DefaultRingDensity : _ringDensity,
				3, 128);
			var leftLength = math.max(0f, _leftLength);
			var rightLength = math.max(0f, _rightLength);
			var angle = math.clamp(_angle, 1f, 179f);
			var minimumRoundedSpan = GetMinimumRoundedSpan(diameter, angle);
			leftLength = ClampOptionalArmLength(leftLength, minimumRoundedSpan);
			rightLength = ClampOptionalArmLength(rightLength, minimumRoundedSpan);
			var bottomLength = math.max(0.1f, _bottomLength);
			if (leftLength > 0f || rightLength > 0f) {
				bottomLength = math.max(bottomLength, minimumRoundedSpan);
			}
			var rotation = math.clamp(_rotation, 0f, 360f);
			var cornerRadius = math.max(diameter * 0.5f, _cornerRadius);
			changed |= SetValue(ref _diameter, diameter);
			if (_ringDensity != ringDensity) {
				_ringDensity = ringDensity;
				changed = true;
			}
			changed |= SetValue(ref _bottomLength, bottomLength);
			changed |= SetValue(ref _leftLength, leftLength);
			changed |= SetValue(ref _rightLength, rightLength);
			changed |= SetValue(ref _angle, angle);
			changed |= SetValue(ref _rotation, rotation);
			changed |= SetValue(ref _cornerRadius, cornerRadius);
			return changed;

			static bool SetValue(ref float destination, float value)
			{
				if (Mathf.Approximately(destination, value)) {
					return false;
				}
				destination = value;
				return true;
			}
		}

		internal bool SetDiameter(float diameter)
		{
			diameter = math.max(0.1f, diameter);
			var changed = !Mathf.Approximately(_diameter, diameter);
			_diameter = diameter;
			var cornerRadius = math.max(_cornerRadius, diameter * 0.5f);
			if (!Mathf.Approximately(_cornerRadius, cornerRadius)) {
				_cornerRadius = cornerRadius;
				changed = true;
			}
			var minimumRoundedSpan = GetMinimumRoundedSpan(diameter, _angle);
			var leftLength = ClampOptionalArmLength(_leftLength, minimumRoundedSpan);
			var rightLength = ClampOptionalArmLength(_rightLength, minimumRoundedSpan);
			var bottomLength = _bottomLength;
			if (leftLength > 0f || rightLength > 0f) {
				bottomLength = math.max(bottomLength, minimumRoundedSpan);
			}
			changed |= SetValue(ref _bottomLength, bottomLength);
			changed |= SetValue(ref _leftLength, leftLength);
			changed |= SetValue(ref _rightLength, rightLength);
			return changed;

			static bool SetValue(ref float destination, float value)
			{
				if (Mathf.Approximately(destination, value)) {
					return false;
				}
				destination = value;
				return true;
			}
		}

		internal void SetProperties(float distance, float splineLength, float diameter,
			int ringDensity, float lateralOffset, float verticalOffset,
			float bottomLength, float leftLength, float rightLength, float angle,
			float rotation, float cornerRadius)
		{
			SetDistance(distance, splineLength);
			_diameter = math.max(0.1f, diameter);
			_ringDensity = math.clamp(ringDensity, 3, 128);
			_lateralOffset = lateralOffset;
			_verticalOffset = verticalOffset;
			_angle = math.clamp(angle, 1f, 179f);
			var minimumRoundedSpan = GetMinimumRoundedSpan(_diameter, _angle);
			_leftLength = ClampOptionalArmLength(leftLength, minimumRoundedSpan);
			_rightLength = ClampOptionalArmLength(rightLength, minimumRoundedSpan);
			_bottomLength = math.max(0.1f, bottomLength);
			if (_leftLength > 0f || _rightLength > 0f) {
				_bottomLength = math.max(_bottomLength, minimumRoundedSpan);
			}
			_rotation = math.clamp(rotation, 0f, 360f);
			_cornerRadius = math.max(_diameter * 0.5f, cornerRadius);
		}

		private static float ClampOptionalArmLength(float length, float minimumLength)
			=> length <= 0f ? 0f : math.max(length, minimumLength);

		private static float GetMinimumRoundedSpan(float diameter, float angle)
		{
			var cornerAngle = math.radians((180f - angle) * 0.5f);
			var tangentScale = math.tan(cornerAngle * 0.5f);
			return diameter * 0.5f * tangentScale / MaximumCornerSpanFraction;
		}
	}

	[Serializable]
	[MovedFrom(true, sourceClassName: "WireRailCrossWireFixture")]
	public sealed class WireRailRungFixture : WireRailFixture
	{
		public const float DefaultAngle = 0f;

		[SerializeField, Min(0.1f)] private float _diameter = WireRailLayout.ReferenceWireDiameter;
		[SerializeField, HideInInspector, Min(0)] private int _startRailIndex;
		[SerializeField, HideInInspector, Min(0)] private int _endRailIndex = 1;
		[SerializeField, Range(0f, 360f)]
		private float _angle = DefaultAngle;
		[SerializeField] private float _lateralOffset;
		[SerializeField] private float _verticalOffset;
		[SerializeField] private float _lengthAdjustment;

		public float Diameter => _diameter;
		public int StartRailIndex => _startRailIndex;
		public int EndRailIndex => _endRailIndex;
		public float Angle => _angle;
		public float LateralOffset => _lateralOffset;
		public float VerticalOffset => _verticalOffset;
		public float LengthAdjustment => _lengthAdjustment;

		internal bool EnsureRungInitialized(float splineLength)
		{
			var changed = EnsureInitialized(splineLength);
			var diameter = math.max(0.1f, _diameter);
			var startRailIndex = math.max(0, _startRailIndex);
			var endRailIndex = math.max(0, _endRailIndex);
			var angle = math.clamp(_angle, 0f, 360f);
			if (!Mathf.Approximately(_diameter, diameter)) {
				_diameter = diameter;
				changed = true;
			}
			if (_startRailIndex != startRailIndex) {
				_startRailIndex = startRailIndex;
				changed = true;
			}
			if (_endRailIndex != endRailIndex) {
				_endRailIndex = endRailIndex;
				changed = true;
			}
			if (!Mathf.Approximately(_angle, angle)) {
				_angle = angle;
				changed = true;
			}
			return changed;
		}

		internal bool SetDiameter(float diameter)
		{
			diameter = math.max(0.1f, diameter);
			if (Mathf.Approximately(_diameter, diameter)) {
				return false;
			}
			_diameter = diameter;
			return true;
		}

		internal void SetProperties(float distance, float splineLength, float diameter,
			int startRailIndex, int endRailIndex, float angle,
			float lateralOffset, float verticalOffset, float lengthAdjustment)
		{
			SetDistance(distance, splineLength);
			_diameter = math.max(0.1f, diameter);
			_startRailIndex = math.max(0, startRailIndex);
			_endRailIndex = math.max(0, endRailIndex);
			_angle = math.clamp(angle, 0f, 360f);
			_lateralOffset = lateralOffset;
			_verticalOffset = verticalOffset;
			_lengthAdjustment = lengthAdjustment;
		}
	}

	/// <summary>
	/// An endpoint-only fitting that joins two rails with outward leads and a terminal
	/// semicircle. The terminal arc is emitted into a separate collider submesh.
	/// </summary>
	[Serializable]
	[MovedFrom(true, sourceClassName: "WireRailDropLoopFixture")]
	public sealed class WireRailHairpinFixture : WireRailFixture
	{
		public const float DefaultLoopDiameter = 60f;
		public const float DefaultLeadLength = 40f;
		public const float DefaultTangentLength = 15f;
		public const int DefaultRingDensity = 24;

		[SerializeField, Min(0.1f)] private float _diameter = WireRailLayout.ReferenceWireDiameter;
		[SerializeField] private WireRailEndpoint _endpoint = WireRailEndpoint.End;
		[SerializeField, Min(0)] private int _firstRailIndex;
		[SerializeField, Min(0)] private int _secondRailIndex = 1;
		[SerializeField, Min(0.1f)] private float _loopDiameter = DefaultLoopDiameter;
		[SerializeField, Min(0f)] private float _leadLength = DefaultLeadLength;
		[SerializeField, Min(0f)] private float _tangentLength = DefaultTangentLength;
		[SerializeField, Range(4, 128)] private int _ringDensity = DefaultRingDensity;
		[SerializeField] private float _railOffset;
		[SerializeField, Range(0f, 360f)] private float _rotation;

		public float Diameter => _diameter;
		public WireRailEndpoint Endpoint => _endpoint;
		public int FirstRailIndex => _firstRailIndex;
		public int SecondRailIndex => _secondRailIndex;
		public float LoopDiameter => _loopDiameter;
		public float LeadLength => _leadLength;
		public float TangentLength => _tangentLength;
		public int RingDensity => _ringDensity;
		public float RailOffset => _railOffset;
		public float Rotation => _rotation;

		internal bool EnsureHairpinInitialized(float splineLength)
		{
			var changed = EnsureInitialized(splineLength);
			var endpoint = _endpoint == WireRailEndpoint.Start
				? WireRailEndpoint.Start : WireRailEndpoint.End;
			if (_endpoint != endpoint) {
				_endpoint = endpoint;
				changed = true;
			}
			var endpointDistance = _endpoint == WireRailEndpoint.Start ? 0f : splineLength;
			if (!Mathf.Approximately(Distance, endpointDistance)) {
				SetDistance(endpointDistance, splineLength);
				changed = true;
			}
			changed |= SetValue(ref _diameter, math.max(0.1f, _diameter));
			var firstRailIndex = math.max(0, _firstRailIndex);
			var secondRailIndex = math.max(0, _secondRailIndex);
			if (_firstRailIndex != firstRailIndex) {
				_firstRailIndex = firstRailIndex;
				changed = true;
			}
			if (_secondRailIndex != secondRailIndex) {
				_secondRailIndex = secondRailIndex;
				changed = true;
			}
			changed |= SetValue(ref _loopDiameter, math.max(0.1f, _loopDiameter));
			changed |= SetValue(ref _leadLength, math.max(0f, _leadLength));
			changed |= SetValue(ref _tangentLength, math.max(0f, _tangentLength));
			changed |= SetValue(ref _railOffset,
				math.clamp(_railOffset, 0f, math.max(0f, splineLength)));
			var ringDensity = math.clamp(_ringDensity <= 0 ? DefaultRingDensity : _ringDensity,
				4, 128);
			if (_ringDensity != ringDensity) {
				_ringDensity = ringDensity;
				changed = true;
			}
			changed |= SetValue(ref _rotation, math.clamp(_rotation, 0f, 360f));
			return changed;

			static bool SetValue(ref float destination, float value)
			{
				if (Mathf.Approximately(destination, value)) {
					return false;
				}
				destination = value;
				return true;
			}
		}

		internal bool SetDiameter(float diameter)
		{
			diameter = math.max(0.1f, diameter);
			if (Mathf.Approximately(_diameter, diameter)) {
				return false;
			}
			_diameter = diameter;
			return true;
		}

		internal void SetProperties(float splineLength, float diameter,
			WireRailEndpoint endpoint, int firstRailIndex, int secondRailIndex,
			float loopDiameter, float leadLength, float tangentLength, int ringDensity,
			float railOffset, float rotation)
		{
			_endpoint = endpoint == WireRailEndpoint.Start
				? WireRailEndpoint.Start : WireRailEndpoint.End;
			SetDistance(endpoint == WireRailEndpoint.Start ? 0f : splineLength, splineLength);
			_diameter = math.max(0.1f, diameter);
			_firstRailIndex = math.max(0, firstRailIndex);
			_secondRailIndex = math.max(0, secondRailIndex);
			_loopDiameter = math.max(0.1f, loopDiameter);
			_leadLength = math.max(0f, leadLength);
			_tangentLength = math.max(0f, tangentLength);
			_ringDensity = math.clamp(ringDensity, 4, 128);
			_railOffset = math.clamp(railOffset, 0f, math.max(0f, splineLength));
			_rotation = math.clamp(rotation, 0f, 360f);
		}
	}

	/// <summary>
	/// An endpoint-only fitting that continues two selected rails past the spline and
	/// then bends them vertically down. The remaining rails can be shortened independently.
	/// </summary>
	[Serializable]
	[MovedFrom(true, sourceClassName: "WireRailDropFixture")]
	public sealed class WireRailElbowFixture : WireRailFixture
	{
		public const float DefaultOffset = 40f;
		public const float DefaultDropLength = 80f;

		[SerializeField, Min(0.1f)] private float _diameter = WireRailLayout.ReferenceWireDiameter;
		[SerializeField] private WireRailEndpoint _endpoint = WireRailEndpoint.End;
		[SerializeField, Min(0)] private int _firstRailIndex;
		[SerializeField, Min(0)] private int _secondRailIndex = 1;
		// Moves the elbow inward from the spline endpoint, shortening the two rails. Zero drops
		// exactly at the endpoint.
		[FormerlySerializedAs("_distanceToHole")]
		[SerializeField, Min(0f)] private float _offset = DefaultOffset;
		[SerializeField, Min(0.1f)] private float _dropLength = DefaultDropLength;
		[SerializeField] private float _zAngle;
		[SerializeField] private List<float> _railOffsets = new();
		[SerializeField, HideInInspector] private bool _railPairInitialized;

		public float Diameter => _diameter;
		public WireRailEndpoint Endpoint => _endpoint;
		public int FirstRailIndex => _firstRailIndex;
		public int SecondRailIndex => _secondRailIndex;
		public float Offset => _offset;
		public float DropLength => _dropLength;
		public float ZAngle => _zAngle;
		public int RailCount => _railOffsets?.Count ?? 0;
		public IReadOnlyList<float> RailOffsets => _railOffsets;
		internal bool RailPairInitialized => _railPairInitialized;

		public bool IsAttachedRail(int railIndex)
			=> railIndex == _firstRailIndex || railIndex == _secondRailIndex;

		public float GetRailOffset(int railIndex)
		{
			if (_railOffsets == null || railIndex < 0 || railIndex >= _railOffsets.Count) {
				throw new ArgumentOutOfRangeException(nameof(railIndex));
			}
			return _railOffsets[railIndex];
		}

		internal bool EnsureElbowInitialized(float splineLength, int railCount)
		{
			var changed = EnsureInitialized(splineLength);
			var endpoint = _endpoint == WireRailEndpoint.Start
				? WireRailEndpoint.Start : WireRailEndpoint.End;
			if (_endpoint != endpoint) {
				_endpoint = endpoint;
				changed = true;
			}
			var endpointDistance = _endpoint == WireRailEndpoint.Start ? 0f : splineLength;
			if (!Mathf.Approximately(Distance, endpointDistance)) {
				SetDistance(endpointDistance, splineLength);
				changed = true;
			}
			changed |= SetValue(ref _diameter, math.max(0.1f, _diameter));
			var maximumRailIndex = math.max(0, railCount - 1);
			var firstRailIndex = math.clamp(_firstRailIndex, 0, maximumRailIndex);
			var secondRailIndex = math.clamp(_secondRailIndex, 0, maximumRailIndex);
			if (_firstRailIndex != firstRailIndex) {
				_firstRailIndex = firstRailIndex;
				changed = true;
			}
			if (_secondRailIndex != secondRailIndex) {
				_secondRailIndex = secondRailIndex;
				changed = true;
			}
			changed |= SetValue(ref _offset,
				math.clamp(_offset, 0f, math.max(0f, splineLength)));
			changed |= SetValue(ref _dropLength, math.max(0.1f, _dropLength));
			_railOffsets ??= new List<float>();
			while (_railOffsets.Count < railCount) {
				_railOffsets.Add(0f);
				changed = true;
			}
			if (_railOffsets.Count > railCount) {
				_railOffsets.RemoveRange(railCount, _railOffsets.Count - railCount);
				changed = true;
			}
			for (var railIndex = 0; railIndex < _railOffsets.Count; railIndex++) {
				var offset = IsAttachedRail(railIndex) ? 0f : math.clamp(
					_railOffsets[railIndex], 0f, math.max(0f, splineLength));
				if (Mathf.Approximately(_railOffsets[railIndex], offset)) {
					continue;
				}
				_railOffsets[railIndex] = offset;
				changed = true;
			}
			return changed;

			static bool SetValue(ref float destination, float value)
			{
				if (Mathf.Approximately(destination, value)) {
					return false;
				}
				destination = value;
				return true;
			}
		}

		internal bool SetDiameter(float diameter)
		{
			diameter = math.max(0.1f, diameter);
			if (Mathf.Approximately(_diameter, diameter)) {
				return false;
			}
			_diameter = diameter;
			return true;
		}

		internal bool EnsureRailPairInitialized(int firstRailIndex, int secondRailIndex)
		{
			if (_railPairInitialized) {
				return false;
			}
			_firstRailIndex = math.max(0, firstRailIndex);
			_secondRailIndex = math.max(0, secondRailIndex);
			if (_railOffsets != null) {
				if (_firstRailIndex < _railOffsets.Count) {
					_railOffsets[_firstRailIndex] = 0f;
				}
				if (_secondRailIndex < _railOffsets.Count) {
					_railOffsets[_secondRailIndex] = 0f;
				}
			}
			_railPairInitialized = true;
			return true;
		}

		internal void SetProperties(float splineLength, int railCount, float diameter,
			WireRailEndpoint endpoint, int firstRailIndex, int secondRailIndex,
			float offset, float dropLength, float zAngle,
			IReadOnlyList<float> railOffsets)
		{
			if (railOffsets == null) {
				throw new ArgumentNullException(nameof(railOffsets));
			}
			_endpoint = endpoint == WireRailEndpoint.Start
				? WireRailEndpoint.Start : WireRailEndpoint.End;
			SetDistance(_endpoint == WireRailEndpoint.Start ? 0f : splineLength, splineLength);
			_diameter = math.max(0.1f, diameter);
			var maximumRailIndex = math.max(0, railCount - 1);
			_firstRailIndex = math.clamp(firstRailIndex, 0, maximumRailIndex);
			_secondRailIndex = math.clamp(secondRailIndex, 0, maximumRailIndex);
			_offset = math.clamp(offset, 0f, math.max(0f, splineLength));
			_dropLength = math.max(0.1f, dropLength);
			_zAngle = zAngle;
			_railPairInitialized = true;
			// Snapshot the incoming cutoffs first: callers may pass this fixture's own
			// RailOffsets list, which Clear() below would otherwise empty before we read it.
			var incoming = new float[railCount];
			for (var railIndex = 0; railIndex < railCount; railIndex++) {
				incoming[railIndex] = railIndex < railOffsets.Count ? railOffsets[railIndex] : 0f;
			}
			_railOffsets ??= new List<float>();
			_railOffsets.Clear();
			for (var railIndex = 0; railIndex < railCount; railIndex++) {
				_railOffsets.Add(IsAttachedRail(railIndex)
					? 0f : math.clamp(incoming[railIndex], 0f, math.max(0f, splineLength)));
			}
		}
	}

	/// <summary>
	/// An endpoint-only fitting that moves each rail's visible and collidable start or end
	/// independently inward along the complete route.
	/// </summary>
	[Serializable]
	public sealed class WireRailTrimFixture : WireRailFixture
	{
		[SerializeField] private WireRailEndpoint _endpoint = WireRailEndpoint.End;
		[SerializeField] private List<float> _railOffsets = new();

		public WireRailEndpoint Endpoint => _endpoint;
		public int RailCount => _railOffsets?.Count ?? 0;
		public IReadOnlyList<float> RailOffsets => _railOffsets;

		public float GetRailOffset(int railIndex)
		{
			if (_railOffsets == null || railIndex < 0 || railIndex >= _railOffsets.Count) {
				throw new ArgumentOutOfRangeException(nameof(railIndex));
			}
			return _railOffsets[railIndex];
		}

		internal bool EnsureRailTrimInitialized(float splineLength, int railCount)
		{
			var changed = EnsureInitialized(splineLength);
			var endpoint = _endpoint == WireRailEndpoint.Start
				? WireRailEndpoint.Start : WireRailEndpoint.End;
			if (_endpoint != endpoint) {
				_endpoint = endpoint;
				changed = true;
			}
			var endpointDistance = _endpoint == WireRailEndpoint.Start ? 0f : splineLength;
			if (!Mathf.Approximately(Distance, endpointDistance)) {
				SetDistance(endpointDistance, splineLength);
				changed = true;
			}
			_railOffsets ??= new List<float>();
			while (_railOffsets.Count < railCount) {
				_railOffsets.Add(0f);
				changed = true;
			}
			if (_railOffsets.Count > railCount) {
				_railOffsets.RemoveRange(railCount, _railOffsets.Count - railCount);
				changed = true;
			}
			for (var railIndex = 0; railIndex < _railOffsets.Count; railIndex++) {
				var offset = math.clamp(_railOffsets[railIndex], 0f,
					math.max(0f, splineLength));
				if (Mathf.Approximately(_railOffsets[railIndex], offset)) {
					continue;
				}
				_railOffsets[railIndex] = offset;
				changed = true;
			}
			return changed;
		}

		internal void SetProperties(float splineLength, int railCount,
			WireRailEndpoint endpoint, IReadOnlyList<float> railOffsets)
		{
			if (railOffsets == null) {
				throw new ArgumentNullException(nameof(railOffsets));
			}
			_endpoint = endpoint == WireRailEndpoint.Start
				? WireRailEndpoint.Start : WireRailEndpoint.End;
			SetDistance(_endpoint == WireRailEndpoint.Start ? 0f : splineLength, splineLength);
			_railOffsets ??= new List<float>();
			_railOffsets.Clear();
			for (var railIndex = 0; railIndex < railCount; railIndex++) {
				var offset = railIndex < railOffsets.Count ? railOffsets[railIndex] : 0f;
				_railOffsets.Add(math.clamp(offset, 0f, math.max(0f, splineLength)));
			}
		}
	}

	[Serializable]
	[MovedFrom(true, sourceClassName: "WireRailLegFixture")]
	public sealed class WireRailStandFixture : WireRailFixture
	{
		public const float DefaultStartLength = 40f;
		public const float DefaultFootWidth = 30f;
		public const float DefaultFootLength = 30f;
		public const float DefaultFootConnectionLength = 30f;
		public const int FootBendSegments = 12;

		public static readonly Vector3 DefaultStartDirection = new(0f, 0f, -1f);
		public static readonly Vector3 DefaultFootPosition = new(15f, -22.5f, -80f);

		[SerializeField, Min(0.1f)] private float _diameter = WireRailLayout.ReferenceWireDiameter;
		[SerializeField] private WireRailStandSide _legSide = WireRailStandSide.Right;
		[SerializeField] private float _lateralOffset;
		[SerializeField] private float _verticalOffset;
		[SerializeField] private float _lengthAdjustment;
		[SerializeField] private Vector3 _startDirection = new(0f, 0f, -1f);
		[SerializeField, Min(0f)] private float _startLength = DefaultStartLength;
		[SerializeField] private Vector3 _footPosition = new(15f, -22.5f, -80f);
		[SerializeField] private Vector3 _footRotation;
		[SerializeField] private bool _footClockwise;
		[SerializeField, Min(0.1f)] private float _footWidth = DefaultFootWidth;
		[SerializeField, Min(0f)] private float _footLength = DefaultFootLength;
		[SerializeField, Min(0f)] private float _footConnectionLength = DefaultFootConnectionLength;

		public float Diameter => _diameter;
		public WireRailStandSide LegSide => _legSide;
		public float LateralOffset => _lateralOffset;
		public float VerticalOffset => _verticalOffset;
		public float LengthAdjustment => _lengthAdjustment;
		public Vector3 StartDirection => _startDirection;
		public float StartLength => _startLength;
		public Vector3 FootPosition => _footPosition;
		public Vector3 FootRotation => _footRotation;
		public bool FootClockwise => _footClockwise;
		public float FootWidth => _footWidth;
		public float FootLength => _footLength;
		public float FootConnectionLength => _footConnectionLength;

		internal bool EnsureStandInitialized(float splineLength)
		{
			var changed = EnsureInitialized(splineLength);
			var diameter = math.max(0.1f, _diameter);
			var startDirection = _startDirection.sqrMagnitude > 1e-8f
				? _startDirection.normalized : DefaultStartDirection;
			var startLength = math.max(0f, _startLength);
			var footWidth = math.max(0.1f, _footWidth);
			var footLength = math.max(0f, _footLength);
			var footConnectionLength = math.max(0f, _footConnectionLength);
			if (!Mathf.Approximately(_diameter, diameter)) {
				_diameter = diameter;
				changed = true;
			}
			if ((_startDirection - startDirection).sqrMagnitude > 1e-8f) {
				_startDirection = startDirection;
				changed = true;
			}
			if (!Mathf.Approximately(_startLength, startLength)) {
				_startLength = startLength;
				changed = true;
			}
			if (!Mathf.Approximately(_footWidth, footWidth)) {
				_footWidth = footWidth;
				changed = true;
			}
			if (!Mathf.Approximately(_footLength, footLength)) {
				_footLength = footLength;
				changed = true;
			}
			if (!Mathf.Approximately(_footConnectionLength, footConnectionLength)) {
				_footConnectionLength = footConnectionLength;
				changed = true;
			}
			return changed;
		}

		internal bool SetDiameter(float diameter)
		{
			diameter = math.max(0.1f, diameter);
			if (Mathf.Approximately(_diameter, diameter)) {
				return false;
			}
			_diameter = diameter;
			return true;
		}

		internal void SetProperties(float distance, float splineLength, float diameter,
			WireRailStandSide legSide, Vector3 startDirection, float startLength,
			Vector3 footPosition, Vector3 footRotation, float footWidth, float footLength,
			float footConnectionLength, float lateralOffset = 0f,
			float verticalOffset = 0f, float lengthAdjustment = 0f,
			bool footClockwise = false)
		{
			SetDistance(distance, splineLength);
			_diameter = math.max(0.1f, diameter);
			_legSide = legSide;
			_lateralOffset = lateralOffset;
			_verticalOffset = verticalOffset;
			_lengthAdjustment = lengthAdjustment;
			_startDirection = startDirection.sqrMagnitude > 1e-8f
				? startDirection.normalized : DefaultStartDirection;
			_startLength = math.max(0f, startLength);
			_footPosition = footPosition;
			_footRotation = footRotation;
			_footClockwise = footClockwise;
			_footWidth = math.max(0.1f, footWidth);
			_footLength = math.max(0f, footLength);
			_footConnectionLength = math.max(0f, footConnectionLength);
		}
	}

	public readonly struct WireRailRingCrossSection
	{
		public readonly Vector2 CenterOffset;
		public readonly float BaseRadius;
		public readonly float Radius;
		// The wires the ring wraps around, in the route-local (lateral, vertical) plane.
		public readonly Vector2[] RailOffsets;
		public readonly float[] RailRadii;

		internal WireRailRingCrossSection(float2 centerOffset, float baseRadius,
			float radius, float2[] railOffsets, float[] railRadii)
		{
			CenterOffset = new Vector2(centerOffset.x, centerOffset.y);
			BaseRadius = baseRadius;
			Radius = radius;
			var count = railOffsets?.Length ?? 0;
			RailOffsets = new Vector2[count];
			RailRadii = new float[count];
			for (var i = 0; i < count; i++) {
				RailOffsets[i] = new Vector2(railOffsets[i].x, railOffsets[i].y);
				RailRadii[i] = railRadii[i];
			}
		}
	}

	public readonly struct WireRailRungCrossSection
	{
		public readonly Vector2 StartRailOffset;
		public readonly Vector2 EndRailOffset;
		public readonly float StartRailRadius;
		public readonly float EndRailRadius;
		public readonly Vector2 StartOffset;
		public readonly Vector2 EndOffset;

		internal WireRailRungCrossSection(float2 startRailOffset,
			float2 endRailOffset, float startRailRadius, float endRailRadius,
			float2 startOffset, float2 endOffset)
		{
			StartRailOffset = new Vector2(startRailOffset.x, startRailOffset.y);
			EndRailOffset = new Vector2(endRailOffset.x, endRailOffset.y);
			StartRailRadius = startRailRadius;
			EndRailRadius = endRailRadius;
			StartOffset = new Vector2(startOffset.x, startOffset.y);
			EndOffset = new Vector2(endOffset.x, endOffset.y);
		}
	}

	public readonly struct WireRailStandPreview
	{
		public readonly Vector3 StartRailOffset;
		public readonly Vector3 EndRailOffset;
		public readonly float StartRailRadius;
		public readonly float EndRailRadius;
		public readonly IReadOnlyList<Vector3> CenterlinePoints;

		internal WireRailStandPreview(Vector3 startRailOffset, Vector3 endRailOffset,
			float startRailRadius, float endRailRadius,
			IReadOnlyList<Vector3> centerlinePoints)
		{
			StartRailOffset = startRailOffset;
			EndRailOffset = endRailOffset;
			StartRailRadius = startRailRadius;
			EndRailRadius = endRailRadius;
			CenterlinePoints = centerlinePoints;
		}
	}

	public readonly struct WireRailCradlePreview
	{
		public readonly IReadOnlyList<Vector2> RailOffsets;
		public readonly IReadOnlyList<float> RailRadii;
		public readonly IReadOnlyList<Vector2> CenterlinePoints;

		internal WireRailCradlePreview(IReadOnlyList<Vector2> railOffsets,
			IReadOnlyList<float> railRadii, IReadOnlyList<Vector2> centerlinePoints)
		{
			RailOffsets = railOffsets;
			RailRadii = railRadii;
			CenterlinePoints = centerlinePoints;
		}
	}

	public readonly struct WireRailElbowPreview
	{
		public readonly IReadOnlyList<Vector3> FirstRailPoints;
		public readonly IReadOnlyList<Vector3> SecondRailPoints;
		public readonly float FirstRailRadius;
		public readonly float SecondRailRadius;

		internal WireRailElbowPreview(IReadOnlyList<Vector3> firstRailPoints,
			IReadOnlyList<Vector3> secondRailPoints, float firstRailRadius,
			float secondRailRadius)
		{
			FirstRailPoints = firstRailPoints;
			SecondRailPoints = secondRailPoints;
			FirstRailRadius = firstRailRadius;
			SecondRailRadius = secondRailRadius;
		}
	}

	public readonly struct WireRailHairpinPreview
	{
		public readonly IReadOnlyList<Vector3> CenterlinePoints;
		public readonly float Radius;

		internal WireRailHairpinPreview(IReadOnlyList<Vector3> centerlinePoints,
			float radius)
		{
			CenterlinePoints = centerlinePoints;
			Radius = radius;
		}
	}

	/// <summary>
	/// Native Unity spline authoring with independently positioned wire layouts and fixtures.
	/// The spline helper stores raw VPX coordinates; its transform converts them into Unity space.
	/// </summary>
	[ExecuteAlways]
	[DisallowMultipleComponent]
	[AddComponentMenu("Pinball/Game Item/Wire Rail")]
	public class WireRailComponent : MonoBehaviour, ICollidableComponent
	{
		private const string SplineObjectName = "Wire Rail Spline";
		private static readonly ProfilerMarker SynchronizeSegmentsMarker =
			new("WireRail.SynchronizeSegments");
		private static readonly ProfilerMarker RenderMeshMarker =
			new("WireRail.RenderMesh");
		private static readonly ProfilerMarker ColliderMeshMarker =
			new("WireRail.ColliderMesh");
		private static Material _builtinDefaultMaterial;

		[SerializeField] private SplineContainer _splineContainer;
		[SerializeField] private List<WireRailSegment> _segments = new();
		[SerializeField, HideInInspector] private List<int> _layoutDisplayOrder = new();
		[SerializeReference] private List<WireRailFixture> _fixtures = new();
		// Reusable buffer holding only the enabled fixtures, fed to the render generator.
		// Disabled fixtures are hidden from rendering but left untouched for colliders.
		private readonly List<WireRailFixture> _enabledFixtures = new();
		[SerializeField, Range(1, 6)] private int _railCount = 4;
		[SerializeField, HideInInspector] private bool _railCountInitialized;
		[SerializeField, Min(0.1f)] private float _wireDiameter = WireRailLayout.DefaultWireDiameter;
		[SerializeField, Min(0f), FormerlySerializedAs("_braceCapBevelSize")]
		private float _wireCapBevelSize = 0.5f;
		[SerializeField, Range(6, 16)] private int _radialSegments = 10;
		[SerializeField, Range(2, 64)] private int _renderSamplesPerSegment = 16;
		[SerializeField] private Material _renderMaterial;
		[SerializeField, Min(1f)] private float _referenceBallDiameter = WireRailLayout.ReferenceBallDiameter;
		[SerializeField, Range(2, 32)] private int _colliderSamplesPerSegment = 8;
		[SerializeField] private bool _showColliderPreview;
		[SerializeReference] private PhysicsMaterialAsset _physicsMaterial;
		[SerializeField] private PhysicsMaterialAsset _terminalPhysicsMaterial;
		[SerializeField] private bool _overwritePhysics = true;
		[SerializeField, Min(0f)] private float _elasticity = 0.3f;
		[SerializeField, Min(0f)] private float _elasticityFalloff = 0.5f;
		[SerializeField, Min(0f)] private float _friction = 0.15f;
		[SerializeField, Range(-90f, 90f)] private float _scatter;
		[NonSerialized] private Mesh _renderMesh;
		[NonSerialized] private Mesh _colliderMesh;
		[NonSerialized] private Vector3[] _colliderEdgeVertices = Array.Empty<Vector3>();
		[NonSerialized] private int _colliderTopologyRetryCount;
		[NonSerialized] private bool _rebuildingGeneratedMeshes;
		[NonSerialized] private bool _collidersDirty = true;
		[NonSerialized] private bool _colliderGeometryDirty = true;
		[NonSerialized] private int _renderGeometryVersion;
		[NonSerialized] private int _colliderGeometryVersion;
		[NonSerialized] private int _renderMeshGenerationCount;
		[NonSerialized] private readonly List<int2> _renderSegmentIndexRanges = new();
		[NonSerialized] private readonly List<int2> _enabledFixtureIndexRanges = new();
		[NonSerialized] private readonly List<int2> _renderFixtureIndexRanges = new();
		[NonSerialized] private string _generationError;
#if UNITY_EDITOR
		[NonSerialized] private bool _editorRebuildScheduled;
		[NonSerialized] private bool _editorRebuildNeedsInvalidation;
		[NonSerialized] internal bool DeferEditorRebuildsForTesting;
#endif

		public SplineContainer SplineContainer => GetSplineContainerWithoutCreating();
		public IReadOnlyList<WireRailSegment> Segments => _segments;
		public IReadOnlyList<WireRailSegment> Layouts => _segments;
		public IReadOnlyList<int> LayoutDisplayOrder => _layoutDisplayOrder;
		public IReadOnlyList<WireRailFixture> Fixtures => _fixtures;
		public int RailCount => _railCount;
		public float WireDiameter => _wireDiameter;
		public float WireCapBevelSize => _wireCapBevelSize;
		public float SplineLength {
			get {
				var container = GetSplineContainerWithoutCreating();
				return container && container.Spline != null ? container.Spline.GetLength() : 0f;
			}
		}
		public string GenerationError => _generationError;
		public bool ShowColliderPreview => _showColliderPreview;
		public Mesh RenderMesh => _renderMesh;

		/// <summary>
		/// Index range (start, count) of each layout span's rail tubes in <see cref="RenderMesh"/>.
		/// </summary>
		public IReadOnlyList<int2> RenderSegmentIndexRanges => _renderSegmentIndexRanges;

		/// <summary>
		/// Index range (start, count) of each fixture in <see cref="RenderMesh"/>, one entry
		/// per entry of <see cref="Fixtures"/>. Disabled or omitted fixtures have an empty range.
		/// </summary>
		public IReadOnlyList<int2> RenderFixtureIndexRanges => _renderFixtureIndexRanges;
		public Mesh ColliderMesh {
			get {
				if (isActiveAndEnabled) {
					EnsureColliderMesh();
				}
				return _colliderMesh;
			}
		}
		public int ColliderTopologyRetryCount => _colliderTopologyRetryCount;
		public int RenderGeometryVersion => _renderGeometryVersion;
		public int ColliderGeometryVersion => _colliderGeometryVersion;
		public bool ColliderGeometryDirty => _colliderGeometryDirty;
		internal int RenderMeshGenerationCount => _renderMeshGenerationCount;

		/// <summary>
		/// Increments every time <see cref="RenderMesh"/> is actually regenerated. Unlike
		/// <see cref="RenderGeometryVersion"/>, which advances when a rebuild is requested,
		/// this only moves once the mesh data has changed.
		/// </summary>
		public int RenderMeshVersion => _renderMeshGenerationCount;

		private void Reset()
		{
			EnsureSplineContainer();
			SynchronizeSegments();
			RebuildGeneratedMeshes();
		}

		private void OnEnable()
		{
			Subscribe();

			var container = GetSplineContainerWithoutCreating();
			if (container) {
				HardenSplineChild(container.gameObject);
				SynchronizeSegments();
				RebuildGeneratedMeshes();
			}
			GetComponentInParent<PhysicsEngine>()?.EnableCollider(ItemId);
		}

		private void Subscribe()
		{
			Spline.Changed -= OnSplineChanged;
			Spline.Changed += OnSplineChanged;
			UnityEngine.Splines.SplineContainer.SplineAdded -= OnSplineCollectionChanged;
			UnityEngine.Splines.SplineContainer.SplineAdded += OnSplineCollectionChanged;
			UnityEngine.Splines.SplineContainer.SplineRemoved -= OnSplineCollectionChanged;
			UnityEngine.Splines.SplineContainer.SplineRemoved += OnSplineCollectionChanged;
#if UNITY_EDITOR
			Undo.undoRedoPerformed -= OnUndoRedo;
			Undo.undoRedoPerformed += OnUndoRedo;
#endif
		}

		private void OnDisable()
		{
			Spline.Changed -= OnSplineChanged;
			UnityEngine.Splines.SplineContainer.SplineAdded -= OnSplineCollectionChanged;
			UnityEngine.Splines.SplineContainer.SplineRemoved -= OnSplineCollectionChanged;
#if UNITY_EDITOR
			Undo.undoRedoPerformed -= OnUndoRedo;
			if (_editorRebuildScheduled) {
				EditorApplication.delayCall -= RebuildAfterValidation;
				_editorRebuildScheduled = false;
				_editorRebuildNeedsInvalidation = false;
			}
#endif
			GetComponentInParent<PhysicsEngine>()?.DisableCollider(ItemId);
			DestroyGeneratedMeshes();
		}

		private void OnValidate()
		{
			_railCount = math.clamp(_railCount, 1, 6);
			_wireDiameter = math.max(0.1f, _wireDiameter);
			_wireCapBevelSize = math.max(0f, _wireCapBevelSize);
			_radialSegments = math.clamp(_radialSegments, 6, 16);
			_renderSamplesPerSegment = math.clamp(_renderSamplesPerSegment, 2, 64);
			_referenceBallDiameter = math.max(1f, _referenceBallDiameter);
			_colliderSamplesPerSegment = math.clamp(_colliderSamplesPerSegment, 2, 32);
			SynchronizeFixtures();
			if (!GetSplineContainerWithoutCreating()) {
				return;
			}
#if UNITY_EDITOR
			ScheduleEditorRebuild(true);
#endif
		}

#if UNITY_EDITOR
		private void RebuildAfterValidation()
		{
			_editorRebuildScheduled = false;
			if (!this || !GetSplineContainerWithoutCreating()) {
				return;
			}
			if (_editorRebuildNeedsInvalidation) {
				InvalidateGeneratedGeometry();
			}
			_editorRebuildNeedsInvalidation = false;
			RebuildGeneratedMeshesImmediately();
			SceneView.RepaintAll();
		}

		private void ScheduleEditorRebuild(bool needsInvalidation)
		{
			_editorRebuildNeedsInvalidation |= needsInvalidation;
			if (_editorRebuildScheduled) {
				return;
			}
			_editorRebuildScheduled = true;
			EditorApplication.delayCall += RebuildAfterValidation;
		}

		internal void FlushDeferredEditorRebuildForTesting()
		{
			if (!_editorRebuildScheduled) {
				return;
			}
			EditorApplication.delayCall -= RebuildAfterValidation;
			RebuildAfterValidation();
		}

		private void OnUndoRedo()
		{
			if (!this || !GetSplineContainerWithoutCreating()) {
				return;
			}
			RebuildGeneratedMeshes();
			SceneView.RepaintAll();
		}
#endif

		public void SetRailCount(int railCount)
		{
			if (railCount < 1 || railCount > 6) {
				throw new ArgumentOutOfRangeException(nameof(railCount), railCount,
					"A wire rail supports between one and six rails.");
			}
			SynchronizeSegments();
			if (_railCount == railCount) {
				return;
			}
			_railCount = railCount;
			_railCountInitialized = true;
			foreach (var layout in _segments) {
				layout.ResizeRailCount(railCount, _wireDiameter, true, true);
			}
			SynchronizeSegmentConnections();
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetRailsActive(int segmentIndex, IReadOnlyList<int> railIndices,
			bool active)
		{
			if (railIndices == null) {
				throw new ArgumentNullException(nameof(railIndices));
			}
			var segment = GetSegment(segmentIndex);
			foreach (var railIndex in railIndices) {
				if (railIndex < 0 || railIndex >= segment.RailCount) {
					throw new ArgumentOutOfRangeException(nameof(railIndices), railIndex,
						$"Layout {GetLayoutDisplayIndex(segmentIndex) + 1} has "
							+ $"{segment.RailCount} wire(s).");
				}
			}
			foreach (var railIndex in railIndices) {
				segment.SetRailActive(railIndex, active);
			}
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetWireContinuous(int segmentIndex, int wireIndex, bool continuous)
		{
			SynchronizeSegments();
			var nextSegmentIndex = GetNextSegmentIndex(segmentIndex);
			if (nextSegmentIndex < 0) {
				throw new InvalidOperationException(
					$"Segment {segmentIndex + 1} has no following segment.");
			}
			var wireCount = _railCount;
			var connection = _segments[segmentIndex].ConnectionToNext;
			connection.EnsureInitialized(wireCount);
			connection.SetWireContinuous(wireIndex, continuous);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetWireTransitionOverride(int segmentIndex, int wireIndex,
			bool overridden)
		{
			SynchronizeSegments();
			var nextSegmentIndex = GetNextSegmentIndex(segmentIndex);
			if (nextSegmentIndex < 0) {
				throw new InvalidOperationException(
					$"Segment {segmentIndex + 1} has no following segment.");
			}
			var wireCount = _railCount;
			var connection = _segments[segmentIndex].ConnectionToNext;
			connection.EnsureInitialized(wireCount);
			connection.SetWireOverridden(wireIndex, overridden);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetWireTransitionCurve(int segmentIndex, int wireIndex,
			AnimationCurve curve)
		{
			SynchronizeSegments();
			var nextSegmentIndex = GetNextSegmentIndex(segmentIndex);
			if (nextSegmentIndex < 0) {
				throw new InvalidOperationException(
					$"Segment {segmentIndex + 1} has no following segment.");
			}
			var wireCount = _railCount;
			var connection = _segments[segmentIndex].ConnectionToNext;
			connection.EnsureInitialized(wireCount);
			connection.SetWireCurve(wireIndex, curve);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public int GetNextSegmentIndex(int segmentIndex)
		{
			if (_segments == null || segmentIndex < 0 || segmentIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(segmentIndex));
			}
			if (segmentIndex + 1 < _segments.Count) {
				return segmentIndex + 1;
			}
			var container = GetSplineContainerWithoutCreating();
			return container && container.Spline != null && container.Spline.Closed
				&& _segments.Count > 1 ? 0 : -1;
		}

		public void SetThirdRailSide(int segmentIndex, WireRailThirdRailSide side)
		{
			GetSegment(segmentIndex).SetThirdRailSide(side);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetRailOffset(int segmentIndex, int railIndex, Vector2 offset)
		{
			GetSegment(segmentIndex).SetRailOffset(railIndex, offset);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetWireDiameter(float diameter)
		{
			if (diameter <= 0f) {
				throw new ArgumentOutOfRangeException(nameof(diameter), diameter,
					"Wire diameter must be positive.");
			}
			_wireDiameter = diameter;
			SynchronizeSegments();
			foreach (var segment in _segments) {
				segment.SetAllWireDiameters(diameter);
			}
			SynchronizeFixtures();
			foreach (var ring in _fixtures.OfType<WireRailRingFixture>()) {
				ring.SetDiameter(diameter);
			}
			foreach (var cradle in _fixtures.OfType<WireRailCradleFixture>()) {
				cradle.SetDiameter(diameter);
			}
			foreach (var rung in _fixtures.OfType<WireRailRungFixture>()) {
				rung.SetDiameter(diameter);
			}
			foreach (var leg in _fixtures.OfType<WireRailStandFixture>()) {
				leg.SetDiameter(diameter);
			}
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetWireProperties(int segmentIndex, IReadOnlyList<int> railIndices,
			IReadOnlyList<Vector2> offsets)
		{
			if (railIndices == null || offsets == null
				|| railIndices.Count != offsets.Count) {
				throw new ArgumentException("Wire indices and offsets must have matching counts.");
			}
			var segment = GetSegment(segmentIndex);
			for (var i = 0; i < railIndices.Count; i++) {
				if (railIndices[i] < 0 || railIndices[i] >= segment.RailCount) {
					throw new ArgumentOutOfRangeException(nameof(railIndices), railIndices[i],
						$"Segment {segmentIndex + 1} has {segment.RailCount} wire(s).");
				}
			}
			for (var i = 0; i < railIndices.Count; i++) {
				segment.SetRailOffset(railIndices[i], offsets[i]);
			}
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void ApplyWirePositionsToAllLayouts(int sourceSegmentIndex,
			IReadOnlyList<int> railIndices)
		{
			if (railIndices == null) {
				throw new ArgumentNullException(nameof(railIndices));
			}
			var sourceSegment = GetSegment(sourceSegmentIndex);
			if (railIndices.Count == 0) {
				return;
			}
			var offsets = new Vector2[railIndices.Count];
			for (var i = 0; i < railIndices.Count; i++) {
				var railIndex = railIndices[i];
				if (railIndex < 0 || railIndex >= sourceSegment.RailCount) {
					throw new ArgumentOutOfRangeException(nameof(railIndices), railIndex,
						$"Layout {GetLayoutDisplayIndex(sourceSegmentIndex) + 1} has "
							+ $"{sourceSegment.RailCount} wire(s).");
				}
				offsets[i] = sourceSegment.GetRailOffset(railIndex);
			}
			foreach (var segment in _segments) {
				for (var i = 0; i < railIndices.Count; i++) {
					segment.SetRailOffset(railIndices[i], offsets[i]);
				}
			}
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public bool CenterPivot()
		{
			var container = GetSplineContainerWithoutCreating();
			var spline = container ? container.Spline : null;
			if (spline == null || spline.Count < 2
				|| !WireRailSplineGeometry.TryEvaluateDistance(spline,
					spline.GetLength() * 0.5f, out var midpoint)) {
				return false;
			}

			var midpointWorld = container.transform.TransformPoint((Vector3)midpoint.Position);
			var worldDelta = midpointWorld - transform.position;
			if (worldDelta.sqrMagnitude <= 1e-12f) {
				return false;
			}
			var splineDelta = (float3)container.transform.InverseTransformVector(worldDelta);
			transform.position = midpointWorld;
			for (var knotIndex = 0; knotIndex < spline.Count - 1; knotIndex++) {
				var knot = spline[knotIndex];
				knot.Position -= splineDelta;
				spline.SetKnotNoNotify(knotIndex, knot);
			}
			var lastKnotIndex = spline.Count - 1;
			var lastKnot = spline[lastKnotIndex];
			lastKnot.Position -= splineDelta;
			spline.SetKnot(lastKnotIndex, lastKnot);
			_collidersDirty = true;
			return true;
		}

		public void ResetSegmentLayout(int segmentIndex)
		{
			GetSegment(segmentIndex).ResetLayout();
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public int AddRingFixture(float distance)
		{
			_fixtures ??= new List<WireRailFixture>();
			var ring = new WireRailRingFixture();
			ring.SetProperties(distance, SplineLength, _wireDiameter, false,
				WireRailRingFixture.DefaultCutoutStartAngle,
				WireRailRingFixture.DefaultCutoutEndAngle, false,
				WireRailRingFixture.DefaultStraightStartAngle,
				WireRailRingFixture.DefaultStraightEndAngle, 0f, 0f, 1f,
				WireRailRingFixture.DefaultRingDensity);
			_fixtures.Add(ring);
			RebuildRenderGeometry();
			MarkDirty();
			return _fixtures.Count - 1;
		}

		public int AddRungFixture(float distance)
		{
			_fixtures ??= new List<WireRailFixture>();
			var rung = new WireRailRungFixture();
			rung.SetProperties(distance, SplineLength, _wireDiameter, 0, 1,
				WireRailRungFixture.DefaultAngle, 0f, 0f, 0f);
			_fixtures.Add(rung);
			RebuildRenderGeometry();
			MarkDirty();
			return _fixtures.Count - 1;
		}

		public int AddCradleFixture(float distance)
		{
			_fixtures ??= new List<WireRailFixture>();
			var cradle = new WireRailCradleFixture();
			cradle.SetProperties(distance, SplineLength, _wireDiameter,
				WireRailCradleFixture.DefaultRingDensity, 0f, 0f,
				WireRailCradleFixture.DefaultBottomLength,
				WireRailCradleFixture.DefaultLeftLength,
				WireRailCradleFixture.DefaultRightLength,
				WireRailCradleFixture.DefaultAngle,
				WireRailCradleFixture.DefaultRotation,
				WireRailCradleFixture.DefaultCornerRadius);
			_fixtures.Add(cradle);
			RebuildRenderGeometry();
			MarkDirty();
			return _fixtures.Count - 1;
		}

		public int AddStandFixture(float distance)
		{
			_fixtures ??= new List<WireRailFixture>();
			var leg = new WireRailStandFixture();
			leg.SetProperties(distance, SplineLength, _wireDiameter,
				WireRailStandSide.Right, WireRailStandFixture.DefaultStartDirection,
				WireRailStandFixture.DefaultStartLength,
				WireRailStandFixture.DefaultFootPosition, Vector3.zero,
				WireRailStandFixture.DefaultFootWidth, WireRailStandFixture.DefaultFootLength,
				WireRailStandFixture.DefaultFootConnectionLength);
			_fixtures.Add(leg);
			RebuildRenderGeometry();
			MarkDirty();
			return _fixtures.Count - 1;
		}

		public int AddHairpinFixture(WireRailEndpoint endpoint = WireRailEndpoint.End)
		{
			_fixtures ??= new List<WireRailFixture>();
			var hairpin = new WireRailHairpinFixture();
			hairpin.SetProperties(SplineLength, _wireDiameter, endpoint, 0, 1,
				WireRailHairpinFixture.DefaultLoopDiameter,
				WireRailHairpinFixture.DefaultLeadLength,
				WireRailHairpinFixture.DefaultTangentLength,
				WireRailHairpinFixture.DefaultRingDensity, 0f, 0f);
			_fixtures.Add(hairpin);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
			return _fixtures.Count - 1;
		}

		public int AddElbowFixture(WireRailEndpoint endpoint = WireRailEndpoint.End)
		{
			_fixtures ??= new List<WireRailFixture>();
			TryGetDefaultEndpointRailPair(endpoint, out var firstRailIndex,
				out var secondRailIndex);
			var elbow = new WireRailElbowFixture();
			elbow.SetProperties(SplineLength, _railCount, _wireDiameter, endpoint,
				firstRailIndex, secondRailIndex,
				WireRailElbowFixture.DefaultOffset,
				WireRailElbowFixture.DefaultDropLength, 0f, Array.Empty<float>());
			_fixtures.Add(elbow);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
			return _fixtures.Count - 1;
		}

		public int AddRailTrimFixture(WireRailEndpoint endpoint = WireRailEndpoint.End)
		{
			_fixtures ??= new List<WireRailFixture>();
			var railTrim = new WireRailTrimFixture();
			railTrim.SetProperties(SplineLength, _railCount, endpoint,
				Array.Empty<float>());
			_fixtures.Add(railTrim);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
			return _fixtures.Count - 1;
		}

		public bool TryGetRingCrossSection(int fixtureIndex,
			out WireRailRingCrossSection crossSection)
		{
			crossSection = default;
			if (_fixtures == null || fixtureIndex < 0 || fixtureIndex >= _fixtures.Count
				|| _fixtures[fixtureIndex] is not WireRailRingFixture ring
				|| !_splineContainer || _splineContainer.Spline == null
				|| !WireRailFixtureMeshGenerator.TryEvaluateRingProfile(
					_splineContainer.Spline, _segments, ring, out var profile)) {
				return false;
			}
			crossSection = new WireRailRingCrossSection(profile.CenterOffset,
				profile.BaseRadius, profile.Radius, profile.RailOffsets, profile.RailRadii);
			return true;
		}

		public bool TryGetRungCrossSection(int fixtureIndex,
			out WireRailRungCrossSection crossSection)
		{
			crossSection = default;
			if (_fixtures == null || fixtureIndex < 0 || fixtureIndex >= _fixtures.Count
				|| _fixtures[fixtureIndex] is not WireRailRungFixture rung
				|| !_splineContainer || _splineContainer.Spline == null
				|| !WireRailFixtureMeshGenerator.TryEvaluateRungProfile(
					_splineContainer.Spline, _segments, rung, out var profile)) {
				return false;
			}
			crossSection = new WireRailRungCrossSection(profile.StartRailOffset,
				profile.EndRailOffset, profile.StartRailRadius, profile.EndRailRadius,
				profile.StartOffset, profile.EndOffset);
			return true;
		}

		public bool TryGetStandPreview(int fixtureIndex, out WireRailStandPreview preview)
		{
			preview = default;
			if (_fixtures == null || fixtureIndex < 0 || fixtureIndex >= _fixtures.Count
				|| _fixtures[fixtureIndex] is not WireRailStandFixture leg
				|| !_splineContainer || _splineContainer.Spline == null
				|| !WireRailFixtureMeshGenerator.TryEvaluateStandProfile(
					_splineContainer.Spline, _segments, leg, out var profile)) {
				return false;
			}
			preview = new WireRailStandPreview(
				ToLocalOffset(profile.AttachmentProfile.StartRailOffset),
				ToLocalOffset(profile.AttachmentProfile.EndRailOffset),
				profile.AttachmentProfile.StartRailRadius,
				profile.AttachmentProfile.EndRailRadius,
				profile.CombinedPath.Select(point => ToLocalPosition(point)).ToArray());
			return true;

			Vector3 ToLocalOffset(float2 offset) => new(offset.x, 0f, offset.y);
			Vector3 ToLocalPosition(float3 position)
			{
				var relative = position - profile.AttachmentProfile.Frame.Position;
				return new Vector3(
					math.dot(relative, profile.AttachmentProfile.Frame.Right),
					math.dot(relative, profile.AttachmentProfile.Frame.Tangent),
					math.dot(relative, profile.AttachmentProfile.Frame.Up));
			}
		}

		public bool TryGetCradlePreview(int fixtureIndex, out WireRailCradlePreview preview)
		{
			preview = default;
			if (_fixtures == null || fixtureIndex < 0 || fixtureIndex >= _fixtures.Count
				|| _fixtures[fixtureIndex] is not WireRailCradleFixture cradle
				|| !_splineContainer || _splineContainer.Spline == null
				|| !WireRailFixtureMeshGenerator.TryEvaluateCradleProfile(
					_splineContainer.Spline, _segments, cradle, out var profile)) {
				return false;
			}
			preview = new WireRailCradlePreview(
				profile.RailOffsets.Select(offset => new Vector2(offset.x, offset.y)).ToArray(),
				profile.RailRadii.ToArray(),
				profile.CenterlinePoints.Select(point => {
					var relative = point - profile.Frame.Position;
					return new Vector2(math.dot(relative, profile.Frame.Right),
						math.dot(relative, profile.Frame.Up));
				}).ToArray());
			return true;
		}

		public bool TryGetElbowPreview(int fixtureIndex, out WireRailElbowPreview preview)
		{
			preview = default;
			if (_fixtures == null || fixtureIndex < 0 || fixtureIndex >= _fixtures.Count
				|| _fixtures[fixtureIndex] is not WireRailElbowFixture elbow
				|| !_splineContainer || _splineContainer.Spline == null
				|| !WireRailFixtureMeshGenerator.TryEvaluateElbowProfile(
					_splineContainer.Spline, _segments, elbow, out var profile)) {
				return false;
			}
			// Show a stretch of the rails leading into the elbow for context. This is preview-only
			// and prepended here; the generated mesh still uses the profile paths untouched.
			var desiredLead = math.max(30f, elbow.DropLength * 1.5f);
			WireRailFixtureMeshGenerator.TryEvaluateElbowIncomingLeads(_splineContainer.Spline,
				_segments, elbow, desiredLead, out var firstLead, out var secondLead);
			preview = new WireRailElbowPreview(
				BuildRailPath(firstLead, profile.FirstRailPoints),
				BuildRailPath(secondLead, profile.SecondRailPoints),
				profile.FirstRailRadius, profile.SecondRailRadius);
			return true;

			Vector3[] BuildRailPath(IReadOnlyList<float3> lead, IReadOnlyList<float3> railPoints)
			{
				var points = new List<Vector3>(
					(lead?.Count ?? 0) + railPoints.Count);
				if (lead != null) {
					foreach (var point in lead) {
						points.Add(ToLocalPosition(point));
					}
				}
				foreach (var point in railPoints) {
					points.Add(ToLocalPosition(point));
				}
				return points.ToArray();
			}

			Vector3 ToLocalPosition(float3 position)
			{
				var relative = position - profile.Frame.Position;
				return new Vector3(
					math.dot(relative, profile.Frame.Right),
					math.dot(relative, profile.Frame.Tangent),
					math.dot(relative, profile.Frame.Up));
			}
		}

		public bool TryGetHairpinPreview(int fixtureIndex,
			out WireRailHairpinPreview preview)
		{
			preview = default;
			if (_fixtures == null || fixtureIndex < 0 || fixtureIndex >= _fixtures.Count
				|| _fixtures[fixtureIndex] is not WireRailHairpinFixture hairpin
				|| !_splineContainer || _splineContainer.Spline == null
				|| !WireRailFixtureMeshGenerator.TryEvaluateHairpinProfile(
					_splineContainer.Spline, _segments, hairpin, out var profile)) {
				return false;
			}
			preview = new WireRailHairpinPreview(
				profile.CenterlinePoints.Select(point => ToLocalPosition(point)).ToArray(),
				hairpin.Diameter * 0.5f);
			return true;

			Vector3 ToLocalPosition(float3 position)
			{
				var relative = position - profile.Frame.Position;
				return new Vector3(
					math.dot(relative, profile.Frame.Right),
					math.dot(relative, profile.Frame.Tangent),
					math.dot(relative, profile.Frame.Up));
			}
		}

		public void RemoveFixture(int fixtureIndex)
		{
			var affectsCollider = GetFixture(fixtureIndex) is WireRailHairpinFixture
				or WireRailElbowFixture or WireRailTrimFixture;
			_fixtures.RemoveAt(fixtureIndex);
			if (affectsCollider) {
				InvalidateColliderGeometry();
			}
			RebuildRenderGeometry();
			MarkDirty();
		}

		public int DuplicateRingFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailRingFixture source) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a ring.",
					nameof(fixtureIndex));
			}
			var duplicate = new WireRailRingFixture();
			duplicate.SetProperties(source.Distance, SplineLength, _wireDiameter,
				source.HasCutout, source.CutoutStartAngle, source.CutoutEndAngle,
				source.HasStraightSection, source.StraightStartAngle,
				source.StraightEndAngle, source.LateralOffset, source.VerticalOffset,
				source.Scale, source.RingDensity);
			duplicate.SetSolderThreshold(source.SolderThreshold);
			duplicate.SetSolderSize(source.SolderSize);
			var duplicateIndex = fixtureIndex + 1;
			_fixtures.Insert(duplicateIndex, duplicate);
			RebuildRenderGeometry();
			MarkDirty();
			return duplicateIndex;
		}

		public int DuplicateRungFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailRungFixture source) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a rung.",
					nameof(fixtureIndex));
			}
			var duplicate = new WireRailRungFixture();
			duplicate.SetProperties(source.Distance, SplineLength, _wireDiameter,
				source.StartRailIndex, source.EndRailIndex,
				source.Angle, source.LateralOffset,
				source.VerticalOffset, source.LengthAdjustment);
			duplicate.SetSolderThreshold(source.SolderThreshold);
			duplicate.SetSolderSize(source.SolderSize);
			var duplicateIndex = fixtureIndex + 1;
			_fixtures.Insert(duplicateIndex, duplicate);
			RebuildRenderGeometry();
			MarkDirty();
			return duplicateIndex;
		}

		public int DuplicateCradleFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailCradleFixture source) {
				throw new ArgumentException(
					$"Fixture {fixtureIndex + 1} is not a cradle.",
					nameof(fixtureIndex));
			}
			var duplicate = new WireRailCradleFixture();
			duplicate.SetProperties(source.Distance, SplineLength, _wireDiameter,
				source.RingDensity, source.LateralOffset, source.VerticalOffset,
				source.BottomLength, source.LeftLength, source.RightLength,
				source.Angle, source.Rotation, source.CornerRadius);
			duplicate.SetSolderThreshold(source.SolderThreshold);
			duplicate.SetSolderSize(source.SolderSize);
			var duplicateIndex = fixtureIndex + 1;
			_fixtures.Insert(duplicateIndex, duplicate);
			RebuildRenderGeometry();
			MarkDirty();
			return duplicateIndex;
		}

		public int DuplicateStandFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailStandFixture source) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a stand.",
					nameof(fixtureIndex));
			}
			var duplicate = new WireRailStandFixture();
			duplicate.SetProperties(source.Distance, SplineLength, _wireDiameter,
				source.LegSide, source.StartDirection, source.StartLength,
				source.FootPosition, source.FootRotation, source.FootWidth,
				source.FootLength, source.FootConnectionLength, source.LateralOffset,
				source.VerticalOffset, source.LengthAdjustment, source.FootClockwise);
			duplicate.SetSolderThreshold(source.SolderThreshold);
			duplicate.SetSolderSize(source.SolderSize);
			var duplicateIndex = fixtureIndex + 1;
			_fixtures.Insert(duplicateIndex, duplicate);
			RebuildRenderGeometry();
			MarkDirty();
			return duplicateIndex;
		}

		public int DuplicateHairpinFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailHairpinFixture source) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a hairpin.",
					nameof(fixtureIndex));
			}
			var duplicate = new WireRailHairpinFixture();
			duplicate.SetProperties(SplineLength, _wireDiameter, source.Endpoint,
				source.FirstRailIndex, source.SecondRailIndex, source.LoopDiameter,
				source.LeadLength, source.TangentLength, source.RingDensity,
				source.RailOffset, source.Rotation);
			duplicate.SetSolderThreshold(source.SolderThreshold);
			duplicate.SetSolderSize(source.SolderSize);
			var duplicateIndex = fixtureIndex + 1;
			_fixtures.Insert(duplicateIndex, duplicate);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
			return duplicateIndex;
		}

		public int DuplicateElbowFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailElbowFixture source) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not an elbow.",
					nameof(fixtureIndex));
			}
			var duplicate = new WireRailElbowFixture();
			duplicate.SetProperties(SplineLength, _railCount, _wireDiameter, source.Endpoint,
				source.FirstRailIndex, source.SecondRailIndex, source.Offset,
				source.DropLength, source.ZAngle, source.RailOffsets);
			duplicate.SetSolderThreshold(source.SolderThreshold);
			duplicate.SetSolderSize(source.SolderSize);
			var duplicateIndex = fixtureIndex + 1;
			_fixtures.Insert(duplicateIndex, duplicate);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
			return duplicateIndex;
		}

		public int DuplicateRailTrimFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailTrimFixture source) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a rail trim.",
					nameof(fixtureIndex));
			}
			var duplicate = new WireRailTrimFixture();
			duplicate.SetProperties(SplineLength, _railCount, source.Endpoint,
				source.RailOffsets);
			var duplicateIndex = fixtureIndex + 1;
			_fixtures.Insert(duplicateIndex, duplicate);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
			return duplicateIndex;
		}

		public void SetRingFixtureProperties(int fixtureIndex, float distance,
			bool hasCutout, float cutoutStartAngle, float cutoutEndAngle,
			bool hasStraightSection = false, float straightStartAngle =
				WireRailRingFixture.DefaultStraightStartAngle, float straightEndAngle =
				WireRailRingFixture.DefaultStraightEndAngle, float lateralOffset = 0f,
			float verticalOffset = 0f, float scale = 1f,
			int ringDensity = 0)
		{
			if (GetFixture(fixtureIndex) is not WireRailRingFixture ring) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a ring.",
					nameof(fixtureIndex));
			}
			var resolvedRingDensity = ringDensity >= 3
				? ringDensity
				: (ring.RingDensity >= 3
					? ring.RingDensity : WireRailRingFixture.DefaultRingDensity);
			ring.SetProperties(distance, SplineLength, _wireDiameter, hasCutout,
				cutoutStartAngle, cutoutEndAngle, hasStraightSection, straightStartAngle,
				straightEndAngle, lateralOffset, verticalOffset, scale,
				resolvedRingDensity);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetRungFixtureProperties(int fixtureIndex, float distance,
			float angle, float lateralOffset = 0f,
			float verticalOffset = 0f, float lengthAdjustment = 0f)
		{
			if (GetFixture(fixtureIndex) is not WireRailRungFixture rung) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a rung.",
					nameof(fixtureIndex));
			}
			rung.SetProperties(distance, SplineLength, _wireDiameter,
				rung.StartRailIndex, rung.EndRailIndex, angle,
				lateralOffset, verticalOffset, lengthAdjustment);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetCradleFixtureProperties(int fixtureIndex, float distance,
			int ringDensity, float lateralOffset, float verticalOffset,
			float bottomLength, float leftLength, float rightLength, float angle,
			float rotation, float cornerRadius)
		{
			if (GetFixture(fixtureIndex) is not WireRailCradleFixture cradle) {
				throw new ArgumentException(
					$"Fixture {fixtureIndex + 1} is not a cradle.",
					nameof(fixtureIndex));
			}
			cradle.SetProperties(distance, SplineLength, _wireDiameter, ringDensity,
				lateralOffset, verticalOffset, bottomLength, leftLength, rightLength,
				angle, rotation, cornerRadius);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetStandFixtureProperties(int fixtureIndex, float distance,
			WireRailStandSide legSide, Vector3 startDirection, float startLength,
			Vector3 footPosition, Vector3 footRotation, float footWidth, float footLength,
			float footConnectionLength, float lateralOffset = 0f,
			float verticalOffset = 0f, float lengthAdjustment = 0f,
			bool? footClockwise = null)
		{
			if (GetFixture(fixtureIndex) is not WireRailStandFixture leg) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a stand.",
					nameof(fixtureIndex));
			}
			leg.SetProperties(distance, SplineLength, _wireDiameter, legSide,
				startDirection, startLength, footPosition, footRotation,
				footWidth, footLength, footConnectionLength, lateralOffset,
				verticalOffset, lengthAdjustment, footClockwise ?? leg.FootClockwise);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void MirrorStandFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailStandFixture leg) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a stand.",
					nameof(fixtureIndex));
			}
			var mirroredRotation = leg.FootRotation;
			mirroredRotation.y = -mirroredRotation.y;
			mirroredRotation.z = -mirroredRotation.z;
			var startDirection = leg.StartDirection;
			startDirection.x = -startDirection.x;
			var footPosition = leg.FootPosition;
			footPosition.x = -footPosition.x;
			leg.SetProperties(leg.Distance, SplineLength, _wireDiameter,
				leg.LegSide == WireRailStandSide.Left
					? WireRailStandSide.Right : WireRailStandSide.Left,
				startDirection, leg.StartLength, footPosition,
				mirroredRotation, leg.FootWidth, leg.FootLength,
				leg.FootConnectionLength, -leg.LateralOffset, leg.VerticalOffset,
				leg.LengthAdjustment, !leg.FootClockwise);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetHairpinFixtureProperties(int fixtureIndex,
			WireRailEndpoint endpoint, int firstRailIndex, int secondRailIndex,
			float loopDiameter, float leadLength, float tangentLength, int ringDensity,
			float railOffset, float rotation)
		{
			if (GetFixture(fixtureIndex) is not WireRailHairpinFixture hairpin) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a hairpin.",
					nameof(fixtureIndex));
			}
			hairpin.SetProperties(SplineLength, _wireDiameter, endpoint,
				firstRailIndex, secondRailIndex, loopDiameter, leadLength, tangentLength,
				ringDensity, railOffset, rotation);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetElbowFixtureProperties(int fixtureIndex,
			WireRailEndpoint endpoint, int firstRailIndex, int secondRailIndex,
			float offset, float dropLength, float zAngle,
			IReadOnlyList<float> railOffsets)
		{
			if (GetFixture(fixtureIndex) is not WireRailElbowFixture elbow) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not an elbow.",
					nameof(fixtureIndex));
			}
			if (elbow.Endpoint != endpoint
				&& !AreEndpointRailsActive(endpoint, firstRailIndex, secondRailIndex)) {
				TryGetDefaultEndpointRailPair(endpoint, out firstRailIndex,
					out secondRailIndex);
			}
			elbow.SetProperties(SplineLength, _railCount, _wireDiameter, endpoint,
				firstRailIndex, secondRailIndex, offset, dropLength, zAngle,
				railOffsets);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
		}

		public bool AreEndpointRailsActive(WireRailEndpoint endpoint,
			int firstRailIndex, int secondRailIndex, float attachmentOffset = 0f)
		{
			if (_segments == null || _segments.Count == 0
				|| firstRailIndex == secondRailIndex) {
				return false;
			}
			// A Drop's rails attach at its offset inward from the endpoint, which may fall in a
			// different layout, so validate there rather than at the spline endpoint.
			int segmentIndex;
			if (attachmentOffset > 1e-5f) {
				var splineLength = SplineLength;
				var attachmentDistance = math.clamp(endpoint == WireRailEndpoint.Start
					? attachmentOffset : splineLength - attachmentOffset, 0f, splineLength);
				segmentIndex = WireRailSplineGeometry.GetLayoutIndexAtDistance(_segments,
					attachmentDistance, splineLength);
			} else {
				segmentIndex = GetEndpointSegmentIndex(endpoint);
			}
			if (segmentIndex < 0) {
				return false;
			}
			var segment = _segments[segmentIndex];
			return firstRailIndex >= 0 && secondRailIndex >= 0
				&& firstRailIndex < segment.RailCount
				&& secondRailIndex < segment.RailCount
				&& segment.IsRailActive(firstRailIndex)
				&& segment.IsRailActive(secondRailIndex);
		}

		private bool TryGetDefaultEndpointRailPair(WireRailEndpoint endpoint,
			out int firstRailIndex, out int secondRailIndex)
		{
			SynchronizeSegments();
			return TryGetDefaultEndpointRailPairFromSegments(endpoint,
				out firstRailIndex, out secondRailIndex);
		}

		private bool TryGetDefaultEndpointRailPairFromSegments(WireRailEndpoint endpoint,
			out int firstRailIndex, out int secondRailIndex)
		{
			firstRailIndex = 0;
			secondRailIndex = math.min(1, math.max(0, _railCount - 1));
			if (_segments == null || _segments.Count == 0) {
				return false;
			}
			var segmentIndex = GetEndpointSegmentIndex(endpoint);
			if (segmentIndex < 0) {
				return false;
			}
			var segment = _segments[segmentIndex];
			firstRailIndex = -1;
			for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
				if (!segment.IsRailActive(railIndex)) {
					continue;
				}
				if (firstRailIndex < 0) {
					firstRailIndex = railIndex;
					continue;
				}
				secondRailIndex = railIndex;
				return true;
			}
			firstRailIndex = 0;
			secondRailIndex = math.min(1, math.max(0, _railCount - 1));
			return false;
		}

		private int GetEndpointSegmentIndex(WireRailEndpoint endpoint)
		{
			if (_segments == null || _segments.Count == 0) {
				return -1;
			}
			var splineLength = SplineLength;
			return WireRailSplineGeometry.GetLayoutIndexAtDistance(_segments,
				endpoint == WireRailEndpoint.Start ? 0f : splineLength, splineLength);
		}

		public bool HasRailTrimConflict(WireRailEndpoint endpoint,
			int firstRailIndex, int secondRailIndex,
			WireRailFixture requestingFixture = null)
		{
			SynchronizeFixtures();
			return WireRailEndpointTrimUtility.HasRailTrimConflict(_fixtures, endpoint,
				firstRailIndex, secondRailIndex, requestingFixture);
		}

		public void SetRailTrimFixtureProperties(int fixtureIndex,
			WireRailEndpoint endpoint, IReadOnlyList<float> railOffsets)
		{
			if (GetFixture(fixtureIndex) is not WireRailTrimFixture railTrim) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a rail trim.",
					nameof(fixtureIndex));
			}
			railTrim.SetProperties(SplineLength, _railCount, endpoint, railOffsets);
			InvalidateColliderGeometry();
			RebuildRenderGeometry();
			MarkDirty();
		}

		/// <summary>
		/// Moves a support fixture along the route. End fittings are attached to an endpoint
		/// and have no route position of their own.
		/// </summary>
		public void SetFixtureDistance(int fixtureIndex, float distance)
		{
			var fixture = GetFixture(fixtureIndex);
			if (fixture is WireRailHairpinFixture or WireRailElbowFixture or WireRailTrimFixture) {
				throw new ArgumentException(
					$"Fixture {fixtureIndex + 1} is an end fitting and has no route position.",
					nameof(fixtureIndex));
			}
			fixture.SetDistance(distance, SplineLength);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetHairpinFixtureOffset(int fixtureIndex, float railOffset)
		{
			if (GetFixture(fixtureIndex) is not WireRailHairpinFixture hairpin) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a hairpin.",
					nameof(fixtureIndex));
			}
			SetHairpinFixtureProperties(fixtureIndex, hairpin.Endpoint, hairpin.FirstRailIndex,
				hairpin.SecondRailIndex, hairpin.LoopDiameter, hairpin.LeadLength,
				hairpin.TangentLength, hairpin.RingDensity, railOffset, hairpin.Rotation);
		}

		public void SetElbowFixtureOffset(int fixtureIndex, float offset)
		{
			if (GetFixture(fixtureIndex) is not WireRailElbowFixture elbow) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not an elbow.",
					nameof(fixtureIndex));
			}
			SetElbowFixtureProperties(fixtureIndex, elbow.Endpoint, elbow.FirstRailIndex,
				elbow.SecondRailIndex, offset, elbow.DropLength, elbow.ZAngle, elbow.RailOffsets);
		}

		public void SetFixtureEnabled(int fixtureIndex, bool enabled)
		{
			var fixture = GetFixture(fixtureIndex);
			if (fixture.Enabled == enabled) {
				return;
			}
			fixture.SetEnabled(enabled);
			// Disabling only hides the fixture's render mesh; the collider is left as-is.
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetFixtureSolderThreshold(int fixtureIndex, float solderThreshold)
		{
			if (solderThreshold < 0f) {
				throw new ArgumentOutOfRangeException(nameof(solderThreshold), solderThreshold,
					"Solder threshold cannot be negative.");
			}
			var fixture = GetFixture(fixtureIndex);
			if (Mathf.Approximately(fixture.SolderThreshold, solderThreshold)) {
				return;
			}
			fixture.SetSolderThreshold(solderThreshold);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetFixtureSolderSize(int fixtureIndex, float solderSize)
		{
			if (solderSize <= 0f) {
				throw new ArgumentOutOfRangeException(nameof(solderSize), solderSize,
					"Solder size must be positive.");
			}
			var fixture = GetFixture(fixtureIndex);
			if (Mathf.Approximately(fixture.SolderSize, solderSize)) {
				return;
			}
			fixture.SetSolderSize(solderSize);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void ApplyRingPropertiesToAll(int sourceFixtureIndex)
		{
			if (GetFixture(sourceFixtureIndex) is not WireRailRingFixture source) {
				throw new ArgumentException($"Fixture {sourceFixtureIndex + 1} is not a ring.",
					nameof(sourceFixtureIndex));
			}
			for (var fixtureIndex = 0; fixtureIndex < _fixtures.Count; fixtureIndex++) {
				if (fixtureIndex == sourceFixtureIndex
					|| _fixtures[fixtureIndex] is not WireRailRingFixture target) {
					continue;
				}
				target.SetProperties(target.Distance, SplineLength, source.Diameter,
					source.HasCutout, source.CutoutStartAngle, source.CutoutEndAngle,
					source.HasStraightSection, source.StraightStartAngle,
					source.StraightEndAngle, source.LateralOffset, source.VerticalOffset,
					source.Scale, source.RingDensity);
				target.SetSolderThreshold(source.SolderThreshold);
				target.SetSolderSize(source.SolderSize);
			}
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void SetWireCapBevelSize(float size)
		{
			_wireCapBevelSize = math.max(0f, size);
			RebuildRenderGeometry();
			MarkDirty();
		}

		public void MoveFixture(int fromIndex, int toIndex)
		{
			SynchronizeFixtures();
			if (fromIndex < 0 || fromIndex >= _fixtures.Count) {
				throw new ArgumentOutOfRangeException(nameof(fromIndex));
			}
			if (toIndex < 0 || toIndex >= _fixtures.Count) {
				throw new ArgumentOutOfRangeException(nameof(toIndex));
			}
			if (fromIndex == toIndex) {
				return;
			}
			var fixture = _fixtures[fromIndex];
			_fixtures.RemoveAt(fromIndex);
			_fixtures.Insert(toIndex, fixture);
			RebuildRenderGeometry();
			MarkDirty();
		}

		private WireRailFixture GetFixture(int fixtureIndex)
		{
			SynchronizeFixtures();
			if (fixtureIndex < 0 || fixtureIndex >= _fixtures.Count) {
				throw new ArgumentOutOfRangeException(nameof(fixtureIndex), fixtureIndex,
					$"The wire rail has {_fixtures.Count} fixture(s).");
			}
			return _fixtures[fixtureIndex];
		}

		private bool SynchronizeFixtures()
		{
			_fixtures ??= new List<WireRailFixture>();
			var changed = false;
			var spline = GetSplineContainerWithoutCreating()?.Spline;
			for (var fixtureIndex = _fixtures.Count - 1; fixtureIndex >= 0; fixtureIndex--) {
				var fixture = _fixtures[fixtureIndex];
				if (fixture == null) {
					_fixtures.RemoveAt(fixtureIndex);
					changed = true;
					continue;
				}
				if (fixture is WireRailRingFixture ring) {
					changed |= ring.EnsureRingInitialized(SplineLength);
					changed |= ring.SetDiameter(_wireDiameter);
					if (!ring.ScaleInitialized && spline != null && _segments != null
						&& _segments.Count > 0
						&& WireRailFixtureMeshGenerator.TryEvaluateRingProfile(spline,
							_segments, ring, out var profile)) {
						changed |= ring.EnsureScaleInitialized(profile.BaseRadius);
					}
				} else if (fixture is WireRailRungFixture rung) {
					changed |= rung.EnsureRungInitialized(SplineLength);
					changed |= rung.SetDiameter(_wireDiameter);
				} else if (fixture is WireRailCradleFixture cradle) {
					changed |= cradle.SetDiameter(_wireDiameter);
					changed |= cradle.EnsureCradleInitialized(SplineLength);
				} else if (fixture is WireRailStandFixture leg) {
					changed |= leg.EnsureStandInitialized(SplineLength);
					changed |= leg.SetDiameter(_wireDiameter);
				} else if (fixture is WireRailHairpinFixture hairpin) {
					changed |= hairpin.EnsureHairpinInitialized(SplineLength);
					changed |= hairpin.SetDiameter(_wireDiameter);
				} else if (fixture is WireRailElbowFixture elbow) {
					changed |= elbow.EnsureElbowInitialized(SplineLength, _railCount);
					changed |= elbow.SetDiameter(_wireDiameter);
					if (!elbow.RailPairInitialized) {
						var firstRailIndex = elbow.FirstRailIndex;
						var secondRailIndex = elbow.SecondRailIndex;
						var pairResolved = AreEndpointRailsActive(elbow.Endpoint,
							firstRailIndex, secondRailIndex);
						if (!pairResolved) {
							pairResolved = TryGetDefaultEndpointRailPairFromSegments(
								elbow.Endpoint, out firstRailIndex, out secondRailIndex);
						}
						if (pairResolved) {
							changed |= elbow.EnsureRailPairInitialized(firstRailIndex,
								secondRailIndex);
						}
					}
				} else if (fixture is WireRailTrimFixture railTrim) {
					changed |= railTrim.EnsureRailTrimInitialized(SplineLength, _railCount);
				} else {
					changed |= fixture.EnsureInitialized(SplineLength);
				}
			}
			return changed;
		}

		public bool SynchronizeSegments()
		{
			_segments ??= new List<WireRailSegment>();
			var changed = false;
			var migratingRailCount = !_railCountInitialized;
			for (var i = 0; i < _segments.Count; i++) {
				if (_segments[i] == null) {
					_segments[i] = new WireRailSegment();
					changed = true;
				} else if (_segments[i].RailCount == 0) {
					changed = true;
				}
				changed |= _segments[i].EnsureInitialized(_wireDiameter);
				changed |= _segments[i].SetAllWireDiameters(_wireDiameter);
			}

			if (_segments.Count == 0 && GetSplineSegmentCount() > 0) {
				_segments.Add(new WireRailSegment());
				changed |= _segments[0].EnsureInitialized(_wireDiameter);
				changed = true;
			}
			if (migratingRailCount) {
				_railCount = math.clamp(_segments.Count > 0
					? _segments.Max(segment => segment.RailCount) : 4, 1, 6);
				_railCountInitialized = true;
				changed = true;
			} else {
				var clampedRailCount = math.clamp(_railCount, 1, 6);
				if (_railCount != clampedRailCount) {
					_railCount = clampedRailCount;
					changed = true;
				}
			}
			foreach (var layout in _segments) {
				changed |= layout.ResizeRailCount(_railCount, _wireDiameter,
					!migratingRailCount, false);
			}
			var splineLength = SplineLength;
			// Older scenes stored one layout per spline curve and therefore had no explicit
			// distances. Preserve their shape once by placing those layouts at the old curve
			// boundaries; from then on the list is completely independent from spline knots.
			if (splineLength > 0f && _segments.Count > 1 && _segments.All(segment =>
					Mathf.Approximately(segment.Distance, 0f))) {
				var spline = GetSplineContainerWithoutCreating()?.Spline;
				var distance = 0f;
				for (var layoutIndex = 0; layoutIndex < _segments.Count; layoutIndex++) {
					_segments[layoutIndex].SetDistance(distance, splineLength);
					if (spline != null && layoutIndex < GetSplineSegmentCount()) {
						distance += spline.GetCurveLength(layoutIndex);
					}
				}
				changed = true;
			}
			if (_segments.Count > 0) {
				_segments[0].SetDistance(0f, splineLength);
				for (var layoutIndex = 1; layoutIndex < _segments.Count; layoutIndex++) {
					var clampedDistance = math.clamp(_segments[layoutIndex].Distance, 0f,
						splineLength);
					if (!Mathf.Approximately(_segments[layoutIndex].Distance, clampedDistance)) {
						_segments[layoutIndex].SetDistance(clampedDistance, splineLength);
						changed = true;
					}
				}
			}
			changed |= SynchronizeLayoutDisplayOrder();
			changed |= SortLayoutsByDistance();
			changed |= SynchronizeSegmentConnections();
			changed |= SynchronizeFixtures();

			if (changed) {
				// Scene previews are version-keyed. Any synchronization mutation must
				// participate in the same invalidation contract as an explicit edit.
				InvalidateGeneratedGeometry();
				MarkDirty();
			}
			return changed;
		}

		public int AddLayout(float distance)
		{
			SynchronizeSegments();
			if (_segments.Count == 0) {
				throw new InvalidOperationException("A wire layout needs a valid spline.");
			}
			distance = math.clamp(distance, 0f, SplineLength);
			var insertIndex = GetLayoutInsertIndex(distance);
			var source = _segments[insertIndex - 1];
			var layout = source.Clone(_wireDiameter);
			layout.SetDistance(distance, SplineLength);
			source.ResetConnection();
			_segments.Insert(insertIndex, layout);
			InsertLayoutDisplayIndex(insertIndex, _layoutDisplayOrder.Count);
			SynchronizeSegmentConnections();
			RebuildGeneratedMeshes();
			MarkDirty();
			return insertIndex;
		}

		public int DuplicateLayout(int sourceLayoutIndex, float distance)
		{
			SynchronizeSegments();
			if (sourceLayoutIndex < 0 || sourceLayoutIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(sourceLayoutIndex));
			}
			distance = math.clamp(distance, 0f, SplineLength);
			var sourceDisplayIndex = _layoutDisplayOrder.IndexOf(sourceLayoutIndex);
			var insertIndex = GetLayoutInsertIndex(distance);
			var source = _segments[sourceLayoutIndex];
			var layout = source.Clone(_wireDiameter);
			layout.SetDistance(distance, SplineLength);
			if (sourceLayoutIndex == insertIndex - 1) {
				source.ResetConnection();
			} else {
				// The physically last layout is duplicated before itself. Its predecessor
				// keeps the authored transition into the identical copy, while the copy
				// receives a default transition into the original last layout.
				layout.ResetConnection();
			}
			_segments.Insert(insertIndex, layout);
			InsertLayoutDisplayIndex(insertIndex, sourceDisplayIndex + 1);
			SynchronizeSegmentConnections();
			RebuildGeneratedMeshes();
			MarkDirty();
			return insertIndex;
		}

		public float GetSuggestedLayoutDistance(int sourceLayoutIndex = -1)
		{
			SynchronizeSegments();
			if (sourceLayoutIndex < -1 || sourceLayoutIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(sourceLayoutIndex));
			}
			if (sourceLayoutIndex >= 0) {
				// Halfway to the next layout, or halfway to the end of the route when the
				// source is the last one, so a duplicate always lands in the source's span.
				var nextDistance = sourceLayoutIndex + 1 < _segments.Count
					? _segments[sourceLayoutIndex + 1].Distance
					: SplineLength;
				return (_segments[sourceLayoutIndex].Distance + nextDistance) * 0.5f;
			}
			if (_segments.Count > 1) {
				return (_segments[^2].Distance + _segments[^1].Distance) * 0.5f;
			}
			return _segments.Count == 1
				? (_segments[0].Distance + SplineLength) * 0.5f
				: 0f;
		}

		public int GetLayoutDisplayIndex(int layoutIndex)
		{
			if (_segments == null || layoutIndex < 0 || layoutIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(layoutIndex));
			}
			var displayIndex = _layoutDisplayOrder?.IndexOf(layoutIndex) ?? -1;
			return displayIndex >= 0 ? displayIndex : layoutIndex;
		}

		public void RemoveLayout(int layoutIndex)
		{
			SynchronizeSegments();
			if (_segments.Count <= 1) {
				throw new InvalidOperationException("A wire rail needs at least one layout.");
			}
			if (layoutIndex < 0 || layoutIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(layoutIndex));
			}
			var previousIndex = layoutIndex - 1;
			if (previousIndex >= 0) {
				_segments[previousIndex].CopyConnectionFrom(_segments[layoutIndex]);
			}
			RemoveLayoutDisplayIndex(layoutIndex);
			_segments.RemoveAt(layoutIndex);
			if (_segments.Count > 0) {
				_segments[0].SetDistance(0f, SplineLength);
			}
			SynchronizeSegmentConnections();
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		/// <summary>
		/// Moves a layout to any distance along the route. The physical order follows the
		/// distances, so a layout moved past its neighbor swaps places with it. Layout 0 is
		/// pinned to the route start.
		/// </summary>
		/// <returns>The layout's physical index after the move.</returns>
		public int SetLayoutDistance(int layoutIndex, float distance)
		{
			var layout = GetSegment(layoutIndex);
			distance = layoutIndex == 0 ? 0f : math.clamp(distance, 0f, SplineLength);
			layout.SetDistance(distance, SplineLength);
			SortLayoutsByDistance();
			SynchronizeSegmentConnections();
			RebuildGeneratedMeshes();
			MarkDirty();
			return _segments.IndexOf(layout);
		}

		/// <summary>
		/// Keeps the physical layout list ordered by distance, layout 0 first. Layouts at the
		/// same distance keep their relative order. The display order is remapped so every
		/// "Layout N" keeps pointing at the same layout.
		/// </summary>
		/// <returns>True when the physical order changed.</returns>
		private bool SortLayoutsByDistance()
		{
			if (_segments.Count < 3) {
				return false;
			}
			var sorted = true;
			for (var layoutIndex = 2; layoutIndex < _segments.Count; layoutIndex++) {
				if (_segments[layoutIndex].Distance < _segments[layoutIndex - 1].Distance) {
					sorted = false;
					break;
				}
			}
			if (sorted) {
				return false;
			}
			var oldOrder = new List<int>(_segments.Count) { 0 };
			oldOrder.AddRange(Enumerable.Range(1, _segments.Count - 1)
				.OrderBy(layoutIndex => _segments[layoutIndex].Distance));
			var newIndexOfOld = new int[_segments.Count];
			var reordered = new List<WireRailSegment>(_segments.Count);
			for (var newIndex = 0; newIndex < oldOrder.Count; newIndex++) {
				newIndexOfOld[oldOrder[newIndex]] = newIndex;
				reordered.Add(_segments[oldOrder[newIndex]]);
			}
			_segments.Clear();
			_segments.AddRange(reordered);
			if (_layoutDisplayOrder != null) {
				for (var displayIndex = 0; displayIndex < _layoutDisplayOrder.Count; displayIndex++) {
					var oldIndex = _layoutDisplayOrder[displayIndex];
					if (oldIndex >= 0 && oldIndex < newIndexOfOld.Length) {
						_layoutDisplayOrder[displayIndex] = newIndexOfOld[oldIndex];
					}
				}
			}
			return true;
		}

		public void MoveLayout(int fromIndex, int toIndex)
		{
			SynchronizeSegments();
			if (fromIndex < 0 || fromIndex >= _layoutDisplayOrder.Count) {
				throw new ArgumentOutOfRangeException(nameof(fromIndex));
			}
			if (toIndex < 0 || toIndex >= _layoutDisplayOrder.Count) {
				throw new ArgumentOutOfRangeException(nameof(toIndex));
			}
			if (fromIndex == toIndex) {
				return;
			}
			var layoutIndex = _layoutDisplayOrder[fromIndex];
			_layoutDisplayOrder.RemoveAt(fromIndex);
			_layoutDisplayOrder.Insert(toIndex, layoutIndex);
			MarkDirty();
		}

		private int GetLayoutInsertIndex(float distance)
		{
			var insertIndex = 1;
			while (insertIndex < _segments.Count
				&& _segments[insertIndex].Distance <= distance) {
				insertIndex++;
			}
			return insertIndex;
		}

		private bool SynchronizeLayoutDisplayOrder()
		{
			_layoutDisplayOrder ??= new List<int>();
			var valid = _layoutDisplayOrder.Count == _segments.Count;
			var seen = valid ? new bool[_segments.Count] : null;
			if (valid) {
				foreach (var layoutIndex in _layoutDisplayOrder) {
					if (layoutIndex < 0 || layoutIndex >= _segments.Count
						|| seen[layoutIndex]) {
						valid = false;
						break;
					}
					seen[layoutIndex] = true;
				}
			}
			if (valid) {
				return false;
			}
			_layoutDisplayOrder.Clear();
			for (var layoutIndex = 0; layoutIndex < _segments.Count; layoutIndex++) {
				_layoutDisplayOrder.Add(layoutIndex);
			}
			return true;
		}

		private void InsertLayoutDisplayIndex(int layoutIndex, int displayIndex)
		{
			for (var index = 0; index < _layoutDisplayOrder.Count; index++) {
				if (_layoutDisplayOrder[index] >= layoutIndex) {
					_layoutDisplayOrder[index]++;
				}
			}
			_layoutDisplayOrder.Insert(math.clamp(displayIndex, 0,
				_layoutDisplayOrder.Count), layoutIndex);
		}

		private void RemoveLayoutDisplayIndex(int layoutIndex)
		{
			_layoutDisplayOrder.Remove(layoutIndex);
			for (var index = 0; index < _layoutDisplayOrder.Count; index++) {
				if (_layoutDisplayOrder[index] > layoutIndex) {
					_layoutDisplayOrder[index]--;
				}
			}
		}

		private bool SynchronizeSegmentConnections()
		{
			if (_segments == null) {
				return false;
			}
			var changed = false;
			for (var segmentIndex = 0; segmentIndex < _segments.Count; segmentIndex++) {
				var nextSegmentIndex = GetNextSegmentIndex(segmentIndex);
				var wireCount = nextSegmentIndex < 0 ? 0 : _railCount;
				changed |= _segments[segmentIndex].EnsureConnectionInitialized(wireCount);
			}
			return changed;
		}

		private WireRailSegment GetSegment(int segmentIndex)
		{
			SynchronizeSegments();
			if (segmentIndex < 0 || segmentIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(segmentIndex), segmentIndex,
					$"The spline has {_segments.Count} segment(s).");
			}
			return _segments[segmentIndex];
		}

		private void OnSplineChanged(Spline spline, int knotIndex,
			SplineModification modification)
		{
			var container = GetSplineContainerWithoutCreating();
			if (!container || !ReferenceEquals(spline, container.Spline)) {
				return;
			}

			RebuildGeneratedMeshes();
			MarkDirty();
		}

		private void OnSplineCollectionChanged(SplineContainer container, int _)
		{
			if (container == GetSplineContainerWithoutCreating()) {
				RebuildGeneratedMeshes();
			}
		}

		private int GetSplineSegmentCount()
		{
			var container = GetSplineContainerWithoutCreating();
			var spline = container ? container.Spline : null;
			if (spline == null || spline.Count == 0) {
				return 0;
			}
			return math.max(0, spline.Count - (spline.Closed ? 0 : 1));
		}

		public void RebuildGeneratedMeshes()
		{
			InvalidateGeneratedGeometry();
#if UNITY_EDITOR
			if (!Application.isPlaying
				&& (Event.current != null || DeferEditorRebuildsForTesting)) {
				ScheduleEditorRebuild(false);
				return;
			}
#endif
			RebuildGeneratedMeshesImmediately();
		}

		public void RebuildRenderGeometry()
		{
			InvalidateRenderGeometry();
#if UNITY_EDITOR
			if (!Application.isPlaying
				&& (Event.current != null || DeferEditorRebuildsForTesting)) {
				ScheduleEditorRebuild(false);
				return;
			}
#endif
			RebuildGeneratedMeshesImmediately();
		}

		private void RebuildGeneratedMeshesImmediately()
		{
			if (_rebuildingGeneratedMeshes) {
				return;
			}
			_rebuildingGeneratedMeshes = true;
			try {
				var container = GetSplineContainerWithoutCreating();
				if (!container) {
					_generationError = "The generated Wire Rail Spline child is missing.";
					return;
				}
				using (SynchronizeSegmentsMarker.Auto()) {
					SynchronizeSegments();
				}
				using (RenderMeshMarker.Auto()) {
					// Only enabled fixtures contribute to the render mesh (own geometry, rail
					// trims, end fitting and solder). Colliders still use the full list.
					_enabledFixtures.Clear();
					foreach (var fixture in _fixtures) {
						if (fixture != null && fixture.Enabled) {
							_enabledFixtures.Add(fixture);
						}
					}
					_renderMesh = WireRailRenderMeshGenerator.Generate(container.Spline, _segments,
						_enabledFixtures, _wireCapBevelSize, _renderSamplesPerSegment, _radialSegments,
						_renderMesh, _renderSegmentIndexRanges, _enabledFixtureIndexRanges);
					_renderMeshGenerationCount++;
					// The generator only saw the enabled fixtures; spread its ranges back over
					// the full list so callers can index them by fixture.
					_renderFixtureIndexRanges.Clear();
					var enabledRangeIndex = 0;
					foreach (var fixture in _fixtures) {
						var range = int2.zero;
						if (fixture != null && fixture.Enabled) {
							if (enabledRangeIndex < _enabledFixtureIndexRanges.Count) {
								range = _enabledFixtureIndexRanges[enabledRangeIndex];
							}
							enabledRangeIndex++;
						}
						_renderFixtureIndexRanges.Add(range);
					}
				}

				var meshFilter = GetOrAddComponent<MeshFilter>(container.gameObject);
				var meshRenderer = GetOrAddComponent<MeshRenderer>(container.gameObject);
				if (meshFilter.sharedMesh != _renderMesh) {
					meshFilter.sharedMesh = _renderMesh;
				}
				AssignRenderMaterial(meshRenderer);
			} finally {
				_rebuildingGeneratedMeshes = false;
			}
		}

		public void RebuildColliderMesh()
		{
			InvalidateColliderGeometry();
			EnsureColliderMesh();
		}

		public void SetShowColliderPreview(bool show)
		{
			if (_showColliderPreview == show) {
				return;
			}
			_showColliderPreview = show;
			MarkDirty();
		}

		public void InvalidateColliderGeometry()
		{
			_colliderGeometryDirty = true;
			_collidersDirty = true;
			_colliderTopologyRetryCount = 0;
			_generationError = null;
			unchecked {
				_colliderGeometryVersion++;
			}
		}

		private void InvalidateGeneratedGeometry()
		{
			InvalidateColliderGeometry();
			InvalidateRenderGeometry();
		}

		private void InvalidateRenderGeometry()
		{
			unchecked {
				_renderGeometryVersion++;
			}
		}

		private bool EnsureColliderMesh()
		{
			if (!_colliderGeometryDirty) {
				return _colliderMesh && _colliderMesh.vertexCount > 0;
			}
			var container = GetSplineContainerWithoutCreating();
			if (!container || container.Spline == null) {
				_generationError = "The generated Wire Rail Spline child is missing.";
				return false;
			}
			using (SynchronizeSegmentsMarker.Auto()) {
				SynchronizeSegments();
			}
			using (ColliderMeshMarker.Auto()) {
				if (!WireRailColliderMeshGenerator.TryGenerate(container.Spline, _segments,
						_fixtures, _referenceBallDiameter, _colliderSamplesPerSegment,
						_colliderMesh, out _colliderMesh, out _colliderEdgeVertices,
						out _colliderTopologyRetryCount,
						out _generationError)) {
					if (_colliderMesh) {
						_colliderMesh.Clear(false);
					} else {
						_colliderMesh = null;
					}
					_colliderEdgeVertices = Array.Empty<Vector3>();
				}
			}
			_colliderGeometryDirty = false;
			return _colliderMesh && _colliderMesh.vertexCount > 0;
		}

		public SplineContainer EnsureSplineContainerExists()
			=> EnsureSplineContainer();

		private void AssignRenderMaterial(MeshRenderer renderer)
		{
			var pipeline = GraphicsSettings.currentRenderPipeline;
			var material = _renderMaterial
				? _renderMaterial
				: pipeline
					? pipeline.defaultMaterial
					: GetBuiltinDefaultMaterial();
			if (renderer.sharedMaterial != material) {
				renderer.sharedMaterial = material;
			}
		}

		private static Material GetBuiltinDefaultMaterial()
		{
			if (!_builtinDefaultMaterial) {
				_builtinDefaultMaterial = Resources.Load<Material>(
					"Materials/Table Opaque (Builtin)");
			}
			return _builtinDefaultMaterial;
		}

		private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
		{
			var component = gameObject.GetComponent<T>();
			return component ? component : gameObject.AddComponent<T>();
		}

		private SplineContainer EnsureSplineContainer()
		{
			var existing = GetSplineContainerWithoutCreating();
			if (existing) {
				return existing;
			}

			var splineObject = new GameObject(SplineObjectName);
			splineObject.transform.SetParent(transform, false);
			splineObject.transform.localPosition = Vector3.zero;
			splineObject.transform.localRotation = ((Matrix4x4)Physics.VpxToWorld).rotation;
			splineObject.transform.localScale = Physics.ScaleInvVector;
			splineObject.AddComponent<WireRailSplineComponent>();
			splineObject.transform.hideFlags |= HideFlags.NotEditable;
			_splineContainer = splineObject.AddComponent<SplineContainer>();
			_splineContainer.Spline = CreateDefaultSpline();

#if UNITY_EDITOR
			if (!Application.isPlaying) {
				if (!Undo.isProcessing) {
					Undo.RegisterCreatedObjectUndo(splineObject, "Create Wire Rail Spline");
				}
				EditorUtility.SetDirty(this);
			}
#endif
			return _splineContainer;
		}

		private SplineContainer GetSplineContainerWithoutCreating()
		{
			if (_splineContainer && _splineContainer.transform.parent == transform) {
				return _splineContainer;
			}

			// Prefer the marked child; fall back to the legacy name for scenes saved before
			// the marker existed, and harden whatever we find.
			_splineContainer = null;
			for (var i = 0; i < transform.childCount; i++) {
				var candidate = transform.GetChild(i);
				if (candidate.TryGetComponent<WireRailSplineComponent>(out _)
					&& candidate.TryGetComponent<SplineContainer>(out var marked)) {
					_splineContainer = marked;
					break;
				}
			}
			if (!_splineContainer) {
				var child = transform.Find(SplineObjectName);
				_splineContainer = child ? child.GetComponent<SplineContainer>() : null;
			}
			if (_splineContainer) {
				HardenSplineChild(_splineContainer.gameObject);
			}
			return _splineContainer;
		}

		/// <summary>
		/// Makes sure the spline child carries its marker and that its transform, which holds
		/// the VPX-to-world conversion, is locked in the inspector. Idempotent.
		/// </summary>
		private static void HardenSplineChild(GameObject splineObject)
		{
			if (!splineObject.TryGetComponent<WireRailSplineComponent>(out _)) {
#if UNITY_EDITOR
				if (!Application.isPlaying && !Undo.isProcessing) {
					Undo.AddComponent<WireRailSplineComponent>(splineObject);
				} else {
					splineObject.AddComponent<WireRailSplineComponent>();
				}
#else
				splineObject.AddComponent<WireRailSplineComponent>();
#endif
			}
			if ((splineObject.transform.hideFlags & HideFlags.NotEditable) == 0) {
				splineObject.transform.hideFlags |= HideFlags.NotEditable;
#if UNITY_EDITOR
				if (!Application.isPlaying) {
					EditorUtility.SetDirty(splineObject.transform);
				}
#endif
			}
		}

		private static Spline CreateDefaultSpline()
		{
			var spline = new Spline(2, false);
			var rotation = quaternion.RotateX(math.PI * 0.5f);
			var start = new BezierKnot(new float3(0f, 0f, 0f)) { Rotation = rotation };
			var end = new BezierKnot(new float3(0f, 500f, 0f)) { Rotation = rotation };
			spline.Add(start, TangentMode.AutoSmooth);
			spline.Add(end, TangentMode.AutoSmooth);
			return spline;
		}

		public int ItemId => UnityObjectId.Get(gameObject);
		public bool IsKinematic => false;

		public bool CollidersDirty {
			set => _collidersDirty = value;
		}

		public float PhysicsElasticity {
			get => _elasticity;
			set {
				_elasticity = value;
				_collidersDirty = true;
			}
		}

		public float PhysicsElasticityFalloff {
			get => _elasticityFalloff;
			set {
				_elasticityFalloff = value;
				_collidersDirty = true;
			}
		}

		public float PhysicsFriction {
			get => _friction;
			set {
				_friction = value;
				_collidersDirty = true;
			}
		}

		public float PhysicsScatter {
			get => _scatter;
			set {
				_scatter = value;
				_collidersDirty = true;
			}
		}

		public bool PhysicsOverwrite {
			get => _overwritePhysics;
			set {
				_overwritePhysics = value;
				_collidersDirty = true;
			}
		}

		public PhysicsMaterialAsset PhysicsMaterialReference {
			get => _physicsMaterial;
			set {
				_physicsMaterial = value;
				_collidersDirty = true;
			}
		}

		public PhysicsMaterialAsset TerminalPhysicsMaterialReference {
			get => _terminalPhysicsMaterial;
			set {
				_terminalPhysicsMaterial = value;
				_collidersDirty = true;
			}
		}

		public float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
		{
			var container = GetSplineContainerWithoutCreating();
			if (!container) {
				return Physics.GetLocalToPlayfieldMatrixInVpx(transform.localToWorldMatrix,
					worldToPlayfield);
			}

			// Collider vertices remain in VPX units, but are local to the spline child
			// that also positions the generated render mesh. Convert that child space
			// directly to playfield VPX space without applying VpxToWorld a second time.
			return math.mul(Physics.WorldToVpx, math.mul(worldToPlayfield,
				(float4x4)container.transform.localToWorldMatrix));
		}

		public void OnTransformationChanged(float4x4 currTransformationMatrix)
		{
			_collidersDirty = true;
		}

		void ICollidableComponent.GetColliders(Player player, PhysicsEngine physicsEngine,
			ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			if (!EnsureColliderMesh()) {
				return;
			}
			var material = !_overwritePhysics && _physicsMaterial
				? new PhysicsMaterialData {
					Elasticity = _physicsMaterial.Elasticity,
					ElasticityFalloff = _physicsMaterial.ElasticityFalloff,
					Friction = _physicsMaterial.Friction,
					ScatterAngleRad = _physicsMaterial.ScatterAngle,
				}
				: new PhysicsMaterialData {
					Elasticity = _elasticity,
					ElasticityFalloff = _elasticityFalloff,
					Friction = _friction,
					ScatterAngleRad = math.radians(_scatter),
				};
			var info = new ColliderInfo {
				Id = -1,
				ItemId = ItemId,
				ItemType = ItemType.MetalWireGuide,
				Material = material,
				HitThreshold = 0f,
				FireEvents = false,
			};
			using var vertices = new NativeArray<Vector3>(_colliderMesh.vertices,
				Allocator.TempJob);
			using var channelIndices = new NativeArray<int>(_colliderMesh.GetIndices(0),
				Allocator.TempJob);
			ColliderUtils.GenerateCollidersFromMesh(in vertices, in channelIndices,
				translateWithinPlayfieldMatrix, info, ref colliders, true);
			if (_colliderMesh.subMeshCount > 1 && _colliderMesh.GetIndexCount(1) > 0) {
				var terminalInfo = info;
				if (_terminalPhysicsMaterial) {
					terminalInfo.Material = new PhysicsMaterialData {
						Elasticity = _terminalPhysicsMaterial.Elasticity,
						ElasticityFalloff = _terminalPhysicsMaterial.ElasticityFalloff,
						Friction = _terminalPhysicsMaterial.Friction,
						ScatterAngleRad = _terminalPhysicsMaterial.ScatterAngle,
					};
				}
				using var terminalIndices = new NativeArray<int>(_colliderMesh.GetIndices(1),
					Allocator.TempJob);
				ColliderUtils.GenerateCollidersFromMesh(in vertices, in terminalIndices,
					translateWithinPlayfieldMatrix, terminalInfo, ref colliders, true);
			}

			var points = new HashSet<Vector3>();
			for (var i = 0; i + 1 < _colliderEdgeVertices.Length; i += 2) {
				var start = _colliderEdgeVertices[i];
				var end = _colliderEdgeVertices[i + 1];
				colliders.Add(new Line3DCollider(start, end, info),
					translateWithinPlayfieldMatrix);
				points.Add(start);
				points.Add(end);
			}
			foreach (var point in points) {
				colliders.Add(new PointCollider(point, info), translateWithinPlayfieldMatrix);
			}
			_collidersDirty = false;
		}

		bool ICollidableComponent.IsCollidable
			=> isActiveAndEnabled && EnsureColliderMesh();

		private void OnDestroy()
			=> DestroyGeneratedMeshes();

		private void DestroyGeneratedMeshes()
		{
			DestroyGeneratedMesh(_renderMesh);
			DestroyGeneratedMesh(_colliderMesh);
			_renderMesh = null;
			_colliderMesh = null;
			_colliderEdgeVertices = Array.Empty<Vector3>();
			_collidersDirty = true;
			_colliderGeometryDirty = true;
		}

		private static void DestroyGeneratedMesh(Mesh mesh)
		{
			if (!mesh) {
				return;
			}
			if (Application.isPlaying) {
				Destroy(mesh);
			} else {
				DestroyImmediate(mesh);
			}
		}

		private void MarkDirty()
		{
#if UNITY_EDITOR
			if (!Application.isPlaying) {
				EditorUtility.SetDirty(this);
				PrefabUtility.RecordPrefabInstancePropertyModifications(this);
			}
#endif
		}
	}
}
