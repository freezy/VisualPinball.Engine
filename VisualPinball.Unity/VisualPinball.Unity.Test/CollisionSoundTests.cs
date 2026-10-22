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
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class CollisionSoundTests
	{
		[Test]
		public void PlanarBandsUseRawSpeedButVolumeUsesIntegerBallVelocity()
		{
			var impact = new BallImpact { Velocity = new float3(5.9f, 0, 0), IncomingVelocity = new float3(0, 0, -7) };
			Assert.That(ImpactSoundRule.Measure(ImpactSoundMetric.PlanarSpeedSquared, impact), Is.EqualTo(25));
			Assert.That(new ImpactSoundRule { Minimum = 5, IncludeMinimum = false }.Matches(impact), Is.True);
			impact.Velocity.x = 5;
			Assert.That(new ImpactSoundRule { Minimum = 5, IncludeMinimum = false }.Matches(impact), Is.False);
			Assert.That(new ImpactSoundRule { Maximum = 5 }.Matches(impact), Is.True);
			Assert.That(ImpactSoundRule.Measure(ImpactSoundMetric.VerticalSpeedSquared, impact), Is.EqualTo(49));
		}

		[Test]
		public void LayeringAndRetriggerAreIndependentForDifferentBalls()
		{
			var go = new GameObject("collision voice test");
			var profile = ScriptableObject.CreateInstance<CollisionSoundAsset>();
			var clip = AudioClip.Create("impact", 441, 1, 44100, false);
			try {
				profile.Clips = new[] { clip };
				profile.Rules = new[] {
					new ImpactSoundRule { Clips = new[] { 0 }, Volume = ImpactSoundMetric.Speed, Gain = .015f },
					new ImpactSoundRule { Clips = new[] { 0 }, Volume = ImpactSoundMetric.VerticalSpeedSquared, Gain = .025f }
				};
				var sound = go.AddComponent<CollisionSoundProbe>();
				sound.SoundAsset = profile;
				sound.OnImpact(new BallImpact { BallId = 1, Speed = 10, IncomingVelocity = new float3(0, 0, -4), TimeMsec = 100 });
				Assert.That(sound.Gains, Is.EqualTo(new[] { .15f, .4f }).Within(.00001));
				sound.OnImpact(new BallImpact { BallId = 1, Speed = 10, TimeMsec = 101 });
				Assert.That(sound.Gains, Has.Count.EqualTo(2));
				sound.OnImpact(new BallImpact { BallId = 2, Speed = 10, TimeMsec = 101 });
				Assert.That(sound.Gains, Has.Count.EqualTo(4), "another ball must not be suppressed by a shared bank cooldown");
			}
			finally { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); Object.DestroyImmediate(clip); }
		}

		[Test]
		public void LoopingCollisionProfilesCannotStartVoices()
		{
			var go = new GameObject("looping collision test");
			var profile = ScriptableObject.CreateInstance<CollisionSoundAsset>();
			var clip = AudioClip.Create("impact", 441, 1, 44100, false);
			try {
				profile.Clips = new[] { clip };
				profile.Rules = new[] { new ImpactSoundRule { Clips = new[] { 0 }, Volume = ImpactSoundMetric.Constant } };
				var serialized = new UnityEditor.SerializedObject(profile);
				serialized.FindProperty("_loop").boolValue = true;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				var sound = go.AddComponent<CollisionSoundProbe>();
				sound.SoundAsset = profile;
				sound.OnImpact(new BallImpact { BallId = 1, Speed = 10, TimeMsec = 100 });
				sound.OnImpact(new BallImpact { BallId = 2, Speed = 10, TimeMsec = 100 });
				Assert.That(sound.Gains, Is.Empty, "an impact has no stop event for a looping voice");
				serialized.FindProperty("_loop").boolValue = false;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				sound.OnImpact(new BallImpact { BallId = 1, Speed = 10, TimeMsec = 100 });
				Assert.That(sound.Gains, Has.Count.EqualTo(1), "a rejected loop must not consume the retrigger clock");
			}
			finally { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); Object.DestroyImmediate(clip); }
		}

		[TestCase(false, false)]
		[TestCase(false, true)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void MovingColliderQueuesRelativeImpactForAStationaryBall(bool localSpace, bool angular)
		{
			using var harness = new PhysicsStateHarness();
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var references = new ColliderReference(ref transforms, Allocator.Persistent, true);
			var matrix = float4x4.TRS(new float3(120, 80, 30), quaternion.RotateZ(math.PI / 2), new float3(1));
			try {
				var info = new ColliderInfo { ItemId = 99, ItemType = ItemType.Primitive,
					FireImpactEvents = true, Material = new PhysicsMaterialData { Elasticity = .8f } };
				var localPosition = new float3(25, 100, 25);
				if (localSpace) {
					references.Add(new CircleCollider(new float2(-100, 100), 100, 0, 100, info), matrix);
				} else {
					references.Add(new LineCollider(new float2(0, 200), new float2(0, 0), 0, 100, info), matrix);
				}
				var colliders = new NativeColliders(ref references, Allocator.Persistent);
				try {
					harness.KinematicTransforms.Add(99, matrix);
					harness.KinematicVelocities.Add(99, new KinematicVelocityState {
						LinearVelocity = angular ? float3.zero : math.rotate(matrix, new float3(10, 0, 0)),
						AngularVelocity = angular ? new float3(0, 0, -.1f) : float3.zero,
						Pivot = matrix.c3.xyz
					});
					var state = harness.CreateState();
					state.KinematicColliders = colliders;
					var normal = math.rotate(matrix, new float3(1, 0, 0));
					var position = math.transform(matrix, localPosition);
					var ball = new BallState { Id = 7, Radius = 25, Mass = 1, Position = position };
					ball.CollisionEvent.SetCollider(0, true);
					ball.CollisionEvent.HitNormal = normal;
					PhysicsStaticCollision.Collide(0, ref ball, ref state);
					Assert.That(math.length(ball.Velocity), Is.GreaterThan(0), "the moving surface must actually hit the ball");
					Assert.That(harness.EventQueue.Count, Is.EqualTo(1));
					var impact = harness.EventQueue.Dequeue().Impact;
					Assert.That(impact.Speed, Is.EqualTo(10).Within(.0001));
					Assert.That(math.length(impact.IncomingVelocity), Is.LessThan(.0001));
					Assert.That(math.distance(impact.Position, position), Is.LessThan(.0001));
					Assert.That(math.distance(impact.Normal, normal), Is.LessThan(.0001));
				}
				finally { colliders.Dispose(); }
			}
			finally { references.Dispose(); transforms.Dispose(); }
		}

		[TestCase(10, true, 1)]
		[TestCase(10, false, 0)]
		[TestCase(0, true, 0)]
		[TestCase(-10, true, 0)]
		[TestCase(1, true, 0, 2)]
		[TestCase(2, true, 1, 2)]
		public void FloorCollisionQueuesCapturedImpactOnlyForAnOptedInRealBounce(float fallingSpeed, bool enabled, int count, float threshold = 0)
		{
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Persistent);
			var references = new ColliderReference(ref transforms, Allocator.Persistent);
			using var harness = new PhysicsStateHarness();
			try {
				var id = 0;
				references.Add(new PlaneCollider(new float3(0, 0, 1), 0, new ColliderInfo {
					ItemId = 99, ItemType = ItemType.Playfield, FireImpactEvents = enabled, HitThreshold = threshold,
					Material = new PhysicsMaterialData { Elasticity = .8f }
				}));
				harness.SetStaticColliders(ref references);
				var state = harness.CreateState();
				var ball = new BallState { Id = 7, Radius = 25, Mass = 1, Position = new float3(100, 200, 25), Velocity = new float3(0, 0, -fallingSpeed) };
				ball.CollisionEvent.SetCollider(id, false);
				ball.CollisionEvent.HitNormal = new float3(0, 0, 1);
				PhysicsStaticCollision.Collide(0, ref ball, ref state);
				Assert.That(harness.EventQueue.Count, Is.EqualTo(count));
				if (count != 0) {
					var data = harness.EventQueue.Dequeue();
					Assert.That(data.EventId, Is.EqualTo(EventId.BallImpact));
					Assert.That(data.Impact.ItemId, Is.EqualTo(99));
					Assert.That(data.Impact.IncomingVelocity.z, Is.EqualTo(-fallingSpeed));
					Assert.That(data.Impact.Velocity.z, Is.GreaterThan(0));
					Assert.That(data.Impact.Position, Is.EqualTo(new float3(100, 200, 25)));
				}
			}
			finally { references.Dispose(); transforms.Dispose(); }
		}

		[Test]
		public void BallPairCollisionIsQueuedOnlyOnceAndRetainsCallbackVelocity()
		{
			using var harness = new PhysicsStateHarness();
			var first = new BallState { Id = 1, Radius = 25, Mass = 1, Position = new float3(50, 0, 25), Velocity = new float3(-10, 0, 0) };
			var second = new BallState { Id = 2, Radius = 25, Mass = 1, Position = new float3(0, 0, 25) };
			first.CollisionEvent.SetBallItem(second.Id);
			first.CollisionEvent.HitNormal = new float3(1, 0, 0);
			second.CollisionEvent.SetBallItem(first.Id);
			second.CollisionEvent.HitNormal = new float3(-1, 0, 0);
			harness.Balls.Add(first.Id, first);
			harness.Balls.Add(second.Id, second);
			var state = harness.CreateState();
			PhysicsDynamicCollision.Collide(0, ref first, ref state);
			harness.Balls[first.Id] = first;
			PhysicsDynamicCollision.Collide(0, ref second, ref state);
			Assert.That(harness.EventQueue.Count, Is.EqualTo(1));
			var impact = harness.EventQueue.Dequeue().Impact;
			Assert.That(impact.Kind, Is.EqualTo(BallImpactKind.BallCollision));
			Assert.That(impact.Speed, Is.EqualTo(10));
			Assert.That(impact.Velocity, Is.EqualTo(impact.IncomingVelocity));
		}

		[Test]
		public void CollisionComponentPackingPreservesBallPairSourceAndGain()
		{
			var go = new GameObject("pack collision");
			try {
				var source = go.AddComponent<CollisionSoundComponent>();
				source.Source = BallImpactKind.BallCollision;
				source.Volume = .8f;
				var copy = go.AddComponent<CollisionSoundComponent>();
				((IPackable)copy).Unpack(((IPackable)source).Pack());
				Assert.That(copy.Source, Is.EqualTo(BallImpactKind.BallCollision));
				Assert.That(copy.Volume, Is.EqualTo(.8f));
			}
			finally { Object.DestroyImmediate(go); }
		}
	}

	public class CollisionSoundProbe : CollisionSoundComponent
	{
		public readonly List<float> Gains = new();
		protected override void StartImpactSound(SoundEffectAsset asset, float gain, SoundEffectPlayback playback) => Gains.Add(gain);
	}
}
