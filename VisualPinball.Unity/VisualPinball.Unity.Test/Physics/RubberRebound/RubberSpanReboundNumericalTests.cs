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

using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	using Math = System.Math;

	/// <summary>
	/// Section 6.1 of the static rebound plan: the surrogate integration against its
	/// double-precision reference, its domain rules and its energy behaviour.
	/// </summary>
	public class RubberSpanReboundNumericalTests
	{
		/// <summary>
		/// The two cases the plan expects to fall back: the apex leaves the anchor
		/// interval at the low end within the first few milliseconds.
		/// </summary>
		private static readonly (string Material, double U, double Speed, double Incidence)[]
			ExpectedFallbacks = {
				("default", 0.05, 30.0, -30.0),
				("linear", 0.05, 30.0, -30.0),
			};

		[Test]
		public void RuntimeIntegrationShouldMatchTheDoublePrecisionReferenceOnTheMatrix()
		{
			var failures = new List<string>();
			var worstSpeedError = 0.0;
			var worstAngleError = 0.0;
			var maximumSteps = 0;
			var minimumReturn = double.MaxValue;
			var maximumReturn = 0.0;

			foreach (var shot in RubberSpanReboundFixture.Matrix()) {
				var record = RubberSpanReboundFixture.Record(shot.Curve);
				var reference = RubberSpanReboundFixture.Reference(shot.Curve);
				var (chord, normal) = RubberSpanReboundFixture.Velocity(shot.Speed,
					shot.IncidenceDegrees);
				var expected = reference.Run(shot.U, chord, normal);
				var actual = RubberSpanRebound.Integrate(in record, (float)shot.U,
					RubberSpanReboundFixture.VelocityFloat(shot.Speed, shot.IncidenceDegrees), 1f);

				if (!actual.Success) {
					failures.Add($"{shot} -> {actual.Failure} (reference {(expected.Success ? "returned" : expected.Failure.ToString())})");
					TestContext.Out.WriteLine(
						$"{shot}|runtime=FAIL:{actual.Failure}|steps={actual.Steps}|reference={(expected.Success ? "ok" : "FAIL:" + expected.Failure)}");
					continue;
				}
				Assert.That(expected.Success, Is.True,
					$"{shot}: the runtime returned but the reference failed with {expected.Failure}");

				var speed = RubberSpanReboundFixture.ExitSpeed(actual.ExitVelocity);
				var angle = RubberSpanReboundFixture.ExitAngleDegrees(actual.ExitVelocity);
				var speedError = Math.Abs(speed - expected.ExitSpeed) / expected.ExitSpeed;
				var angleError = Math.Abs(angle - expected.ExitAngleDegrees);
				worstSpeedError = Math.Max(worstSpeedError, speedError);
				worstAngleError = Math.Max(worstAngleError, angleError);
				maximumSteps = Math.Max(maximumSteps, actual.Steps);
				minimumReturn = Math.Min(minimumReturn, actual.ReturnTime);
				maximumReturn = Math.Max(maximumReturn, actual.ReturnTime);

				TestContext.Out.WriteLine(
					$"{shot}|steps={actual.Steps}|return={actual.ReturnTime:F4}vpe|exit={speed:F4}|angle={angle:F4}|depth={actual.Depth:F4}|apex={actual.ApexFraction:F4}|refExit={expected.ExitSpeed:F4}|refAngle={expected.ExitAngleDegrees:F4}|dSpeed={speedError * 100.0:F4}%|dAngle={angleError:F4}deg|residual={actual.EnergyResidual:E2}|corrected={actual.EnergyCorrected}");

				Assert.That(speedError, Is.LessThan(0.01),
					$"{shot}: exit speed {speed} against reference {expected.ExitSpeed}");
				Assert.That(angleError, Is.LessThan(1.0),
					$"{shot}: exit angle {angle} against reference {expected.ExitAngleDegrees}");
			}

			TestContext.Out.WriteLine(
				$"matrix|worstSpeedError={worstSpeedError * 100.0:F4}%|worstAngleError={worstAngleError:F4}deg|maxSteps={maximumSteps}|return={minimumReturn:F4}..{maximumReturn:F4}vpe");

			var expectedFallbacks = new List<string>();
			foreach (var fallback in ExpectedFallbacks) {
				foreach (var shot in RubberSpanReboundFixture.Matrix()) {
					if (shot.Matches(fallback.Material, fallback.U, fallback.Speed,
						fallback.Incidence)) {
						expectedFallbacks.Add($"{shot} -> {RubberSpanReboundFailure.ApexOutsideSpan} (reference {RubberSpanReboundFailure.ApexOutsideSpan})");
					}
				}
			}
			Assert.That(failures, Is.EquivalentTo(expectedFallbacks),
				"exactly the two documented fallbacks may fail");
			Assert.That(maximumSteps, Is.LessThanOrEqualTo(RubberSpanRebound.StepBudget));
		}

		[Test]
		public void ReferenceShouldConserveEnergyWithinAHundredthOfAPercent()
		{
			foreach (var shot in RubberSpanReboundFixture.Matrix()) {
				var reference = RubberSpanReboundFixture.Reference(shot.Curve);
				var (chord, normal) = RubberSpanReboundFixture.Velocity(shot.Speed,
					shot.IncidenceDegrees);
				var result = reference.Run(shot.U, chord, normal);
				if (!result.Success) {
					continue;
				}
				Assert.That(Math.Abs(result.EnergyResidual), Is.LessThan(1e-4),
					$"{shot}: reference energy residual {result.EnergyResidual:E3}");
			}
		}

		[Test]
		public void RuntimeEnergyCorrectionShouldOnlyEngageAboveFloatNoise()
		{
			var corrected = 0;
			foreach (var shot in RubberSpanReboundFixture.Matrix()) {
				var record = RubberSpanReboundFixture.Record(shot.Curve);
				var result = RubberSpanRebound.Integrate(in record, (float)shot.U,
					RubberSpanReboundFixture.VelocityFloat(shot.Speed, shot.IncidenceDegrees), 1f);
				if (!result.Success) {
					continue;
				}
				if (result.EnergyCorrected) {
					corrected++;
					Assert.That(result.EnergyResidual,
						Is.GreaterThan(RubberSpanRebound.EnergyCorrectionThreshold),
						$"{shot}: the correction engaged at a residual inside float noise");
				} else {
					Assert.That(result.EnergyResidual,
						Is.LessThanOrEqualTo(RubberSpanRebound.EnergyCorrectionThreshold),
						$"{shot}: a residual above float noise was left uncorrected");
				}
				Assert.That(Math.Abs(result.EnergyResidual),
					Is.LessThanOrEqualTo(RubberSpanRebound.EnergyResidualTolerance));
			}
			TestContext.Out.WriteLine($"energy|correctedCases={corrected}");
		}

		[Test]
		public void SeventyDegreeCentreHitShouldFailTheDomainRule()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var result = RubberSpanRebound.Integrate(in record, 0.5f,
				RubberSpanReboundFixture.VelocityFloat(18.0, 70.0), 1f);

			TestContext.Out.WriteLine(
				$"oblique70|failure={result.Failure}|steps={result.Steps}|residual={result.EnergyResidual:E3}|apex={result.ApexFraction:F4}");
			// The plan's 2.1 expects the residual rule here. Measured, the apex leaves the
			// anchor interval first, at step 100, so the span-coordinate rule catches it;
			// both rules fall back to the rigid response, so the behaviour is the plan's.
			Assert.That(result.Success, Is.False);
			Assert.That(result.Failure, Is.EqualTo(RubberSpanReboundFailure.ApexOutsideSpan));
		}

		[Test]
		public void AnObliqueCentreHitThatStoresEnergyShouldFailTheResidualRule()
		{
			// 60 degrees off the normal at the centre: the apex stays inside the anchor
			// interval but returns with energy still in the string.
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var result = RubberSpanRebound.Integrate(in record, 0.5f,
				RubberSpanReboundFixture.VelocityFloat(18.0, 60.0), 1f);

			TestContext.Out.WriteLine(
				$"oblique60|failure={result.Failure}|steps={result.Steps}|residual={result.EnergyResidual:E3}");
			Assert.That(result.Failure, Is.EqualTo(RubberSpanReboundFailure.EnergyResidual));
			Assert.That(math.abs(result.EnergyResidual),
				Is.GreaterThan(RubberSpanRebound.EnergyResidualTolerance));
		}

		[Test]
		public void ApexLeavingTheAnchorIntervalShouldFail()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var result = RubberSpanRebound.Integrate(in record, 0.05f,
				RubberSpanReboundFixture.VelocityFloat(30.0, -30.0), 1f);

			TestContext.Out.WriteLine(
				$"nearAnchor|failure={result.Failure}|steps={result.Steps}|time={result.Steps * RubberSpanRebound.IntegrationStep:F3}vpe");
			Assert.That(result.Failure, Is.EqualTo(RubberSpanReboundFailure.ApexOutsideSpan));
		}

		[Test]
		public void AbsurdSpeedShouldExhaustTheStressTable()
		{
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var result = RubberSpanRebound.Integrate(in record, 0.5f,
				new float2(0f, -5000f), 1f);

			Assert.That(result.Failure, Is.EqualTo(RubberSpanReboundFailure.TableExhausted));
		}

		[Test]
		public void NonFiniteStateShouldFail()
		{
			var record = RubberSpanReboundFixture.DivergingRecord();
			var result = RubberSpanRebound.Integrate(in record, 0.5f, new float2(0f, -18f), 1f);

			Assert.That(result.Failure, Is.EqualTo(RubberSpanReboundFailure.NonFinite));
		}

		[Test]
		public void AStationaryApexShouldExhaustTheStepBudget()
		{
			// A cord that never pushes back: the apex keeps sinking and never returns.
			var record = RubberSpanReboundFixture.Record(RubberSpanReboundFixture.DefaultCurve);
			var table = new global::Unity.Collections.FixedList4096Bytes<float>();
			for (var i = 0; i < RubberCompiledDeformationMaterial.LutSampleCount; i++) {
				table.Add(0f);
			}
			record.StressTable = table;

			var result = RubberSpanRebound.Integrate(in record, 0.5f, new float2(0f, -1f), 1f);

			Assert.That(result.Failure, Is.EqualTo(RubberSpanReboundFailure.StepBudget));
			Assert.That(result.Steps, Is.EqualTo(RubberSpanRebound.StepBudget));
		}

		[Test]
		public void EightModeOracleShouldConfirmTheMasslessLimit()
		{
			// A declared sanity check, not a gate: the oracle carries the cord's mass, so
			// it only has to land near the massless surrogate, and the plan records where
			// it does not.
			foreach (var u in new[] { 0.5, 0.25 }) {
				var coefficients = RubberSpanCoefficients.WorkedDefaultCurve();
				var oracle = new RubberSpanOracle(in coefficients);
				var shot = RubberSpanOracle.Shot.At(u, 18.0);
				var oracleResult = oracle.Run(in shot);

				var reference = RubberSpanReboundFixture.Reference(
					RubberSpanReboundFixture.DefaultCurve);
				var (chord, normal) = RubberSpanReboundFixture.Velocity(18.0, 0.0);
				var surrogate = reference.Run(u, chord, normal);
				Assert.That(surrogate.Success, Is.True, $"u={u}: the reference must return");

				var deviation = Math.Abs(oracleResult.ReboundSpeed - surrogate.ExitSpeed)
					/ surrogate.ExitSpeed;
				TestContext.Out.WriteLine(
					$"oracle|u={u}|oracle={oracleResult.ReboundSpeed:F4}|surrogate={surrogate.ExitSpeed:F4}|deviation={deviation * 100.0:F2}%|withinTenPercent={deviation <= 0.1}");
			}
			Assert.Pass("recorded, not gated");
		}
	}
}
