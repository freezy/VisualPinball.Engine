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
using UnityEngine;
using UnityEngine.Rendering;
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

	/// <summary>
	/// Creates useful starting positions for wire-rail centerlines. All values are in VPX units
	/// and describe the X/Z cross-section around a route whose initial direction is +Y.
	/// </summary>
	public static class WireRailLayout
	{
		public const float ReferenceBallDiameter = 50f;
		public const float ReferenceWireDiameter = 8f;
		public const float BottomRailSpacing = 38f;
		public const float MiddleRailHeight = 44f;
		public const float TopRailHeight = 52f;

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

			var halfSpacing = BottomRailSpacing * 0.5f;
			var offsets = new List<Vector2>(railCount) {
				new(-halfSpacing, 0f),
				new(halfSpacing, 0f),
			};

			if (railCount == 3) {
				offsets.Add(new Vector2(
					thirdRailSide == WireRailThirdRailSide.Left ? -halfSpacing : halfSpacing,
					MiddleRailHeight));
				return offsets.ToArray();
			}

			if (railCount >= 4) {
				offsets.Add(new Vector2(-halfSpacing, MiddleRailHeight));
				offsets.Add(new Vector2(halfSpacing, MiddleRailHeight));
			}

			var topRailCount = railCount - 4;
			for (var i = 0; i < topRailCount; i++) {
				var x = topRailCount == 1
					? 0f
					: math.lerp(-halfSpacing, halfSpacing, i / (float)(topRailCount - 1));
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
		[SerializeField, Min(0f)] private float _distance;

		public float Distance => _distance;

		internal bool EnsureInitialized(float splineLength)
		{
			var clampedDistance = math.clamp(_distance, 0f, math.max(0f, splineLength));
			if (Mathf.Approximately(_distance, clampedDistance)) {
				return false;
			}
			_distance = clampedDistance;
			return true;
		}

		internal void SetDistance(float distance, float splineLength)
		{
			_distance = math.clamp(distance, 0f, math.max(0f, splineLength));
		}
	}

	[Serializable]
	public sealed class WireRailBraceFixture : WireRailFixture
	{
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

		internal bool EnsureBraceInitialized(float splineLength)
		{
			var changed = EnsureInitialized(splineLength);
			var diameter = math.max(0.1f, _diameter);
			var startAngle = math.clamp(_cutoutStartAngle, 0f, 360f);
			var endAngle = math.clamp(_cutoutEndAngle, 0f, 360f);
			var straightStartAngle = math.clamp(_straightStartAngle, 0f, 360f);
			var straightEndAngle = math.clamp(_straightEndAngle, 0f, 360f);
			var scale = math.max(0.1f, _scale);
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
			float lateralOffset, float verticalOffset, float scale)
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

	public readonly struct WireRailBraceCrossSection
	{
		public readonly Vector2 CenterOffset;
		public readonly float BaseRadius;
		public readonly float Radius;

		internal WireRailBraceCrossSection(float2 centerOffset, float baseRadius,
			float radius)
		{
			CenterOffset = new Vector2(centerOffset.x, centerOffset.y);
			BaseRadius = baseRadius;
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

		[SerializeField] private SplineContainer _splineContainer;
		[SerializeField] private List<WireRailSegment> _segments = new();
		[SerializeReference] private List<WireRailFixture> _fixtures = new();
		[SerializeField, Range(1, 6)] private int _railCount = 4;
		[SerializeField, HideInInspector] private bool _railCountInitialized;
		[SerializeField, Min(0.1f)] private float _wireDiameter = WireRailLayout.ReferenceWireDiameter;
		[SerializeField, Min(0f), FormerlySerializedAs("_braceCapBevelSize")]
		private float _wireCapBevelSize;
		[SerializeField, Range(6, 16)] private int _radialSegments = 8;
		[SerializeField, Range(2, 64)] private int _renderSamplesPerSegment = 16;
		[SerializeField] private Material _renderMaterial;
		[SerializeField, Min(1f)] private float _referenceBallDiameter = WireRailLayout.ReferenceBallDiameter;
		[SerializeField, Range(2, 32)] private int _colliderSamplesPerSegment = 8;
		[SerializeField] private bool _showColliderPreview;
		[SerializeReference] private PhysicsMaterialAsset _physicsMaterial;
		[SerializeField] private bool _overwritePhysics = true;
		[SerializeField, Min(0f)] private float _elasticity = 0.3f;
		[SerializeField, Min(0f)] private float _elasticityFalloff = 0.5f;
		[SerializeField, Min(0f)] private float _friction = 0.3f;
		[SerializeField, Range(-90f, 90f)] private float _scatter;
		[NonSerialized] private Mesh _renderMesh;
		[NonSerialized] private Mesh _colliderMesh;
		[NonSerialized] private Vector3[] _colliderEdgeVertices = Array.Empty<Vector3>();
		[NonSerialized] private bool _rebuildingGeneratedMeshes;
		[NonSerialized] private bool _collidersDirty = true;
		[NonSerialized] private string _generationError;
#if UNITY_EDITOR
		[NonSerialized] private bool _validationRebuildScheduled;
		[NonSerialized] private int _generatedInputHash;
#endif

		public SplineContainer SplineContainer => GetSplineContainerWithoutCreating();
		public IReadOnlyList<WireRailSegment> Segments => _segments;
		public IReadOnlyList<WireRailSegment> Layouts => _segments;
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
		public Mesh ColliderMesh => _colliderMesh;

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
			if (_validationRebuildScheduled) {
				EditorApplication.delayCall -= RebuildAfterValidation;
				_validationRebuildScheduled = false;
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
			if (!_validationRebuildScheduled) {
				_validationRebuildScheduled = true;
				EditorApplication.delayCall += RebuildAfterValidation;
			}
#endif
		}

#if UNITY_EDITOR
		private void RebuildAfterValidation()
		{
			_validationRebuildScheduled = false;
			if (!this || !GetSplineContainerWithoutCreating()) {
				return;
			}
			SynchronizeSegments();
			RebuildGeneratedMeshes();
		}

		private void OnUndoRedo()
		{
			if (!this || !GetSplineContainerWithoutCreating()) {
				return;
			}
			SynchronizeSegments();
			if (_renderMesh && _generatedInputHash == ComputeGenerationInputHash()) {
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
						$"Layout {segmentIndex + 1} has {segment.RailCount} wire(s).");
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
			foreach (var brace in _fixtures.OfType<WireRailBraceFixture>()) {
				brace.SetDiameter(diameter);
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
						$"Layout {sourceSegmentIndex + 1} has {sourceSegment.RailCount} wire(s).");
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

		public int AddBraceFixture(float distance)
		{
			_fixtures ??= new List<WireRailFixture>();
			var brace = new WireRailBraceFixture();
			brace.SetProperties(distance, SplineLength, _wireDiameter, false,
				WireRailBraceFixture.DefaultCutoutStartAngle,
				WireRailBraceFixture.DefaultCutoutEndAngle, false,
				WireRailBraceFixture.DefaultStraightStartAngle,
				WireRailBraceFixture.DefaultStraightEndAngle, 0f, 0f, 1f);
			_fixtures.Add(brace);
			RebuildGeneratedMeshes();
			MarkDirty();
			return _fixtures.Count - 1;
		}

		public bool TryGetBraceCrossSection(int fixtureIndex,
			out WireRailBraceCrossSection crossSection)
		{
			crossSection = default;
			if (_fixtures == null || fixtureIndex < 0 || fixtureIndex >= _fixtures.Count
				|| _fixtures[fixtureIndex] is not WireRailBraceFixture brace
				|| !_splineContainer || _splineContainer.Spline == null
				|| !WireRailFixtureMeshGenerator.TryEvaluateBraceProfile(
					_splineContainer.Spline, _segments, brace, out var profile)) {
				return false;
			}
			crossSection = new WireRailBraceCrossSection(profile.CenterOffset,
				profile.BaseRadius, profile.Radius);
			return true;
		}

		public void RemoveFixture(int fixtureIndex)
		{
			GetFixture(fixtureIndex);
			_fixtures.RemoveAt(fixtureIndex);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public int DuplicateBraceFixture(int fixtureIndex)
		{
			if (GetFixture(fixtureIndex) is not WireRailBraceFixture source) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a brace.",
					nameof(fixtureIndex));
			}
			var duplicate = new WireRailBraceFixture();
			duplicate.SetProperties(source.Distance, SplineLength, _wireDiameter,
				source.HasCutout, source.CutoutStartAngle, source.CutoutEndAngle,
				source.HasStraightSection, source.StraightStartAngle,
				source.StraightEndAngle, source.LateralOffset, source.VerticalOffset,
				source.Scale);
			var duplicateIndex = fixtureIndex + 1;
			_fixtures.Insert(duplicateIndex, duplicate);
			RebuildGeneratedMeshes();
			MarkDirty();
			return duplicateIndex;
		}

		public void SetBraceFixtureProperties(int fixtureIndex, float distance,
			bool hasCutout, float cutoutStartAngle, float cutoutEndAngle,
			bool hasStraightSection = false, float straightStartAngle =
				WireRailBraceFixture.DefaultStraightStartAngle, float straightEndAngle =
				WireRailBraceFixture.DefaultStraightEndAngle, float lateralOffset = 0f,
			float verticalOffset = 0f, float scale = 1f)
		{
			if (GetFixture(fixtureIndex) is not WireRailBraceFixture brace) {
				throw new ArgumentException($"Fixture {fixtureIndex + 1} is not a brace.",
					nameof(fixtureIndex));
			}
			brace.SetProperties(distance, SplineLength, _wireDiameter, hasCutout,
				cutoutStartAngle, cutoutEndAngle, hasStraightSection, straightStartAngle,
				straightEndAngle, lateralOffset, verticalOffset, scale);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void ApplyBracePropertiesToAll(int sourceFixtureIndex)
		{
			if (GetFixture(sourceFixtureIndex) is not WireRailBraceFixture source) {
				throw new ArgumentException($"Fixture {sourceFixtureIndex + 1} is not a brace.",
					nameof(sourceFixtureIndex));
			}
			for (var fixtureIndex = 0; fixtureIndex < _fixtures.Count; fixtureIndex++) {
				if (fixtureIndex == sourceFixtureIndex
					|| _fixtures[fixtureIndex] is not WireRailBraceFixture target) {
					continue;
				}
				target.SetProperties(target.Distance, SplineLength, source.Diameter,
					source.HasCutout, source.CutoutStartAngle, source.CutoutEndAngle,
					source.HasStraightSection, source.StraightStartAngle,
					source.StraightEndAngle, source.LateralOffset, source.VerticalOffset,
					source.Scale);
			}
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetWireCapBevelSize(float size)
		{
			_wireCapBevelSize = math.max(0f, size);
			RebuildGeneratedMeshes();
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
			RebuildGeneratedMeshes();
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
				if (fixture is WireRailBraceFixture brace) {
					changed |= brace.EnsureBraceInitialized(SplineLength);
					changed |= brace.SetDiameter(_wireDiameter);
					if (!brace.ScaleInitialized && spline != null && _segments != null
						&& _segments.Count > 0
						&& WireRailFixtureMeshGenerator.TryEvaluateBraceProfile(spline,
							_segments, brace, out var profile)) {
						changed |= brace.EnsureScaleInitialized(profile.BaseRadius);
					}
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
			if (_segments.Count > 1 && _segments.All(segment =>
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
					var clampedDistance = math.clamp(_segments[layoutIndex].Distance,
						_segments[layoutIndex - 1].Distance, splineLength);
					if (!Mathf.Approximately(_segments[layoutIndex].Distance, clampedDistance)) {
						_segments[layoutIndex].SetDistance(clampedDistance, splineLength);
						changed = true;
					}
				}
			}
			changed |= SynchronizeSegmentConnections();
			changed |= SynchronizeFixtures();

			if (changed) {
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
			var insertIndex = 1;
			while (insertIndex < _segments.Count
				&& _segments[insertIndex].Distance <= distance) {
				insertIndex++;
			}
			var source = _segments[insertIndex - 1];
			var layout = source.Clone(_wireDiameter);
			layout.SetDistance(distance, SplineLength);
			source.ResetConnection();
			_segments.Insert(insertIndex, layout);
			SynchronizeSegmentConnections();
			RebuildGeneratedMeshes();
			MarkDirty();
			return insertIndex;
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
			_segments.RemoveAt(layoutIndex);
			if (_segments.Count > 0) {
				_segments[0].SetDistance(0f, SplineLength);
			}
			SynchronizeSegmentConnections();
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void SetLayoutDistance(int layoutIndex, float distance)
		{
			var layout = GetSegment(layoutIndex);
			if (layoutIndex == 0) {
				distance = 0f;
			} else {
				var minimum = _segments[layoutIndex - 1].Distance;
				var maximum = layoutIndex + 1 < _segments.Count
					? _segments[layoutIndex + 1].Distance
					: SplineLength;
				distance = math.clamp(distance, minimum, maximum);
			}
			layout.SetDistance(distance, SplineLength);
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		public void MoveLayout(int fromIndex, int toIndex)
		{
			SynchronizeSegments();
			if (fromIndex < 0 || fromIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(fromIndex));
			}
			if (toIndex < 0 || toIndex >= _segments.Count) {
				throw new ArgumentOutOfRangeException(nameof(toIndex));
			}
			if (fromIndex == toIndex) {
				return;
			}
			var distanceSlots = _segments.Select(segment => segment.Distance).ToArray();
			var layout = _segments[fromIndex];
			_segments.RemoveAt(fromIndex);
			_segments.Insert(toIndex, layout);
			for (var layoutIndex = 0; layoutIndex < _segments.Count; layoutIndex++) {
				_segments[layoutIndex].SetDistance(distanceSlots[layoutIndex], SplineLength);
			}
			SynchronizeSegmentConnections();
			RebuildGeneratedMeshes();
			MarkDirty();
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

			SynchronizeSegments();
			RebuildGeneratedMeshes();
			MarkDirty();
		}

		private void OnSplineCollectionChanged(SplineContainer container, int _)
		{
			if (container == GetSplineContainerWithoutCreating()) {
				SynchronizeSegments();
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
				SynchronizeSegments();
				_renderMesh = WireRailRenderMeshGenerator.Generate(container.Spline, _segments,
					_fixtures, _wireCapBevelSize, _renderSamplesPerSegment, _radialSegments,
					_renderMesh);
				if (!WireRailColliderMeshGenerator.TryGenerate(container.Spline, _segments,
						_referenceBallDiameter, _colliderSamplesPerSegment,
						_colliderMesh, out _colliderMesh, out _colliderEdgeVertices,
						out _generationError)) {
					if (_colliderMesh) {
						_colliderMesh.Clear(false);
					} else {
						_colliderMesh = null;
					}
					_colliderEdgeVertices = Array.Empty<Vector3>();
				}

				var meshFilter = GetOrAddComponent<MeshFilter>(container.gameObject);
				var meshRenderer = GetOrAddComponent<MeshRenderer>(container.gameObject);
				meshFilter.sharedMesh = _renderMesh;
				AssignRenderMaterial(meshRenderer);
				_collidersDirty = true;
#if UNITY_EDITOR
				_generatedInputHash = ComputeGenerationInputHash();
#endif
			} finally {
				_rebuildingGeneratedMeshes = false;
			}
		}

#if UNITY_EDITOR
		private int ComputeGenerationInputHash()
		{
			unchecked {
				var hash = 17;
				var container = GetSplineContainerWithoutCreating();
				var spline = container ? container.Spline : null;
				hash = hash * 31 + (spline?.Count ?? 0);
				hash = hash * 31 + (spline != null && spline.Closed ? 1 : 0);
				if (spline != null) {
					for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++) {
						hash = hash * 31 + spline[knotIndex].GetHashCode();
						hash = hash * 31 + spline.GetTangentMode(knotIndex).GetHashCode();
						hash = hash * 31
							+ spline.GetAutoSmoothTension(knotIndex).GetHashCode();
					}
				}

				hash = hash * 31 + (_segments?.Count ?? 0);
				hash = hash * 31 + _railCount;
				if (_segments != null) {
					for (var segmentIndex = 0; segmentIndex < _segments.Count;
						segmentIndex++) {
						hash = hash * 31 + (_segments[segmentIndex] == null ? 0
							: JsonUtility.ToJson(_segments[segmentIndex]).GetHashCode());
					}
				}
				hash = hash * 31 + (_fixtures?.Count ?? 0);
				if (_fixtures != null) {
					for (var fixtureIndex = 0; fixtureIndex < _fixtures.Count; fixtureIndex++) {
						var fixture = _fixtures[fixtureIndex];
						hash = hash * 31 + (fixture?.GetType().FullName?.GetHashCode() ?? 0);
						hash = hash * 31 + (fixture == null ? 0
							: JsonUtility.ToJson(fixture).GetHashCode());
					}
				}
				hash = hash * 31 + _wireDiameter.GetHashCode();
				hash = hash * 31 + _wireCapBevelSize.GetHashCode();
				hash = hash * 31 + _radialSegments;
				hash = hash * 31 + _renderSamplesPerSegment;
				hash = hash * 31 + (_renderMaterial
					? _renderMaterial.GetEntityId().GetHashCode() : 0);
				hash = hash * 31 + _referenceBallDiameter.GetHashCode();
				hash = hash * 31 + _colliderSamplesPerSegment;
				return hash;
			}
		}
#endif

		public SplineContainer EnsureSplineContainerExists()
			=> EnsureSplineContainer();

		private void AssignRenderMaterial(MeshRenderer renderer)
		{
			if (_renderMaterial) {
				renderer.sharedMaterial = _renderMaterial;
				return;
			}
			var pipeline = GraphicsSettings.currentRenderPipeline;
			renderer.sharedMaterial = pipeline
				? pipeline.defaultMaterial
				: Resources.Load<Material>("Materials/Table Opaque (Builtin)");
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

			var child = transform.Find(SplineObjectName);
			_splineContainer = child ? child.GetComponent<SplineContainer>() : null;
			return _splineContainer;
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
			set => _elasticity = value;
		}

		public float PhysicsElasticityFalloff {
			get => _elasticityFalloff;
			set => _elasticityFalloff = value;
		}

		public float PhysicsFriction {
			get => _friction;
			set => _friction = value;
		}

		public float PhysicsScatter {
			get => _scatter;
			set => _scatter = value;
		}

		public bool PhysicsOverwrite {
			get => _overwritePhysics;
			set => _overwritePhysics = value;
		}

		public PhysicsMaterialAsset PhysicsMaterialReference {
			get => _physicsMaterial;
			set => _physicsMaterial = value;
		}

		public float4x4 GetLocalToPlayfieldMatrixInVpx(float4x4 worldToPlayfield)
			=> Physics.GetLocalToPlayfieldMatrixInVpx(transform.localToWorldMatrix,
				worldToPlayfield);

		public void OnTransformationChanged(float4x4 currTransformationMatrix)
		{
			_collidersDirty = true;
		}

		void ICollidableComponent.GetColliders(Player player, PhysicsEngine physicsEngine,
			ref ColliderReference colliders, float4x4 translateWithinPlayfieldMatrix, float margin)
		{
			if (!_colliderMesh || _colliderMesh.vertexCount == 0) {
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
			using var indices = new NativeArray<int>(_colliderMesh.triangles,
				Allocator.TempJob);
			ColliderUtils.GenerateCollidersFromMesh(in vertices, in indices,
				translateWithinPlayfieldMatrix, info, ref colliders, true);

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
			=> isActiveAndEnabled && _colliderMesh && _colliderMesh.vertexCount > 0;

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
