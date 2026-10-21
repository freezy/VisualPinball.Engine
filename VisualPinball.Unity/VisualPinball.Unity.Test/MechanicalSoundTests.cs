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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using VisualPinball.Unity.Simulation;

namespace VisualPinball.Unity.Test
{
	public class MechanicalSoundTests
	{
		[Test]
		public void NativeInputObserversWorkWithoutGamelogicAndDoNotRepeatHeldPresses() {
			var obj = new GameObject("Input observer test");
			obj.SetActive(false);
			using (var simulation = new SimulationThread(obj.AddComponent<PhysicsEngine>(), null, null)) {
				try {
					var observed = new List<bool>();
					simulation.MainThreadInputObserver = (action, pressed) =>
					{
						if (action == VisualPinball.Engine.Common.InputConstants.ActionInsertCoin1)
							observed.Add(pressed);
					};
					foreach (var value in new[] { 1f, 1f, 0f, 1f })
						simulation.EnqueueInputEvent(new NativeInputApi.InputEvent
						{
							EventType = (int)NativeInputApi.InputEventType.Action,
							Action = (int)NativeInputApi.InputAction.CoinInsert1,
							Value = value
						});
					Invoke(simulation, "ProcessInputEvents");
					simulation.FlushMainThreadInputDispatch();
					Assert.That(observed, Is.EqualTo(new[] { true, false, true }));
					observed.Clear();
					simulation.EnqueueInputEvent(new NativeInputApi.InputEvent
					{
						EventType = (int)NativeInputApi.InputEventType.Action,
						Action = (int)NativeInputApi.InputAction.CoinInsert1,
						Value = 0
					});
					Invoke(simulation, "ProcessInputEvents");
					simulation.ResetInputNotifications(false);
					simulation.FlushMainThreadInputDispatch();
					Assert.That(observed, Is.EqualTo(new[] { false }), "backend changes rearm without replaying queued presses");
				}
				finally { Object.DestroyImmediate(obj); }
			}
		}

		[TestCase(MultiPlayMode.PlayInParallel)]
		[TestCase(MultiPlayMode.DoNotPlay)]
		[TestCase(MultiPlayMode.StopPrevious)]
		[TestCase(MultiPlayMode.FadeOutPrevious)]
		public void FlipperAttackAndUpRemainIndependentLayers(MultiPlayMode mode) {
			var obj = new GameObject("Layered flipper sound");
			var attack = ScriptableObject.CreateInstance<SoundEffectAsset>();
			var up = ScriptableObject.CreateInstance<SoundEffectAsset>();
			var clip = AudioClip.Create("Flipper test", 44100, 1, 44100, false);
			try {
				attack.Clips = new[] { clip };
				up.Clips = new[] { clip };
				var sound = obj.AddComponent<MechanicalSoundTestFlipper>();
				sound.MultiPlayMode = mode;
				sound.PlayLayers(attack, up);
				Assert.That(obj.GetComponents<AudioSource>().Length, Is.EqualTo(2));
				Assert.That(Array.TrueForAll(obj.GetComponents<AudioSource>(), a => a.isPlaying), Is.True);
			}
			finally {
				Object.DestroyImmediate(obj);
				Object.DestroyImmediate(attack);
				Object.DestroyImmediate(up);
				Object.DestroyImmediate(clip);
			}
		}

		[Test]
		public void CooldownsBelongToOwnerAndBankAndSurvivePlaybackInstances() {
			var first = ScriptableObject.CreateInstance<SoundEffectAsset>();
			var second = ScriptableObject.CreateInstance<SoundEffectAsset>();
			try {
				var a = new SoundPlaybackContext();
				var b = new SoundPlaybackContext();
				Assert.That(a.TryStart(first, 1, .02f), Is.True);
				Assert.That(a.TryStart(first, 1.001f, .02f), Is.False);
				Assert.That(b.TryStart(first, 1.001f, .02f), Is.True);
				Assert.That(a.TryStart(second, 1.001f, .02f), Is.True);
				Assert.That(a.TryStart(first, 1.021f, .02f), Is.True);
				Assert.That(a.TryStart(first, 1.021f, 0), Is.True, "zero cooldown preserves repeated pulses");
				Assert.That(a.TryStart(first, 0, .02f), Is.True, "a reset clock must not suppress the next game");
			}
			finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
		}

		[TestCase(50, 70, false)]
		[TestCase(50.001f, 70, true)]
		[TestCase(-50, -70, false)]
		[TestCase(-50.001f, -70, true)]
		[TestCase(95, 70, true)]
		[TestCase(-95, -70, true)]
		public void ReflipThresholdIsStrictAndSymmetric(float angle, float end, bool expected) {
			Assert.That(FlipperSoundComponent.IsReflip(angle, 0, end, 20), Is.EqualTo(expected));
		}

		[TestCase(115, 120, false)]
		[TestCase(114.999f, 120, true)]
		[TestCase(-115, -120, false)]
		[TestCase(-114.999f, -120, true)]
		[TestCase(126, 120, false)]
		[TestCase(-126, -120, false)]
		public void ReleaseThresholdIsStrictAndSymmetric(float angle, float start, bool expected) {
			Assert.That(FlipperSoundComponent.HasReleaseTravel(angle, start, 0, 5), Is.EqualTo(expected));
		}

		[Test]
		public void NewSoundConfigurationUsesDerivedPackingThroughInterface() {
			var obj = new GameObject("Sounds");
			try {
				var motor = obj.AddComponent<PinballEventSoundComponent>();
				motor.EventName = "Mech Speed";
				motor.Threshold = 3;
				motor.Comparison = SoundEventComparison.LessThan;
				motor.Volume = .4f;
				var bytes = ((IPackable)motor).Pack();
				motor.EventName = "";
				motor.Threshold = 0;
				motor.Volume = 1;
				((IPackable)motor).Unpack(bytes);
				Assert.That(motor.EventName, Is.EqualTo("Mech Speed"));
				Assert.That(motor.Threshold, Is.EqualTo(3));
				Assert.That(motor.Comparison, Is.EqualTo(SoundEventComparison.LessThan));
				Assert.That(motor.Volume, Is.EqualTo(.4f));
				var flipper = obj.AddComponent<FlipperSoundComponent>();
				flipper.ReflipDegrees = 17;
				flipper.AttackGain = new Vector2(.2f, .7f);
				flipper.MultiPlayMode = MultiPlayMode.StopPrevious;
				bytes = ((IPackable)flipper).Pack();
				flipper.ReflipDegrees = 0;
				flipper.AttackGain = Vector2.zero;
				((IPackable)flipper).Unpack(bytes);
				Assert.That(flipper.ReflipDegrees, Is.EqualTo(17));
				Assert.That(flipper.AttackGain, Is.EqualTo(new Vector2(.2f, .7f)));
				Assert.That(flipper.MultiPlayMode, Is.EqualTo(MultiPlayMode.StopPrevious));
			}
			finally { Object.DestroyImmediate(obj); }
		}

		[UnityTest]
		public IEnumerator MotorLoopRestartsInsideBankCooldown() => MotorRestartInsideBankCooldown(true, MultiPlayMode.PlayInParallel);

		[UnityTest]
		public IEnumerator MotorLoopRestartsWithDoNotPlay() => MotorRestartInsideBankCooldown(true, MultiPlayMode.DoNotPlay);

		[UnityTest]
		public IEnumerator MotorLoopRestartsWithStopPrevious() => MotorRestartInsideBankCooldown(true, MultiPlayMode.StopPrevious);

		[UnityTest]
		public IEnumerator MotorLoopRestartsWithFadeOutPrevious() => MotorRestartInsideBankCooldown(true, MultiPlayMode.FadeOutPrevious);

		[UnityTest]
		public IEnumerator MotorOneShotKeepsBankCooldown() => MotorRestartInsideBankCooldown(false, MultiPlayMode.PlayInParallel);

		private IEnumerator MotorRestartInsideBankCooldown(bool loop, MultiPlayMode mode) {
			var obj = new GameObject("Motor loop cooldown test");
			var asset = ScriptableObject.CreateInstance<SoundEffectAsset>();
			var clip = AudioClip.Create("Motor test", 44100, 1, 44100, false);
			try {
				asset.Clips = new[] { clip };
				typeof(SoundEffectAsset).GetField("_loop", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, loop);
				typeof(SoundEffectAsset).GetField("_cooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, .2f);
				var emitter = obj.AddComponent<MechanicalSoundTestEmitter>();
				var sound = obj.AddComponent<PinballEventSoundComponent>();
				sound.SoundAsset = asset;
				sound.MultiPlayMode = mode;
				sound.EventName = "Mech Speed";
				Invoke(sound, "OnEnableAfterAfterAwake");
				emitter.Emit(1);
				Assert.That(sound.IsPlayingOrRequestingSound(), Is.True, "initial sound must start");
				emitter.Emit(0);
				emitter.Emit(1);
				// Observe the new playback after the canceled loop has finished cleaning up.
				yield return null;
				yield return null;
				Assert.That(sound.IsPlayingOrRequestingSound(), Is.EqualTo(loop), "loops resume without another predicate transition; one-shot cooldown remains enforced");
				Assert.That(obj.GetComponents<AudioSource>(), Has.Length.EqualTo(loop ? 1 : 0));
				if (loop) Assert.That(obj.GetComponent<AudioSource>().isPlaying, Is.True);
				emitter.Emit(2);
				Assert.That(obj.GetComponents<AudioSource>(), Has.Length.EqualTo(loop ? 1 : 0), "a held true predicate must not restart playback");
				emitter.Emit(0);
				yield return null;
				yield return null;
				Assert.That(sound.IsPlayingOrRequestingSound(), Is.False);
			}
			finally {
				Object.DestroyImmediate(obj);
				Object.DestroyImmediate(asset);
				Object.DestroyImmediate(clip);
			}
		}

		[Test]
		public void LoopCooldownStillDebouncesStartsWithoutAStop() {
			var obj = new GameObject("Uninterrupted loop cooldown test");
			var asset = ScriptableObject.CreateInstance<SoundEffectAsset>();
			var clip = AudioClip.Create("Loop test", 44100, 1, 44100, false);
			try {
				asset.Clips = new[] { clip };
				typeof(SoundEffectAsset).GetField("_loop", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, true);
				typeof(SoundEffectAsset).GetField("_cooldown", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, .2f);
				var sound = obj.AddComponent<MechanicalSoundTestPlayer>();
				sound.SoundAsset = asset;
				sound.Play();
				sound.Play();
				Assert.That(obj.GetComponents<AudioSource>(), Has.Length.EqualTo(1), "without a stop, loop starts must still respect bank cooldown");
			}
			finally {
				Object.DestroyImmediate(obj);
				Object.DestroyImmediate(asset);
				Object.DestroyImmediate(clip);
			}
		}

		[Test]
		public void MotorBindsUniquelyAndKeepsPredicateActiveAfterFinitePlayback() {
			var obj = new GameObject("Motor");
			try {
				var emitter = obj.AddComponent<MechanicalSoundTestEmitter>();
				var sound = obj.AddComponent<MechanicalSoundTestListener>();
				sound.EventName = "Mech Speed";
				Invoke(sound, "OnEnableAfterAfterAwake");
				foreach (var speed in new[] { 0f, 1f, 2f, 0f, -1f, 1f })
					emitter.Emit(speed);
				Assert.That(sound.Starts, Is.EqualTo(2));
				emitter.Emit(3);
				Assert.That(sound.Starts, Is.EqualTo(2), "completion of a finite sample does not rearm the predicate");
				Invoke(sound, "OnDisable");
				emitter.Emit(0);
				emitter.Emit(1);
				Assert.That(sound.Starts, Is.EqualTo(2), "disabled listeners must unsubscribe");
				Invoke(sound, "OnEnableAfterAfterAwake");
				emitter.Emit(1);
				Assert.That(sound.Starts, Is.EqualTo(3));
				Invoke(sound, "OnDisable");
			}
			finally { Object.DestroyImmediate(obj); }
		}

		private static void Invoke(object target, string name) {
			for (var type = target.GetType(); type != null; type = type.BaseType) {
				var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				if (method != null) {
					method.Invoke(target, null);
					return;
				}
			}
			throw new MissingMethodException(name);
		}
	}

	public class MechanicalSoundTestEmitter : MonoBehaviour, IPinballEventEmitter
	{
		public PinballEvent[] Events => new[] { new PinballEvent("Mech Speed", PinballEventUnit.None) };
		public event EventHandler<PinballEventArgs> OnPinballEvent;
		public void Emit(float value) => OnPinballEvent?.Invoke(this, new PinballEventArgs("Mech Speed", value, PinballEventUnit.None));
	}

	public class MechanicalSoundTestListener : PinballEventSoundComponent
	{
		public int Starts;
		protected override void StartSound(SoundAsset asset, float gain = 1f) => Starts++;
	}

	public class MechanicalSoundTestPlayer : SoundComponent
	{
		public void Play() => StartSound();
	}

	public class MechanicalSoundTestFlipper : FlipperSoundComponent
	{
		public void PlayLayers(SoundEffectAsset attack, SoundEffectAsset up) {
			StartSound(attack);
			StartSound(up);
		}
	}
}
