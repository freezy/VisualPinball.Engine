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
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// General Public License for more details.

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>A centred grid of whole square pyramids on an insert's finished flat surface.</summary>
	internal sealed class InsertStipple
	{
		private const int MaxCells = 4096;
		private readonly float2 _start;
		private readonly List<float2> _polygon;
		private readonly float2 _min;
		private readonly float2 _max;
		private readonly float _width;
		private readonly float _step;
		private readonly float _height;
		private readonly float _tolerance;
		private readonly int _columns;
		private readonly int _rows;
		private readonly bool[] _included;
		private readonly HashSet<int> _segments = new();

		public bool IsStippleSegment(IReadOnlyList<int> sources)
		{
			foreach (var source in sources) {
				if (_segments.Contains(source)) {
					return true;
				}
			}
			return false;
		}

		public InsertStipple(List<float2> polygon, InsertHole[] holes, float width, float margin, float height, float weld)
		{
			_polygon = polygon;
			_width = math.max(width, 0.01f);
			_step = _width + math.max(0f, margin);
			_height = math.max(0f, height);
			_tolerance = math.max(weld * 2f, 1e-4f);
			_min = new float2(float.MaxValue);
			_max = new float2(float.MinValue);
			foreach (var point in polygon) {
				_min = math.min(_min, point);
				_max = math.max(_max, point);
			}
			var span = _max - _min;
			_columns = math.max(0, (int)math.floor((span.x + _step - _width) / _step));
			_rows = math.max(0, (int)math.floor((span.y + _step - _width) / _step));
			if ((long)_columns * _rows > MaxCells) {
				throw new InsertOutlineException($"Stipple settings would make more than {MaxCells} cells. Increase the width or margin.");
			}
			_included = new bool[_columns * _rows];
			_start = (_min + _max - new float2(
				_columns > 0 ? _columns * _step - (_step - _width) : 0f,
				_rows > 0 ? _rows * _step - (_step - _width) : 0f)) * 0.5f;
			for (var row = 0; row < _rows; row++) {
				for (var column = 0; column < _columns; column++) {
					var lower = _start + new float2(column * _step, row * _step);
					_included[row * _columns + column] = Fits(polygon, holes, lower);
				}
			}
		}

		private bool Fits(List<float2> polygon, InsertHole[] holes, float2 lower)
		{
			var lo = lower - _tolerance;
			var hi = lower + _width + _tolerance;
			var corners = new[] {
				lo, new float2(hi.x, lo.y), hi, new float2(lo.x, hi.y),
			};
			foreach (var corner in corners) {
				if (!InsertMeshGenerator.Contains(polygon, corner)) {
					return false;
				}
			}
			for (var i = 0; i < polygon.Count; i++) {
				if (IntersectsBox(polygon[i], polygon[(i + 1) % polygon.Count], lo, hi)) {
					return false;
				}
			}
			if (holes != null) {
				foreach (var hole in holes) {
					if (hole.Radius <= 0f) {
						continue;
					}
					var center = new float2(hole.Center.x, hole.Center.y);
					var nearest = math.clamp(center, lo, hi);
					if (math.distancesq(nearest, center) <= hole.Radius * hole.Radius) {
						return false;
					}
				}
			}
			return true;
		}

		private static bool IntersectsBox(float2 a, float2 b, float2 lo, float2 hi)
		{
			if (math.all(a >= lo) && math.all(a <= hi) || math.all(b >= lo) && math.all(b <= hi)) {
				return true;
			}
			var delta = b - a;
			var from = 0f;
			var to = 1f;
			bool Clip(float p, float q)
			{
				if (math.abs(p) < 1e-10f) {
					return q >= 0f;
				}
				var t = q / p;
				if (p < 0f) { from = math.max(from, t); }
				else { to = math.min(to, t); }
				return from <= to;
			}
			return Clip(-delta.x, a.x - lo.x) && Clip(delta.x, hi.x - a.x)
				&& Clip(-delta.y, a.y - lo.y) && Clip(delta.y, hi.y - a.y);
		}

		public void AddSegments(InsertArrangement arrangement)
		{
			var previousUsed = false;
			for (var column = 0; column < _columns; column++) {
				var used = false;
				for (var row = 0; row < _rows; row++) {
					used |= _included[row * _columns + column];
				}
				if (!used) {
					previousUsed = false;
					continue;
				}
				var x = _start.x + column * _step;
				if (!previousUsed || _step > _width) {
					AddClippedLine(arrangement, x, true);
				}
				AddClippedLine(arrangement, x + _width, true);
				previousUsed = true;
			}
			previousUsed = false;
			for (var row = 0; row < _rows; row++) {
				var used = false;
				for (var column = 0; column < _columns; column++) {
					used |= _included[row * _columns + column];
				}
				if (!used) {
					previousUsed = false;
					continue;
				}
				var y = _start.y + row * _step;
				if (!previousUsed || _step > _width) {
					AddClippedLine(arrangement, y, false);
				}
				AddClippedLine(arrangement, y + _width, false);
				previousUsed = true;
			}
			for (var row = 0; row < _rows; row++) {
				for (var column = 0; column < _columns; column++) {
					if (!_included[row * _columns + column]) {
						continue;
					}
					var lo = _start + new float2(column * _step, row * _step);
					var hi = lo + _width;
					var apex = arrangement.AddPoint((lo + hi) * 0.5f);
					foreach (var corner in new[] { lo, new float2(hi.x, lo.y), hi, new float2(lo.x, hi.y) }) {
						_segments.Add(arrangement.AddSegment(arrangement.AddPoint(corner), apex));
					}
				}
			}
		}

		private void AddClippedLine(InsertArrangement arrangement, float coordinate, bool vertical)
		{
			var cuts = new List<float>();
			for (var i = 0; i < _polygon.Count; i++) {
				var a = _polygon[i];
				var b = _polygon[(i + 1) % _polygon.Count];
				var from = vertical ? a.x : a.y;
				var to = vertical ? b.x : b.y;
				if ((from <= coordinate && coordinate < to) || (to <= coordinate && coordinate < from)) {
					var along = vertical ? a.y + (b.y - a.y) * (coordinate - from) / (to - from)
						: a.x + (b.x - a.x) * (coordinate - from) / (to - from);
					cuts.Add(along);
				}
			}
			cuts.Sort();
			for (var i = 0; i + 1 < cuts.Count; i += 2) {
				if (cuts[i + 1] - cuts[i] <= _tolerance) {
					continue;
				}
				var a = vertical ? new float2(coordinate, cuts[i]) : new float2(cuts[i], coordinate);
				var b = vertical ? new float2(coordinate, cuts[i + 1]) : new float2(cuts[i + 1], coordinate);
				_segments.Add(arrangement.AddSegment(arrangement.AddPoint(a), arrangement.AddPoint(b)));
			}
		}

		public bool TryAt(float2 point, out float2 center)
		{
			center = default;
			if (_columns == 0 || _rows == 0) {
				return false;
			}
			var column = (int)math.floor((point.x - _start.x) / _step);
			var row = (int)math.floor((point.y - _start.y) / _step);
			if (column < 0 || column >= _columns || row < 0 || row >= _rows || !_included[row * _columns + column]) {
				return false;
			}
			var lower = _start + new float2(column * _step, row * _step);
			if (point.x < lower.x - _tolerance || point.x > lower.x + _width + _tolerance
				|| point.y < lower.y - _tolerance || point.y > lower.y + _width + _tolerance) {
				return false;
			}
			center = lower + _width * 0.5f;
			return true;
		}

		public float Height(float2 position)
		{
			if (!TryAt(position, out var center)) {
				return 0f;
			}
			return _height * math.max(0f, 1f - math.cmax(math.abs(position - center)) / (_width * 0.5f));
		}

		public Vector3 Normal(float2 position)
		{
			if (!TryAt(position, out var center)) {
				return Vector3.up;
			}
			var delta = position - center;
			var slope = _height * 2f / _width;
			var dx = math.abs(delta.x) >= math.abs(delta.y) ? -math.sign(delta.x) * slope : 0f;
			var dy = math.abs(delta.y) > math.abs(delta.x) ? -math.sign(delta.y) * slope : 0f;
			var x = InsertMeshGenerator.ToLocal(new float2(1f, 0f), dx) - InsertMeshGenerator.ToLocal(float2.zero);
			var y = InsertMeshGenerator.ToLocal(new float2(0f, 1f), dy) - InsertMeshGenerator.ToLocal(float2.zero);
			var normal = (Vector3)math.normalize((float3)Vector3.Cross(x, y));
			return normal.y < 0f ? -normal : normal;
		}
	}
}
