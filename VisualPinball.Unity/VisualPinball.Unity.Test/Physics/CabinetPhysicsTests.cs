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
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Common;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Covers cabinet spring, keyboard nudge, and plumb-bob physics primitives.
	/// </summary>
	public class CabinetPhysicsTests
	{
		[Test]
		public void OscillatorTracksAnalyticStepResponse()
		{
			const float mass = 113f;
			const float frequencyHz = 9.3f;
			const float dampingRatio = 0.052f;
			const float targetAcceleration = 12f;
			var oscillator = new DampedHarmonicOscillator(mass, frequencyHz, dampingRatio);
			var force = mass * targetAcceleration;

			for (var i = 0; i < 50; i++) {
				oscillator.StepOneMillisecond(force);
			}

			var expected = AnalyticStepResponse(mass, frequencyHz, dampingRatio, force, 0.050f);
			Assert.That(oscillator.Displacement, Is.EqualTo(expected.Displacement).Within(0.0008f));
			Assert.That(oscillator.Velocity, Is.EqualTo(expected.Velocity).Within(0.03f));
			Assert.That(oscillator.Acceleration, Is.EqualTo(expected.Acceleration).Within(0.7f));
		}

		[Test]
		public void CabinetPhysicsUsesCalibratedAxisModels()
		{
			var cabinet = CabinetPhysicsState.Default;
			cabinet.StepOneMillisecond(new float2(cabinet.Mass * 12f, cabinet.Mass * -10f));

			Assert.That(cabinet.CabinetAcceleration.x, Is.EqualTo(12f).Within(1e-5f));
			Assert.That(cabinet.CabinetAcceleration.y, Is.EqualTo(-10f).Within(1e-5f));
			Assert.That(cabinet.CabinetOffset.x, Is.GreaterThan(0f));
			Assert.That(cabinet.CabinetOffset.y, Is.LessThan(0f));
		}

		[Test]
		public void KeyboardCabinetModelLimitsVisibleRebounds()
		{
			var nudge = new KeyboardNudgeState(KeyboardNudgeMode.CabModel, 1f, 5f);
			nudge.Nudge(0f, 2f);

			var offsets = new float[1000];
			var peak = 0f;
			for (var i = 0; i < offsets.Length; i++) {
				nudge.StepOneMillisecond();
				offsets[i] = nudge.CabinetOffset.y;
				peak = math.max(peak, math.abs(offsets[i]));
			}

			Assert.That(CountVisibleExtrema(offsets, peak * 0.1f), Is.LessThanOrEqualTo(3));
		}

		[Test]
		public void PlumbTiltLatchesAndQueuesTiltEvent()
		{
			var plumb = new PlumbState(true, 1f, 2f);

			for (var i = 0; i < 1000 && !plumb.TiltHigh; i++) {
				plumb.StepOneMillisecond(new float2(80f, 0f));
			}

			Assert.That(plumb.TiltHigh, Is.True);
			Assert.That(plumb.TiltIndex, Is.EqualTo(1));
			Assert.That(plumb.PendingTiltStates.Length, Is.EqualTo(1));
			Assert.That(plumb.PendingTiltStates[0], Is.EqualTo(1));

			var status = plumb.ReadAndResetTiltStatus();
			Assert.That(math.abs(status.x), Is.GreaterThan(0f));
			Assert.That(status.z, Is.GreaterThan(100f));

			var resetStatus = plumb.ReadAndResetTiltStatus();
			Assert.That(resetStatus.x, Is.EqualTo(0f));
			Assert.That(resetStatus.y, Is.EqualTo(0f));
			Assert.That(resetStatus.z, Is.EqualTo(0f));

			plumb.ClearPendingTiltEvents();
			Assert.That(plumb.PendingTiltStates.Length, Is.EqualTo(0));
		}

		[Test]
		public void NativeTiltAndSimulatedPlumbKeepTheSwitchClosedUntilBothRelease()
		{
			const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
			var table = new GameObject("Tilt routing test");
			table.SetActive(false);
			try {
				var playfield = new GameObject("Playfield");
				playfield.transform.SetParent(table.transform);
				var physics = playfield.AddComponent<PhysicsEngine>();
				var player = table.AddComponent<Player>();
				var bob = table.AddComponent<TiltBobComponent>();
				var direct = new SwitchMapping { Id = "tilt", Source = SwitchSource.InputSystem, InputAction = InputConstants.ActionTilt };
				var device = new SwitchMapping { Id = "tilt", Source = SwitchSource.Playfield, Device = bob, DeviceItem = TiltBobComponent.SwitchItem };
				Assert.That(TiltBobComponent.OwnsTiltInputDestination(direct, new[] { direct, device }), Is.False,
					"An inactive, unregistered bob does not take over the direct Tilt binding.");
				typeof(TiltBobComponent).GetMethod("Awake", privateInstance).Invoke(bob, null);
				var api = (TiltBobApi)player.Switch(bob, TiltBobComponent.SwitchItem);
				Assert.That(api, Is.Not.Null, "The table-root Tilt Bob registers with the child playfield engine.");
				var edges = new List<bool>();
				api.Switch += (_, evt) => edges.Add(evt.IsEnabled);

				using var simulation = new SimulationThread(physics, null, null);
				var simulated = bob.CreateSimulatedTiltDispatcher();
				simulation.SetTiltBobDispatchers(bob.SetPhysicalTilt, simulated);
				var processInput = typeof(SimulationThread).GetMethod("ProcessInputEvents", privateInstance);
				var queueTilt = typeof(SimulationThread).GetMethod("QueueMainThreadTiltState", privateInstance);

				void Physical(bool pressed)
				{
					simulation.EnqueueInputEvent(new NativeInputApi.InputEvent {
						EventType = (int)NativeInputApi.InputEventType.Action,
						Action = (int)NativeInputApi.InputAction.Tilt,
						Value = pressed ? 1f : 0f
					});
					processInput.Invoke(simulation, null);
				}
				void Simulated(bool pressed) =>
					queueTilt.Invoke(simulation, new object[] { simulated, pressed });

				Physical(true);
				Simulated(true);
				Physical(false);
				simulation.FlushMainThreadInputDispatch();
				Assert.That(api.IsSwitchEnabled, Is.True);
				Assert.That(edges, Is.EqualTo(new[] { true }));
				Physical(true);
				Simulated(false);
				simulation.FlushMainThreadInputDispatch();
				Assert.That(edges, Is.EqualTo(new[] { true }));

				Simulated(true);
				bob.ApplySettings(new CabinetPlumbSettings { mode = TiltBobMode.Physical });
				bob.ApplySettings(new CabinetPlumbSettings());
				Physical(false);
				simulation.FlushMainThreadInputDispatch();
				Assert.That(edges, Is.EqualTo(new[] { true, false }), "An old simulated edge cannot close the switch after a mode change.");

				player.NotifyCabinetInputAction(InputConstants.ActionTilt, true);
				player.NotifyCabinetInputAction(InputConstants.ActionTilt, false);
				Assert.That(edges, Is.EqualTo(new[] { true, false, true, false }));

				Assert.That(TiltBobComponent.OwnsTiltInputDestination(direct, new[] { direct, device }), Is.True);
				direct.Id = "other";
				Assert.That(TiltBobComponent.OwnsTiltInputDestination(direct, new[] { direct, device }), Is.False);
			} finally {
				Object.DestroyImmediate(table);
			}
		}

		[Test]
		public void PlumbPlayerTuningOverridesAndRestoresAuthoredValues()
		{
			var table = new GameObject("Tilt tuning test");
			table.SetActive(false);
			try {
				var playfield = new GameObject("Playfield");
				playfield.transform.SetParent(table.transform);
				var physics = playfield.AddComponent<PhysicsEngine>();
				var bob = table.AddComponent<TiltBobComponent>();
				bob.PlumbDamping = .8f;
				bob.PlumbThresholdAngle = 2f;

				bob.ApplySettings(new CabinetPlumbSettings { damping = 1.5f, thresholdDeg = 10f });
				Assert.That(physics.PlumbDamping, Is.EqualTo(.8f));
				Assert.That(physics.PlumbThresholdAngle, Is.EqualTo(2f));
				TiltBobComponent.ApplySettings(physics, new CabinetPlumbSettings { overrideTuning = true, damping = 1.5f, thresholdDeg = 20f });
				Assert.That(physics.PlumbDamping, Is.EqualTo(1.5f));
				Assert.That(physics.PlumbThresholdAngle, Is.EqualTo(15f));
				Assert.That(physics.GetNudgeSettings().plumb.overrideTuning, Is.True);
				TiltBobComponent.ApplySettings(physics, new CabinetPlumbSettings());
				Assert.That(physics.PlumbDamping, Is.EqualTo(.8f));
				Assert.That(physics.PlumbThresholdAngle, Is.EqualTo(2f));
			} finally {
				Object.DestroyImmediate(table);
			}
		}

		/// <summary>
		/// Computes the closed-form underdamped oscillator response used to verify
		/// the one-millisecond integrator.
		/// </summary>
		private static (float Displacement, float Velocity, float Acceleration) AnalyticStepResponse(float mass, float frequencyHz, float dampingRatio, float force, float time)
		{
			var omega0 = 2f * math.PI * frequencyHz;
			var omegaD = omega0 * math.sqrt(1f - dampingRatio * dampingRatio);
			var alpha = dampingRatio * omega0;
			var steadyState = force / (mass * omega0 * omega0);
			var decay = math.exp(-alpha * time);
			var displacement = steadyState * (1f - decay * (math.cos(omegaD * time) + alpha / omegaD * math.sin(omegaD * time)));
			var velocity = steadyState * decay * (omega0 * omega0 / omegaD) * math.sin(omegaD * time);
			var acceleration = (force - 2f * dampingRatio * mass * omega0 * velocity - mass * omega0 * omega0 * displacement) / mass;

			return (displacement, velocity, acceleration);
		}

		/// <summary>
		/// Counts visible rebound peaks after ignoring small tail oscillations.
		/// </summary>
		private static int CountVisibleExtrema(float[] values, float threshold)
		{
			var count = 0;
			for (var i = 2; i < values.Length; i++) {
				var previousSlope = values[i - 1] - values[i - 2];
				var slope = values[i] - values[i - 1];
				if (math.abs(values[i - 1]) <= threshold || previousSlope == 0f || slope == 0f) {
					continue;
				}
				if (math.sign(previousSlope) != math.sign(slope)) {
					count++;
				}
			}
			return count;
		}
	}
}
