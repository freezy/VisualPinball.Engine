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

// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RollingSoundTests
	{
		[Test]
		public void VpxResponseMatchesIndependentReferenceAtEveryIntegerSpeed()
		{
			var row = new RollingSoundResponse { MinimumSpeed = 2, GainFactor = .00011f, GainExponent = 3, PitchMode = RollingPitchMode.QuadraticHertz, PitchFactor = 15, SpeedMode = RollingSpeedMode.IntegerHorizontal };
			for (var s = 0; s <= 140; s++) {
				Assert.That(row.Speed(new float3(s + .99f, 0, 70), 900), Is.EqualTo(s));
				Assert.That(row.Gain(s) * .5f * .8f, Is.EqualTo(s < 2 ? 0 : .000044 * s * s * s).Within(.00002));
				Assert.That(row.Pitch(s, 44100), Is.EqualTo(1 + 15.0 * s * s / 44100).Within(.00001));
				row.PitchMode = RollingPitchMode.LinearHertz; row.PitchFactor = 11000f / 59; row.PitchOrigin = 1;
				foreach (var offset in new[] { -4000, -1000 }) { row.PitchOffset = offset; Assert.That(row.Pitch(s, 44100), Is.EqualTo(1 + (offset + (s - 1) * 11000.0 / 59) / 44100).Within(.00001)); }
				row.PitchMode = RollingPitchMode.QuadraticHertz; row.PitchFactor = 15; row.PitchOffset = 0;
			}
		}

		[Test]
		public void TagsUseAssetIdentityAndNeverImplicitlyFallBackToSteel()
		{
			var steel = ScriptableObject.CreateInstance<BallSoundMaterialAsset>(); var ceramic = ScriptableObject.CreateInstance<BallSoundMaterialAsset>();
			var surface = ScriptableObject.CreateInstance<RollingSoundMaterialAsset>();
			try {
				var row = new RollingSoundResponse { BallMaterial = steel }; surface.Responses = new[] { row };
				Assert.That(surface.FindResponse(ceramic), Is.Null); Assert.That(surface.FindResponse(steel), Is.SameAs(row));
				surface.FallbackBallMaterial = steel; Assert.That(surface.FindResponse(ceramic), Is.SameAs(row));
			} finally { Object.DestroyImmediate(surface); Object.DestroyImmediate(steel); Object.DestroyImmediate(ceramic); }
		}

		[TestCase(0f, 0f, 1f, true)]
		[TestCase(1f, 0f, 0f, false)]
		[TestCase(0f, 0f, -1f, false)]
		public void GravitySelectsSupportAndRejectsSideWallsAndCovers(float x, float y, float z, bool support)
			=> Assert.That(RollingPhysics.IsSupport(new float3(x, y, z), new float3(0, 0, -1), 0, 0), Is.EqualTo(support));

		[Test]
		public void SupportRejectsRecedingSeparatedAndNonFiniteContacts()
		{
			Assert.That(RollingPhysics.IsSupport(new float3(0, 0, 1), new float3(0, 0, -1), 0, 1), Is.False);
			Assert.That(RollingPhysics.IsSupport(new float3(0, 0, 1), new float3(0, 0, -1), 30, 0), Is.False);
			Assert.That(RollingPhysics.IsSupport(new float3(float.NaN), new float3(0, 0, -1), 0, 0), Is.False);
		}

		[Test]
		public void ShortCrossingsRemainOrderedAndMissingSupportMutesImmediately()
		{
			using var ring = new RollingTransitionRing(1024, Allocator.Temp);
			var writable = ring;
			var ball = new RollingBallState { BallId = -7, Generation = 3 };
			var candidates = new FixedList4096Bytes<RollingCandidate>();
			foreach (var surface in new[] { 1, 2, 3 }) {
				candidates.Clear(); candidates.Add(new RollingCandidate { Surface = surface, Weight = 1, Speed = 10 });
				RollingPhysics.Reduce(ref ball, ref candidates, surface * .004, ref writable);
			}
			Assert.That(ball.Surface, Is.EqualTo(3));
			Assert.That(ring.Cursors[1], Is.EqualTo(3));
			Assert.That(ring.Entries[1].From, Is.EqualTo(1)); Assert.That(ring.Entries[1].To, Is.EqualTo(2));
			candidates.Clear(); RollingPhysics.Reduce(ref ball, ref candidates, .013, ref writable);
			Assert.That(ball.Surface, Is.Zero); Assert.That(ball.Speed, Is.Zero); Assert.That(ball.ReservedSurface, Is.EqualTo(3));
			candidates.Add(new RollingCandidate { Surface = 3, Weight = 1, Speed = 10 }); RollingPhysics.Reduce(ref ball, ref candidates, .02, ref writable);
			Assert.That(ring.Cursors[1], Is.EqualTo(3), "a short seam must not replay the entry");
			candidates.Clear(); RollingPhysics.Reduce(ref ball, ref candidates, .05, ref writable);
			Assert.That(ball.ReservedSurface, Is.Zero); Assert.That(ring.Cursors[1], Is.EqualTo(4)); Assert.That(ring.Entries[3].Air, Is.True);
		}

		[Test]
		public void SilentSupportIsDistinctFromAirAndHistoryCannotRetainAnAbsentWinner()
		{
			using var ring = new RollingTransitionRing(1024, Allocator.Temp); var writable = ring;
			var ball = new RollingBallState { BallId = 1, Surface = 5, ReservedSurface = 5, Supported = true, ReservedSupport = true };
			ball.History.Add(new RollingSample { Surface = 5, Weight = 100, Time = .001 });
			var candidates = new FixedList4096Bytes<RollingCandidate>(); candidates.Add(new RollingCandidate { Surface = 0, Weight = 1 });
			RollingPhysics.Reduce(ref ball, ref candidates, .002, ref writable);
			Assert.That(ball.ReservedSurface, Is.Zero); Assert.That(ring.Entries[0].From, Is.EqualTo(5)); Assert.That(ring.Entries[0].To, Is.Zero); Assert.That(ring.Entries[0].Air, Is.False);
		}

		[Test]
		public void TransitionRingBoundsOverflowAndHonorsSnapshotWatermark()
		{
			using var ring = new RollingTransitionRing(4, Allocator.Temp); var writable = ring;
			for (var i = 0; i < 6; i++) writable.Push(new RollingTransition { BallId = i });
			Assert.That(ring.Cursors[2], Is.EqualTo(2)); var watermark = ring.Cursors[1];
			Assert.That(writable.TryPop(watermark, out var transition), Is.True); Assert.That(transition.BallId, Is.EqualTo(2));
			writable.Push(new RollingTransition { BallId = 6 });
			var ids = new List<int>(); while (writable.TryPop(watermark, out transition)) ids.Add(transition.BallId);
			Assert.That(ids, Is.EqualTo(new[] { 3, 4, 5 })); Assert.That(writable.TryPop(ring.Cursors[1], out transition), Is.True); Assert.That(transition.BallId, Is.EqualTo(6));
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ContactObserverKeepsPhysicsUnchangedAndCaptureClearsSupport(bool frozen)
		{
			using var harness = new PhysicsStateHarness();
			var transforms = new NativeParallelHashMap<int, float4x4>(1, Allocator.Temp);
			var references = new ColliderReference(ref transforms, Allocator.Temp);
			var rolling = new RollingPhysicsState(4, 1024, Allocator.Temp);
			var contacts = new NativeList<ContactBufferElement>(Allocator.Temp);
			var disabled = new NativeParallelHashSet<int>(1, Allocator.Temp);
			try {
				references.Add(new PlaneCollider(new float3(0, 0, 1), 0, new ColliderInfo { ItemId = 99, ItemType = ItemType.Playfield, RollingSurfaceId = 3 }));
				harness.SetStaticColliders(ref references);
				var state = harness.CreateState(); state.Rolling = rolling; state.DisabledCollisionItems = disabled; var playfieldObject = new GameObject("rolling test playfield");
				try { state.Env = new PhysicsEnv(0, playfieldObject.AddComponent<PlayfieldComponent>(), 1); } finally { Object.DestroyImmediate(playfieldObject); }
				var ball = new BallState { Id = 1, Radius = 25, Mass = 1, Position = new float3(0, 0, 25), Velocity = new float3(10, 0, .1f), IsFrozen = frozen, AttachedMagnetId = 17 };
				harness.Balls[1] = ball; rolling.Balls[1] = new RollingBallState { BallId = 1, Generation = 2, Surface = 2, ReservedSurface = 2 };
				var evt = CollisionEventData.None; evt.SetCollider(0, false); evt.IsContact = true; evt.HitNormal = new float3(0, 0, 1);
				contacts.Add(new ContactBufferElement(1, evt) { FrictionVelocity = new float3(10, 0, 0) });
				RollingPhysics.Observe(ref state, ref contacts, .1f, .001);
				Assert.That(harness.Balls[1].Position, Is.EqualTo(ball.Position)); Assert.That(harness.Balls[1].Velocity, Is.EqualTo(ball.Velocity));
				Assert.That(rolling.Balls[1].Surface, Is.EqualTo(frozen ? 0 : 3)); Assert.That(rolling.Balls[1].Speed, Is.EqualTo(frozen ? 0 : 10));
				Assert.That(contacts[0].CollEvent.HitNormal, Is.EqualTo(evt.HitNormal));
			} finally { rolling.Dispose(); contacts.Dispose(); disabled.Dispose(); references.Dispose(); transforms.Dispose(); }
		}

		[Test]
		public void ColliderHeadersRoundTripTheirAcousticIdentity()
		{
			var info = new ColliderInfo { ItemId = 17, RollingSurfaceId = 25 }; var header = new ColliderHeader(); header.Init(info, ColliderType.Triangle);
			Assert.That(header.ColliderInfo.RollingSurfaceId, Is.EqualTo(25)); var other = header; other.RollingSurfaceId = 26;
			Assert.That(header, Is.Not.EqualTo(other)); Assert.That(header.GetHashCode(), Is.Not.EqualTo(other.GetHashCode()));
		}
		[Test]
		public void RendererOwnsMoreThanThirtyTwoBallsWithoutRestartingLoopsOrResurrectingMute()
		{
			var root = new GameObject("rolling voices"); var context = new PhysicsEngineContext();
			var material = ScriptableObject.CreateInstance<RollingSoundMaterialAsset>(); var tag = ScriptableObject.CreateInstance<BallSoundMaterialAsset>();
			var bank = ScriptableObject.CreateInstance<SoundEffectAsset>(); var clip = AudioClip.Create("steel", 44100, 1, 44100, false);
			var gain = MechanicalSoundGain.Gain;
			try {
				bank.Clips = new[] { clip }; typeof(SoundEffectAsset).GetField("_loop", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(bank, true);
				material.Responses = new[] { new RollingSoundResponse { BallMaterial = tag, Loop = bank } };
				var renderer = root.AddComponent<RollingSoundComponent>(); renderer.DefaultBallMaterial = tag; renderer.Prepare(context);
				var surface = new GameObject("surface"); surface.transform.SetParent(root.transform, false); var setting = surface.AddComponent<RollingSoundSurfaceComponent>(); setting.Mode = RollingSurfaceMode.Override; setting.Material = material;
				var surfaceId = RollingSoundComponent.SurfaceId(setting, null);
				for (var i = 0; i < 64; i++) {
					var go = new GameObject("ball"); go.transform.SetParent(root.transform, false); var ball = go.AddComponent<BallComponent>();
					context.BallComponents[ball.Id] = ball; context.RollingGenerations[ball.Id] = 1;
					context.Rolling.Balls[ball.Id] = new RollingBallState { BallId = ball.Id, Generation = 1, Surface = surfaceId, ReservedSurface = surfaceId, Speed = 10, Velocity = new float3(10, 0, 0) };
				}
				renderer.Capture(); renderer.Apply(); Assert.That(renderer.LoopStarts, Is.EqualTo(64));
				MechanicalSoundGain.Gain = 0;
				for (var frame = 0; frame < 20; frame++) { renderer.Capture(); renderer.Apply(); renderer.UpdateVoices(.016f); }
				Assert.That(renderer.LoopStarts, Is.EqualTo(64));
				var sources = root.GetComponentsInChildren<AudioSource>(); Assert.That(sources, Has.Length.EqualTo(64));
				Assert.That(sources.All(source => source.volume == 0), Is.True);
				var first = context.BallComponents.First().Key; renderer.ForgetBall(first); context.BallComponents.Remove(first); context.RollingGenerations.Remove(first); context.Rolling.Balls.Remove(first);
				renderer.Capture(); renderer.Apply(); Assert.That(root.GetComponentsInChildren<AudioSource>(), Has.Length.EqualTo(63));
			} finally { Object.DestroyImmediate(root); context.Rolling.Dispose(); Object.DestroyImmediate(material); Object.DestroyImmediate(tag); Object.DestroyImmediate(bank); Object.DestroyImmediate(clip); MechanicalSoundGain.Gain = gain; }
		}

	}
}
