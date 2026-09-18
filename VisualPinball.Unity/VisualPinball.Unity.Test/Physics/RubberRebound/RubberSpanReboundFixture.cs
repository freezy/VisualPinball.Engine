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
using Unity.Collections;
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// The section 12 worked span of the deformable plan, expressed once as a runtime
	/// span record and once as the double-precision reference of section 6.1, from the
	/// same compiled 128-sample stress table.
	/// </summary>
	/// <remarks>
	/// The span lies along <c>+x</c> from the origin, its outward normal is <c>+y</c>,
	/// and the ball approaches from <c>+y</c>, so a shot's normal velocity is negative
	/// and a positive incidence angle carries the ball toward anchor B.
	/// </remarks>
	internal static class RubberSpanReboundFixture
	{
		internal const double ThicknessVpx = RubberSpanCoefficients.WorkedThicknessVpx;
		internal const double LengthVpx = RubberSpanCoefficients.WorkedInstalledLengthVpx;
		internal const double Stretch = RubberSpanCoefficients.WorkedStretch;
		internal const double UnloadedLengthVpx = LengthVpx / Stretch;
		internal const int ItemId = 4711;

		/// <summary>Cross-section area of the worked cord, in square VPX units.</summary>
		internal static double AreaVpx
			=> Math.PI * ThicknessVpx * ThicknessVpx * 0.25;

		internal static IRubberStressCurve DefaultCurve => new RubberDefaultStressCurve();

		internal static IRubberStressCurve LinearRampCurve => new RubberLinearRampStressCurve();

		internal static IEnumerable<(string Name, IRubberStressCurve Curve)> Materials
		{
			get
			{
				yield return ("default", DefaultCurve);
				yield return ("linear", LinearRampCurve);
			}
		}

		internal static readonly double[] Positions = { 0.5, 0.25, 0.1, 0.05 };
		internal static readonly double[] Speeds = { 8.0, 18.0, 30.0 };
		internal static readonly double[] IncidenceDegrees = { 0.0, 30.0, -30.0 };

		/// <summary>
		/// The baker's compiled table, in VPE stress units: 128 samples of the curve
		/// uniformly spaced over <c>[1, MaximumStretchRatio]</c>.
		/// </summary>
		internal static double[] StressTable(IRubberStressCurve curve)
		{
			var count = RubberCompiledDeformationMaterial.LutSampleCount;
			var table = new double[count];
			for (var i = 0; i < count; i++) {
				var stretch = 1.0 + (curve.MaximumStretchRatio - 1.0) * i / (count - 1.0);
				table[i] = RubberDeformationUnits.StressToVpe(curve.StressPascals(stretch));
			}
			return table;
		}

		internal static RubberSpanReboundReference Reference(IRubberStressCurve curve)
			=> new RubberSpanReboundReference(LengthVpx, UnloadedLengthVpx, AreaVpx,
				curve.MaximumStretchRatio, StressTable(curve));

		/// <summary>The same span as the runtime record the collide pass reads.</summary>
		internal static RubberSpanRebound Record(IRubberStressCurve curve)
		{
			var table = new FixedList4096Bytes<float>();
			foreach (var sample in StressTable(curve)) {
				table.Add((float)sample);
			}
			return new RubberSpanRebound {
				AnchorA = float3.zero,
				AnchorB = new float3((float)LengthVpx, 0f, 0f),
				ChordDirection = new float3(1f, 0f, 0f),
				OutwardNormal = new float3(0f, 1f, 0f),
				Length = (float)LengthVpx,
				UnloadedLength = (float)UnloadedLengthVpx,
				CrossSectionArea = (float)AreaVpx,
				MaximumStretchRatio = (float)curve.MaximumStretchRatio,
				ItemId = ItemId,
				PathElementIndex = 0,
				StressTable = table,
			};
		}

		/// <summary>
		/// A record whose tension overflows single precision, so the first step already
		/// leaves the finite range.
		/// </summary>
		internal static RubberSpanRebound DivergingRecord()
		{
			var record = Record(DefaultCurve);
			var table = new FixedList4096Bytes<float>();
			for (var i = 0; i < RubberCompiledDeformationMaterial.LutSampleCount; i++) {
				table.Add(1e37f);
			}
			record.StressTable = table;
			return record;
		}

		/// <summary>In-plane velocity of a shot: chord in x, outward normal in y.</summary>
		internal static (double Chord, double Normal) Velocity(double speed,
			double incidenceDegrees)
		{
			var radians = incidenceDegrees * Math.PI / 180.0;
			return (speed * Math.Sin(radians), -speed * Math.Cos(radians));
		}

		internal static float2 VelocityFloat(double speed, double incidenceDegrees)
		{
			var (chord, normal) = Velocity(speed, incidenceDegrees);
			return new float2((float)chord, (float)normal);
		}

		internal static double ExitSpeed(float2 velocity)
			=> Math.Sqrt((double)velocity.x * velocity.x + (double)velocity.y * velocity.y);

		internal static double ExitAngleDegrees(float2 velocity)
			=> Math.Atan2(velocity.x, velocity.y) * 180.0 / Math.PI;

		/// <summary>Every case of the section 6.1 qualification matrix.</summary>
		internal static IEnumerable<Shot> Matrix()
		{
			foreach (var (name, curve) in Materials) {
				foreach (var u in Positions) {
					foreach (var speed in Speeds) {
						foreach (var incidence in IncidenceDegrees) {
							yield return new Shot(name, curve, u, speed, incidence);
						}
					}
				}
			}
		}

		internal readonly struct Shot
		{
			internal readonly string Material;
			internal readonly IRubberStressCurve Curve;
			internal readonly double U;
			internal readonly double Speed;
			internal readonly double IncidenceDegrees;

			internal Shot(string material, IRubberStressCurve curve, double u, double speed,
				double incidenceDegrees)
			{
				Material = material;
				Curve = curve;
				U = u;
				Speed = speed;
				IncidenceDegrees = incidenceDegrees;
			}

			internal bool Matches(string material, double u, double speed, double incidence)
				=> Material == material && Math.Abs(U - u) < 1e-9
					&& Math.Abs(Speed - speed) < 1e-9
					&& Math.Abs(IncidenceDegrees - incidence) < 1e-9;

			public override string ToString()
				=> $"{Material}|u={U:0.###}|v={Speed:0.#}|inc={IncidenceDegrees:+0;-0;0}";
		}
	}
}
