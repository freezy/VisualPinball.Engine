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
using System.Linq;
using System.Reflection;
using NativeTrees;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using VisualPinball.Engine.VPT;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RollingSoundReviewTests
	{
		[Test]
		public void GainClampsAfterBankVolumeAndSilentRowsReleaseOldVoices()
		{
			using var fixture = new AudioFixture(); var bank = fixture.Bank("quiet", .25f); var surface = fixture.Surface(bank, 2);
			var ball = fixture.Ball(); fixture.Set(ball, surface); fixture.Render();
			Assert.That(fixture.Source(ball).volume, Is.EqualTo(.5f).Within(.0001), "0.25 bank gain times 2 speed gain must remain 0.5");
			var silent = fixture.Surface(null, 2); fixture.Set(ball, silent); fixture.Render();
			Assert.That(fixture.Source(ball), Is.Null, "a valid row with no loop must never reuse the previous bank");
		}

		[Test]
		public void SharedSlotReleasePrecedesAnotherBallsEntryRegardlessOfSnapshotOrder()
		{
			using var fixture = new AudioFixture(); var bank = fixture.Bank("ramps", 1, 3); var surface = fixture.Surface(bank, 1, true);
			var first = fixture.Ball(); fixture.Set(first, surface); fixture.Context.Rolling.Transitions.Push(new RollingTransition { BallId = first, Generation = 1, To = surface, Air = true }); fixture.Render();
			Assert.That(fixture.Source(first).clip, Is.SameAs(bank.Clips[0]));
			var second = fixture.Ball(); fixture.Set(first, 0); fixture.Set(second, surface);
			fixture.Context.Rolling.Transitions.Push(new RollingTransition { BallId = first, Generation = 1, From = surface, Time = .001 });
			fixture.Context.Rolling.Transitions.Push(new RollingTransition { BallId = second, Generation = 1, To = surface, Time = .002, Air = true });
			fixture.Render(second);
			Assert.That(fixture.Source(second).clip, Is.SameAs(bank.Clips[0]), "global FIFO ordering must free slot zero before allocating the second ball");
		}

		[Test]
		public void SilentSupportToAnAudibleSurfaceIsAMaterialBoundary()
		{
			using var ring = new RollingTransitionRing(1024, Allocator.Temp); var writable = ring;
			var ball = new RollingBallState { BallId = 1 };
			var candidates = new FixedList4096Bytes<RollingCandidate>(); candidates.Add(new RollingCandidate { Surface = 0, Weight = 1 });
			RollingPhysics.Reduce(ref ball, ref candidates, .001, ref writable);
			candidates.Clear(); candidates.Add(new RollingCandidate { Surface = 5, Weight = 1 }); RollingPhysics.Reduce(ref ball, ref candidates, .002, ref writable);
			Assert.That(ring.Entries[1].To, Is.EqualTo(5)); Assert.That(ring.Entries[1].Air, Is.False);
		}

		[Test]
		public void TranslatedFlipperSurfaceVelocityUsesItsPivot()
		{
			var info = new ColliderInfo { ItemId = 1 }; var flipper = new FlipperCollider(50, 100, 10, 7, 0, 1, info);
			var point = new float3(20, 10, 25); var movement = new FlipperMovementState { AngleSpeed = .3f };
			var before = flipper.SurfaceVelocity(in movement, in point);
			var translation = new float3(400, 900, 0); flipper.Transform(flipper, float4x4.Translate(translation)); point += translation;
			Assert.That(math.distance(flipper.SurfaceVelocity(in movement, in point), before), Is.LessThan(.00001));
		}

		[Test]
		public void CatchUpTicksPublishMonotonicTimeInsteadOfReusingTheStateEnvironment()
		{
			var playfield = new GameObject("clock test playfield"); var context = new PhysicsEngineContext();
			var transforms = new NativeParallelHashMap<int, float4x4>(0, Allocator.Temp);
			var colliders = new ColliderReference(ref transforms, Allocator.Temp); var empty = new ColliderReference(ref transforms, Allocator.Temp, true);
			try {
				context.PhysicsEnv = new PhysicsEnv(0, playfield.AddComponent<PlayfieldComponent>(), .5f);
				context.InsideOfs = new InsideOfs(Allocator.Persistent);
				context.Rolling = new RollingPhysicsState(4, 1024, Allocator.Persistent);
				var info = new ColliderInfo { ItemId = 1, ItemType = ItemType.Playfield, RollingSurfaceId = 1 };
				colliders.Add(new PlaneCollider(new float3(0,0,1), 0, info)); colliders.Add(new PlaneCollider(new float3(0,0,-1), -200, info));
				context.Colliders = new NativeColliders(ref colliders, Allocator.Persistent); context.KinematicColliders = new NativeColliders(ref empty, Allocator.Persistent); context.KinematicCollidersAtIdentity = new NativeColliders(ref empty, Allocator.Persistent);
				context.KinematicColliderLookups = new NativeParallelHashMap<int, NativeColliderIds>(0, Allocator.Persistent);
				context.ElasticityOverVelocityLUTs = new NativeParallelHashMap<int, FixedList512Bytes<float>>(0, Allocator.Persistent); context.FrictionOverVelocityLUTs = new NativeParallelHashMap<int, FixedList512Bytes<float>>(0, Allocator.Persistent);
				var bounds = new AABB(new float3(-100), new float3(200));
				context.Octree = new NativeOctree<int>(bounds, 16, 8, Allocator.Persistent); context.KinematicOctree = new NativeOctree<int>(bounds,16,8,Allocator.Persistent); context.BallOctree = new NativeOctree<int>(bounds,16,8,Allocator.Persistent);
				context.Octree.Insert(0, bounds);
				context.PhysicsCycle = new PhysicsCycle(Allocator.Persistent); context.SimulationNativeResourcesCreated = true;
				context.BallStates.Ref[7] = new BallState { Id = 7, Radius = 25, Mass = 1, Position = new float3(0,0,25), Velocity = new float3(2,0,0), CollisionEvent = CollisionEventData.None };
				context.Rolling.RegisterBall(7,1);
				var state = context.CreateState();
				PhysicsUpdate.Execute(ref state, ref context.PhysicsEnv, ref context.OverlappingColliders, ref context.KinematicOctree, ref context.BallOctree, ref context.PhysicsCycle, 10000);
				Assert.That(state.Env.CurPhysicsFrameTime, Is.Zero, "the state environment is deliberately an old copy");
				Assert.That(context.Rolling.Balls[7].LastSupportTime, Is.GreaterThan(.009), "all ten catch-up ticks must advance rolling time");
			} finally { context.Dispose(); empty.Dispose(); colliders.Dispose(); transforms.Dispose(); Object.DestroyImmediate(playfield); }
		}

		[TestCase(false, false)]
		[TestCase(true, false)]
		[TestCase(true, true)]
		public void LocalSupportOwnsItsStaticFrameAndUsesTheTablesGravityMagnitude(bool kinematic, bool currentTransform)
		{
			using var harness = new PhysicsStateHarness(); var playfield = new GameObject("support test playfield");
			var transforms = new NativeParallelHashMap<int, float4x4>(0, Allocator.Temp); var references = new ColliderReference(ref transforms, Allocator.Temp, kinematic);
			var rolling = new RollingPhysicsState(1,1024,Allocator.Temp); var disabled = new NativeParallelHashSet<int>(0,Allocator.Temp); var contacts = new NativeList<ContactBufferElement>(Allocator.Temp);
			try {
				references.Add(new PlaneCollider(new float3(0,0,1),0,new ColliderInfo { ItemId=99, ItemType=ItemType.Primitive, RollingSurfaceId=3 }));
				var colliders = new NativeColliders(ref references,Allocator.Temp);
				try {
					var state=harness.CreateState(); if(kinematic)state.KinematicColliders=colliders;else state.Colliders=colliders;
					state.Rolling=rolling;state.DisabledCollisionItems=disabled;state.Env=new PhysicsEnv(0,playfield.AddComponent<PlayfieldComponent>(),.5f);
					rolling.SupportFrames[99]=new RollingSupportFrame { LocalDirection=new float3(1,0,0),StaticDirection=currentTransform ? new float3(0,0,1) : new float3(0,0,-1) };
					if(currentTransform)harness.KinematicTransforms[99]=float4x4.RotateY(math.PI/2);
					harness.Balls[1]=new BallState { Id=1,Radius=25,Mass=1,Position=new float3(0,0,25),Velocity=new float3(10,0,0) };rolling.RegisterBall(1,1);
					var evt=CollisionEventData.None;evt.SetCollider(0,kinematic);evt.HitNormal=new float3(0,0,1);evt.IsContact=true;
					contacts.Add(new ContactBufferElement(1,evt) { FrictionVelocity=new float3(10,0,0) });
					RollingPhysics.Observe(ref state,ref contacts,.1f,.001);
					Assert.That(rolling.Balls[1].Surface,Is.EqualTo(3));Assert.That(rolling.Balls[1].History[0].Weight,Is.EqualTo(.05f).Within(.00001));
				}finally{colliders.Dispose();}
			}finally{contacts.Dispose();disabled.Dispose();rolling.Dispose();references.Dispose();transforms.Dispose();Object.DestroyImmediate(playfield);}
		}

		[Test]
		public void TransitionVolumesUseTheirOwnSourceAndDestinationGroups()
		{
			using var fixture=new AudioFixture();var from=fixture.Surface(fixture.Bank("wood",1),1);var to=fixture.Surface(fixture.Bank("ramp",1),1);
			fixture.Material(from).Group=RollingSoundGroup.Playfield;fixture.Material(to).Group=RollingSoundGroup.Ramp;
			fixture.Material(from).Responses[0].Exit=fixture.Bank("exit",.2f,1,false);fixture.Material(to).Responses[0].Enter=fixture.Bank("enter",.4f,1,false);
			fixture.Renderer.Volume=.8f;fixture.Renderer.PlayfieldVolume=.5f;fixture.Renderer.RampVolume=0;
			var ball=fixture.Ball();fixture.Set(ball,from);fixture.Render();fixture.Set(ball,to);
			fixture.Context.Rolling.Transitions.Push(new RollingTransition { BallId=ball,Generation=1,From=from,To=to,Speed=10,Time=.1 });
			fixture.Renderer.Capture();fixture.Renderer.Apply();
			var sources=fixture.Renderer.GetComponentsInChildren<AudioSource>().Where(source=>!source.loop).ToArray();
			Assert.That(sources.Select(source=>source.clip.name),Is.EqualTo(new[]{"exit0","enter0"}));
			Assert.That(sources[0].volume,Is.EqualTo(.08f).Within(.00001));Assert.That(sources[1].volume,Is.Zero);
		}

		[Test]
		public void AirReleaseKeepsItsAuthoredDurationAndPauseKeepsTheVoiceSession()
		{
			using var fixture=new AudioFixture();var surface=fixture.Surface(fixture.Bank("rolling",1),1);fixture.Material(surface).Responses[0].ReleaseSeconds=1;
			var ball=fixture.Ball();fixture.Set(ball,surface);fixture.Render();var source=fixture.Source(ball);var starts=fixture.Renderer.LoopStarts;
			fixture.Renderer.SetPaused(true);fixture.Renderer.UpdateVoices(1);fixture.Renderer.SetPaused(false);fixture.Render();
			Assert.That(fixture.Source(ball),Is.SameAs(source));Assert.That(fixture.Renderer.LoopStarts,Is.EqualTo(starts));
			fixture.Set(ball,0);fixture.Renderer.Capture();fixture.Renderer.Apply();fixture.Renderer.UpdateVoices(.25f);
			Assert.That(source.volume,Is.EqualTo(.75f).Within(.00001));fixture.Renderer.UpdateVoices(1);Assert.That(fixture.Source(ball),Is.Null);
		}

		[TestCase(.2f)]
		[TestCase(.7f)]
		public void ReturningDuringACrossfadeReusesTheOriginalLoop(float returnAfter)
		{
			using var fixture = new AudioFixture();
			var from = fixture.Surface(fixture.Bank("wood", 1), 1);
			var to = fixture.Surface(fixture.Bank("plastic", 1), 1);
			fixture.Material(from).Responses[0].AttackSeconds = 1;
			fixture.Material(to).Responses[0].AttackSeconds = 1;
			fixture.Material(to).Responses[0].CrossfadeSeconds = 1;
			var ball = fixture.Ball(); fixture.Set(ball, from); fixture.Render();
			var original = fixture.Source(ball);
			fixture.Set(ball, to); fixture.Apply(); fixture.Renderer.UpdateVoices(returnAfter);
			Assert.That(fixture.Renderer.LoopStarts, Is.EqualTo(2));
			fixture.Set(ball, from); fixture.Apply();
			Assert.That(fixture.Source(ball), Is.SameAs(original), "reuse A even when B has become louder");
			// Complete B's de-click before another physics snapshot can cancel a stale request.
			fixture.Renderer.UpdateVoices(.01f);
			Assert.That(fixture.Source(ball), Is.SameAs(original));
			Assert.That(fixture.Renderer.LoopStarts, Is.EqualTo(2), "the restored loop must not restart");
			Assert.That(fixture.Renderer.GetComponentsInChildren<AudioSource>(), Has.Length.EqualTo(1));
		}

		[TestCase(.1f)]
		[TestCase(.5f)]
		public void AttackAndReleaseDurationsDoNotDependOnTheBankVolume(float volume)
		{
			using var fixture = new AudioFixture();
			var surface = fixture.Surface(fixture.Bank("quiet", volume), 1);
			var response = fixture.Material(surface).Responses[0];
			response.AttackSeconds = response.ReleaseSeconds = 1;
			var ball = fixture.Ball(); fixture.Set(ball, surface); fixture.Apply();
			fixture.Renderer.UpdateVoices(.25f);
			Assert.That(fixture.Source(ball).volume, Is.EqualTo(volume * .25f).Within(.00001));
			fixture.Apply(); fixture.Renderer.UpdateVoices(.25f);
			Assert.That(fixture.Source(ball).volume, Is.EqualTo(volume * .5f).Within(.00001), "an unchanged target must not restart its envelope");
			fixture.Renderer.UpdateVoices(.5f);
			var source = fixture.Source(ball);
			fixture.Set(ball, 0); fixture.Apply(); fixture.Renderer.UpdateVoices(.25f);
			Assert.That(fixture.Source(ball), Is.SameAs(source));
			Assert.That(source.volume, Is.EqualTo(volume * .75f).Within(.00001));
			fixture.Apply(); fixture.Renderer.UpdateVoices(.25f);
			Assert.That(source.volume, Is.EqualTo(volume * .5f).Within(.00001));
			fixture.Renderer.UpdateVoices(.5f);
			Assert.That(fixture.Source(ball), Is.Null);
		}

		[Test]
		public void QuietOutgoingLoopUsesTheFullCrossfadeDuration()
		{
			using var fixture = new AudioFixture();
			var from = fixture.Surface(fixture.Bank("quiet wood", .1f), 1);
			var to = fixture.Surface(fixture.Bank("plastic", .2f), 1);
			fixture.Material(to).Responses[0].CrossfadeSeconds = 1;
			var ball = fixture.Ball(); fixture.Set(ball, from); fixture.Render(); var outgoing = fixture.Source(ball);
			fixture.Set(ball, to); fixture.Apply(); fixture.Renderer.UpdateVoices(.25f);
			Assert.That(outgoing, Is.Not.Null);
			Assert.That(outgoing.volume, Is.EqualTo(.075f).Within(.00001));
			fixture.Apply(); fixture.Renderer.UpdateVoices(.25f);
			Assert.That(outgoing.volume, Is.EqualTo(.05f).Within(.00001));
			fixture.Renderer.UpdateVoices(.5f);
			Assert.That(fixture.Renderer.GetComponentsInChildren<AudioSource>(), Has.Length.EqualTo(1));
		}

		[Test]
		public void ReturningToASlotPoolDoesNotReuseAVoiceForItsPreviousSlot()
		{
			using var fixture = new AudioFixture(); var bank = fixture.Bank("ramps", 1, 2);
			var ramp = fixture.Surface(bank, 1, true); var wood = fixture.Surface(fixture.Bank("wood", 1), 1);
			fixture.Material(wood).Responses[0].AttackSeconds = fixture.Material(wood).Responses[0].CrossfadeSeconds = 1;
			var first = fixture.Ball(); fixture.Set(first, ramp); fixture.Render();
			Assert.That(fixture.Source(first).clip, Is.SameAs(bank.Clips[0]));
			fixture.Set(first, wood); fixture.Apply(); fixture.Renderer.UpdateVoices(.1f);
			var second = fixture.Ball(); fixture.Set(second, ramp); fixture.Apply(); fixture.Renderer.UpdateVoices(.1f);
			Assert.That(fixture.Source(second).clip, Is.SameAs(bank.Clips[0]));
			fixture.Set(first, ramp); fixture.Apply(); fixture.Renderer.UpdateVoices(.001f);
			fixture.Apply(); fixture.Renderer.UpdateVoices(.01f);
			Assert.That(fixture.Source(first).clip, Is.SameAs(bank.Clips[1]), "slot zero now belongs to the other ball");
			Assert.That(fixture.Renderer.LoopStarts, Is.EqualTo(4));
		}

		private sealed class AudioFixture : IDisposable
		{
			private readonly GameObject _root = new("audio review fixture");
			private readonly List<Object> _assets = new();
			private readonly BallSoundMaterialAsset _tag;
			internal readonly PhysicsEngineContext Context = new();
			internal readonly RollingSoundComponent Renderer;
			private readonly float _oldGain;
			internal AudioFixture() { _oldGain = MechanicalSoundGain.Gain; MechanicalSoundGain.Gain = 1; _tag = ScriptableObject.CreateInstance<BallSoundMaterialAsset>(); _assets.Add(_tag); Renderer = _root.AddComponent<RollingSoundComponent>(); Renderer.DefaultBallMaterial = _tag; Renderer.PlayfieldVolume = 1; Renderer.Prepare(Context); }
			internal SoundEffectAsset Bank(string name, float volume, int count = 1, bool loop = true) {
				var bank = ScriptableObject.CreateInstance<SoundEffectAsset>(); bank.name = name; _assets.Add(bank);
				var clips = new List<AudioClip>(); for(var i=0;i<count;i++) { var clip = AudioClip.Create(name+i,44100,1,44100,false); clips.Add(clip); _assets.Add(clip); } bank.Clips = clips.ToArray();
				var data = new SerializedObject(bank); data.FindProperty("_loop").boolValue = loop; data.FindProperty("_volumeRange").vector2Value = new Vector2(volume,volume); data.ApplyModifiedPropertiesWithoutUndo(); return bank;
			}
			internal int Surface(SoundEffectAsset bank, float gain, bool slots = false) { var material = ScriptableObject.CreateInstance<RollingSoundMaterialAsset>(); _assets.Add(material); material.VariantMode = slots ? RollingVariantMode.FirstFreeSlot : RollingVariantMode.Random; material.SlotPool = "Ramps"; material.Responses = new[] { new RollingSoundResponse { BallMaterial = _tag, Loop = bank, GainFactor = gain, GainExponent = 0 } }; var go = new GameObject("surface"); go.transform.SetParent(_root.transform,false); var setting = go.AddComponent<RollingSoundSurfaceComponent>(); setting.Mode = RollingSurfaceMode.Override; setting.Material = material; return RollingSoundComponent.SurfaceId(setting,null); }
			internal RollingSoundMaterialAsset Material(int id) => (RollingSoundMaterialAsset)((System.Collections.IList)typeof(RollingSoundComponent).GetField("_materials",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Renderer))[id];
			internal int Ball() { var go = new GameObject("ball"); go.transform.SetParent(_root.transform,false); var ball = go.AddComponent<BallComponent>(); Context.BallComponents[ball.Id] = ball; Context.RollingGenerations[ball.Id] = 1; return ball.Id; }
			internal void Set(int ball, int surface) => Context.Rolling.Balls[ball] = new RollingBallState { BallId = ball, Generation = 1, Surface = surface, ReservedSurface = surface, Supported = surface != 0, ReservedSupport = surface != 0, Speed = 10, Velocity = new float3(10,0,0) };
			internal void Apply() { Renderer.Capture(); Renderer.Apply(); }
			internal void Render(int firstBall = 0) { Renderer.Capture(); if(firstBall!=0) { var list=(System.Collections.IList)typeof(RollingSoundComponent).GetField("_staged",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Renderer); if((int)list[0].GetType().GetField("Ball").GetValue(list[0])!=firstBall) list.GetType().GetMethod("Reverse",Type.EmptyTypes).Invoke(list,null); } Renderer.Apply(); Renderer.UpdateVoices(1); }
			internal AudioSource Source(int ball) { var sessions=(System.Collections.IDictionary)typeof(RollingSoundComponent).GetField("_sessions",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(Renderer); var session=sessions[ball]; var voice=session.GetType().GetField("Active").GetValue(session); return voice==null ? null : (AudioSource)voice.GetType().GetField("Source").GetValue(voice); }
			public void Dispose() { Object.DestroyImmediate(_root); Context.Rolling.Dispose(); foreach(var asset in _assets) Object.DestroyImmediate(asset); MechanicalSoundGain.Gain = _oldGain; }
		}
	}
}
