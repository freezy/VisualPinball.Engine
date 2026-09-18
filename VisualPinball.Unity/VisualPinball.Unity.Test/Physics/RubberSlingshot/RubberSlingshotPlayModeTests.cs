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
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// Section 6.3 of the static rebound plan, play-mode bullet: a three-post guided
	/// rubber with a physical slingshot on one of its free spans, shot at once
	/// self-wired, once wired through the player's coil and once with no wire at all.
	/// </summary>
	/// <remarks>
	/// A two-post loop is ambiguous to the binding resolver by design, so the fixture
	/// uses three posts. The self-wired variant asserts the exact kick tick, in
	/// simulation time: the coil energizes exactly <c>WireDelayMs</c> after the switch
	/// closed, which is the tick the kick is applied on.
	/// </remarks>
	public class RubberSlingshotPlayModeTests
	{
		/// <summary>
		/// The HDRP test project logs errors that are not this fixture's: the default
		/// gamelogic engine dereferences the keyboard, which is null in a headless run,
		/// and its open scene carries a wire guide with a degenerate mesh. Both are
		/// recorded in UNITY_TEST_SUITE_FAILURES_HANDOFF.md; until they are fixed, the
		/// tests here assert on measured values and ignore failing log messages. The
		/// flag does not survive the domain reload of a play-mode transition, so it is
		/// set again after every one.
		/// </summary>
		private static void IgnoreHeadlessLogErrors() => LogAssert.ignoreFailingMessages = true;

		[UnityTearDown]
		public IEnumerator LeavePlayModeAfterEachTest()
		{
			IgnoreHeadlessLogErrors();
			if (Application.isPlaying) {
				yield return new ExitPlayMode();
			}
		}

		[UnityTest]
		public IEnumerator SelfWiredSlingshotShouldKickOnTheWireDelayTick()
		{
			IgnoreHeadlessLogErrors();
			yield return new EnterPlayMode();
			IgnoreHeadlessLogErrors();
			var fixture = Fixture.Create(selfWired: true);
			try {
				yield return fixture.Activate();
				Assert.That(fixture.Slingshot.SlingshotApi, Is.Not.Null,
					"the slingshot must register its api in play mode");
				Assert.That(fixture.SpanId, Is.GreaterThanOrEqualTo(0),
					"the slingshot must bind to a dense span id");

				yield return fixture.Shoot(25f);

				var api = fixture.Slingshot.SlingshotApi;
				TestContext.Out.WriteLine(
					$"selfWired|close={api.LastSwitchCloseSimulationTimeMs}|energize={api.LastCoilEnergizedSimulationTimeMs}|peak={fixture.PeakSpeed}");
				Assert.That(api.LastSwitchCloseSimulationTimeMs, Is.Not.NaN,
					"the shot must close the blade switch");
				Assert.That(api.CoilEnergizeCount, Is.EqualTo(1));
				Assert.That(api.LastCoilEnergizedSimulationTimeMs
						- api.LastSwitchCloseSimulationTimeMs,
					Is.EqualTo(fixture.Slingshot.WireDelayMs).Within(1.001f),
					"the kick tick is the closure plus the wire delay");
			} finally {
				fixture.Dispose();
			}
			yield return new ExitPlayMode();
		}

		[UnityTest]
		public IEnumerator AKickedBallShouldLeaveFasterThanAPassiveRebound()
		{
			IgnoreHeadlessLogErrors();
			yield return new EnterPlayMode();
			IgnoreHeadlessLogErrors();
			float passive;
			var passiveFixture = Fixture.Create(selfWired: false);
			try {
				yield return passiveFixture.Activate();
				yield return passiveFixture.Shoot(25f);
				passive = passiveFixture.PeakSpeed;
				Assert.That(passiveFixture.Slingshot.SlingshotApi.CoilEnergizeCount, Is.Zero,
					"without a wire the coil must stay cold");
			} finally {
				passiveFixture.Dispose();
			}

			float kicked;
			var kickedFixture = Fixture.Create(selfWired: true);
			try {
				yield return kickedFixture.Activate();
				yield return kickedFixture.Shoot(25f);
				kicked = kickedFixture.PeakSpeed;
				Assert.That(kickedFixture.Slingshot.SlingshotApi.CoilEnergizeCount,
					Is.EqualTo(1));
			} finally {
				kickedFixture.Dispose();
			}

			TestContext.Out.WriteLine($"speeds|passive={passive}|kicked={kicked}");
			Assert.That(kicked, Is.GreaterThan(passive * 1.1f),
				"the kicked ball must leave clearly faster than the passive rebound");
			yield return new ExitPlayMode();
		}

		[UnityTest]
		public IEnumerator APlayerWiredSlingshotShouldKickWithTheSelfWireOff()
		{
			IgnoreHeadlessLogErrors();
			yield return new EnterPlayMode();
			IgnoreHeadlessLogErrors();
			var fixture = Fixture.Create(selfWired: false);
			try {
				yield return fixture.Activate();
				var api = fixture.Slingshot.SlingshotApi;

				// the player's wiring: the blade switch drives the actuator coil
				api.BladeSwitch.Switch += (_, args) => api.ActuatorCoil.OnCoil(args.IsEnabled);

				yield return fixture.Shoot(25f);

				TestContext.Out.WriteLine(
					$"playerWired|energized={api.CoilEnergizeCount}|peak={fixture.PeakSpeed}");
				Assert.That(api.LastSwitchCloseSimulationTimeMs, Is.Not.NaN);
				Assert.That(api.CoilEnergizeCount, Is.GreaterThanOrEqualTo(1),
					"a wired coil must energize");
			} finally {
				fixture.Dispose();
			}
			yield return new ExitPlayMode();
		}

		#region Fixture

		/// <summary>
		/// A minimal table: a playfield, a three-post guided Physical rubber with a
		/// current Deformable bake, and one slingshot on its first free span.
		/// </summary>
		private sealed class Fixture : IDisposable
		{
			private const float Stretch = 1.1f;
			private const int Thickness = 8;

			/// <summary>The cord sits at the resting ball's centre height.</summary>
			private const float CordHeightVpx = 25f;

			internal GameObject Root;
			internal Player Player;
			internal PhysicsEngine PhysicsEngine;
			internal RubberComponent Rubber;
			internal RubberSlingshotComponent Slingshot;
			internal SlingshotActuatorAsset Actuator;
			internal GameObject BallPrefab;
			internal int SpanId = -1;

			/// <summary>Fastest the ball moved during the shot, in metres per second.</summary>
			internal float PeakSpeed;

			private float3 _anchorA;
			private float3 _anchorB;
			private float3 _outward;
			private GameObject _ball;

			internal static Fixture Create(bool selfWired)
			{
				var fixture = new Fixture();
				fixture.Root = new GameObject("Slingshot Play Mode Fixture");
				fixture.Root.SetActive(false);
				fixture.Root.AddComponent<TableComponent>();
				// the default engine is only there so the player wires up its switch,
				// coil and lamp players; its Update() needs resources and an input
				// device this batch-mode project does not have, so it stays disabled
				fixture.Root.AddComponent<DefaultGamelogicEngine>().enabled = false;
				fixture.Player = fixture.Root.AddComponent<Player>();
				fixture.PhysicsEngine = fixture.Root.AddComponent<PhysicsEngine>();
				// run the physics on the main thread: batch mode has no input devices and
				// a frame-paced loop makes the shot reproducible
				var simulation = fixture.Root.AddComponent<Simulation.SimulationThreadComponent>();
				simulation.EnableSimulationThread = false;
				simulation.EnableNativeInput = false;
				simulation.RecordSimulationTrace = false;

				var playfieldObject = new GameObject("Playfield");
				playfieldObject.transform.SetParent(fixture.Root.transform, false);
				var playfield = playfieldObject.AddComponent<PlayfieldComponent>();
				playfield.GlassHeight = 500f;
				playfield.RenderSlope = 0f;
				// the floor, so the ball rolls at the cord's height instead of falling
				playfieldObject.AddComponent<PlayfieldColliderComponent>();

				// three posts in the middle of the playfield, at the ball's centre
				// height; the guided bake turns them into three straight free spans
				// joined by supported arcs
				var guides = new[] {
					Post(playfieldObject.transform, "Post A", Local(380f, 1160f)),
					Post(playfieldObject.transform, "Post B", Local(520f, 1160f)),
					Post(playfieldObject.transform, "Post C", Local(450f, 1290f)),
				};

				var rubberObject = new GameObject("Slingshot Rubber");
				rubberObject.transform.SetParent(playfieldObject.transform, false);
				rubberObject.transform.localPosition = Local(450f, 1220f);
				fixture.Rubber = rubberObject.AddComponent<RubberComponent>();
				fixture.Rubber._thickness = Thickness;
				var collider = rubberObject.AddComponent<RubberColliderComponent>();
				collider.Mode = RubberColliderMode.Physical;
				collider.DeformationEnabled = true;
				collider.Elasticity = 0.8f;
				collider.Friction = 0.3f;
				fixture.Rubber.SetGuideBindings(guides.Select(guide =>
					new RubberGuideBinding(guide, guide.Slots[0].Id)));
				Assert.That(RubberAutofit.TryBake(fixture.Rubber, out _, out var bakeError),
					Is.True, bakeError);
				ApplyDeformationBake(fixture.Rubber, collider);

				var element = fixture.Rubber.BakedPath
					.Select((path, index) => (path, index))
					.First(entry => entry.path.Type == RubberPathElementType.FreeSpan);

				fixture.Actuator = Actuators.Create();
				var slingshotObject = new GameObject("Slingshot");
				slingshotObject.transform.SetParent(playfieldObject.transform, false);
				fixture.Slingshot = slingshotObject.AddComponent<RubberSlingshotComponent>();
				fixture.Slingshot.Rubber = fixture.Rubber;
				fixture.Slingshot.Span = new RubberSpanBinding(
					fixture.Rubber.GuideBindings[element.path.StartBindingIndex],
					fixture.Rubber.GuideBindings[element.path.EndBindingIndex]);
				fixture.Slingshot.Actuator = fixture.Actuator;
				fixture.Slingshot.SelfWired = selfWired;
				fixture.Slingshot.SwitchZones = new[] {
					new SlingshotSwitchZone {
						Position01 = 0.35f, CloseDeflection = 3f, OpenDeflection = 1f,
						MinimumClosedTimeMs = 8f,
					},
					new SlingshotSwitchZone {
						Position01 = 0.65f, CloseDeflection = 3f, OpenDeflection = 1f,
						MinimumClosedTimeMs = 8f,
					},
				};
				Assert.That(fixture.Slingshot.ValidateConfiguration(), Is.Empty);

				var prefabHolder = new GameObject("Ball Prefab Holder");
				prefabHolder.transform.SetParent(fixture.Root.transform, false);
				prefabHolder.SetActive(false);
				fixture.BallPrefab = new GameObject("Test Ball Prefab");
				fixture.BallPrefab.transform.SetParent(prefabHolder.transform, false);
				fixture.BallPrefab.AddComponent<BallComponent>();
				return fixture;
			}

			internal IEnumerator Activate()
			{
				Player.OnBallCreated += OnBallCreated;
				Root.SetActive(true);
				yield return null;
				yield return null;
				Assert.That(PhysicsEngine.IsInitialized, Is.True);

				var rubberItemId = UnityObjectId.Get(Rubber.gameObject);
				Assert.That(Slingshot.TryResolveSpan(out var resolved, out var error), Is.True,
					error);
				Assert.That(PhysicsEngine.TryGetRubberSpanId(rubberItemId,
					resolved.PathElementIndex, out var spanId), Is.True,
					"the bound span must have got a dense id");
				SpanId = spanId;

				var state = PhysicsEngine.CreateState();
				Assert.That(state.RubberSpanRebounds.TryGetValue(spanId, out var span), Is.True);
				_anchorA = span.AnchorA;
				_anchorB = span.AnchorB;
				_outward = span.OutwardNormal;
			}

			/// <summary>
			/// Shoots a ball straight at the middle of the bound span, from outside, and
			/// lets the simulation run long enough for the wire, the coil and the kick.
			/// </summary>
			internal IEnumerator Shoot(float speed)
			{
				var midpoint = 0.5f * (_anchorA + _anchorB);
				var start = midpoint + _outward * 70f;
				var direction = -_outward;
				var angle = math.degrees(math.atan2(direction.x, -direction.y));
				var ballId = Player.BallManager.CreateBall(
					new DebugBallCreator(start.x, start.y, 0f, angle, speed), 25f, 1f, BallPrefab);
				Assert.That(ballId, Is.Not.Zero);
				TestContext.Out.WriteLine(
					$"geometry|A={_anchorA}|B={_anchorB}|out={_outward}|start={start}|angle={angle}|ball={ballId}");

				var previous = float3.zero;
				var hasPrevious = false;
				for (var frame = 0; frame < 120; frame++) {
					yield return null;
					if (!_ball) {
						continue;
					}
					var position = (float3)_ball.transform.position;
					if (hasPrevious && Time.deltaTime > 0f) {
						// the shot is the same in both variants, so the peak speed over
						// the whole flight is enough to see the kick
						var velocity = (position - previous) / Time.deltaTime;
						PeakSpeed = math.max(PeakSpeed, math.length(velocity));
					}
					previous = position;
					hasPrevious = true;
					if (frame % 20 == 0) {
						TestContext.Out.WriteLine(
							$"frame {frame}|world={position}|peak={PeakSpeed}|switch={Slingshot.SlingshotApi.IsSwitchClosed}");
					}
					if (Slingshot.SlingshotApi != null
						&& !float.IsNaN(Slingshot.SlingshotApi.LastCoilEnergizedSimulationTimeMs)
						&& frame > 40) {
						break;
					}
				}
			}

			private void OnBallCreated(object sender, BallEvent args) => _ball = args.Ball;

			public void Dispose()
			{
				if (Player) {
					Player.OnBallCreated -= OnBallCreated;
				}
				if (Root) {
					Object.DestroyImmediate(Root);
				}
				if (Actuator) {
					Object.DestroyImmediate(Actuator);
				}
			}

			/// <summary>Playfield-local Unity position of a VPX point at cord height.</summary>
			internal static Vector3 Local(float vpxX, float vpxY)
				=> new Vector3(VisualPinball.Unity.Physics.ScaleToWorld(vpxX),
					VisualPinball.Unity.Physics.ScaleToWorld(CordHeightVpx),
					-VisualPinball.Unity.Physics.ScaleToWorld(vpxY));

			private static RubberGuideComponent Post(Transform parent, string name,
				Vector3 localPosition)
			{
				var postObject = new GameObject(name);
				postObject.transform.SetParent(parent, false);
				postObject.transform.localPosition = localPosition;
				var guide = postObject.AddComponent<RubberGuideComponent>();
				guide.Slots = new[] { RubberGuideSlot.Create("Default", 0.012f) };
				return guide;
			}

			/// <summary>
			/// Which side of a span's tangent the loop interior is on, so the generated
			/// outward normal points away from the rubber.
			/// </summary>
			private static sbyte InwardSign(IReadOnlyList<RubberPathElement> path, int index)
			{
				var centroid = float2.zero;
				for (var i = 0; i < path.Count; i++) {
					centroid += path[i].Start;
				}
				centroid /= math.max(1, path.Count);
				var element = path[index];
				var tangent = math.normalize(element.End - element.Start);
				var left = new float2(-tangent.y, tangent.x);
				var midpoint = (element.Start + element.End) * 0.5f;
				return (sbyte)(math.dot(left, centroid - midpoint) >= 0f ? 1 : -1);
			}

			/// <summary>
			/// Writes a current Deformable bake straight onto the collider, with every
			/// free span rebound-eligible. The editor baker is exercised by its own
			/// suite; this fixture only needs the runtime side to be live.
			/// </summary>
			private static void ApplyDeformationBake(RubberComponent rubber,
				RubberColliderComponent collider)
			{
				var spans = new List<RubberSpanDescriptor>();
				var path = rubber.BakedPath;
				for (var i = 0; i < path.Count; i++) {
					if (path[i].Type != RubberPathElementType.FreeSpan) {
						continue;
					}
					spans.Add(new RubberSpanDescriptor {
						PathElementIndex = i,
						InstalledLength = path[i].Length,
						UnloadedLength = path[i].Length / Stretch,
						MaxDeflection = 60f,
						SurfaceFriction = 0.3f,
						InwardSign = InwardSign(path, i),
						SegmentCount = 8,
						ArcLengthFractions = new[] { 0f, 1f },
						ReboundEligible = true,
					});
				}
				Assert.That(spans, Is.Not.Empty, "the guided bake must produce free spans");

				var count = RubberCompiledDeformationMaterial.LutSampleCount;
				var stress = new float[count];
				var tangent = new float[count];
				var table = RubberSpanReboundFixture.StressTable(
					RubberSpanReboundFixture.DefaultCurve);
				for (var i = 0; i < count; i++) {
					stress[i] = (float)table[i];
					tangent[i] = 1f;
				}
				collider.ApplyDeformationBake(new RubberCompiledDeformationMaterial {
					CompilerVersion = RubberDeformationBakeFormat.CompilerVersion,
					LinearDensity = 0.1f,
					DampingRatio = 0.08f,
					SurfaceFriction = 0.3f,
					MaximumStretchRatio = 2f,
					StressLut = stress,
					TangentStiffnessLut = tangent,
				}, spans.ToArray(), Array.Empty<RubberBackedResponseDescriptor>(),
					RubberDeformationBakeOutcome.Deformable, "play mode fixture",
					RubberDeformationBakeFormat.BakeVersion, new string('a', 64));
			}
		}

		/// <summary>A minimal valid actuator asset, so the component validates.</summary>
		private static class Actuators
		{
			internal static SlingshotActuatorAsset Create()
			{
				var actuator = ScriptableObject.CreateInstance<SlingshotActuatorAsset>();
				actuator.CurrentSampleCount = 2;
				actuator.StrokeSampleCount = 2;
				actuator.MaximumCurrentAmps = 10f;
				actuator.ForceNewtonLut = new[] { 0f, 0f, 20f, 16f };
				actuator.FluxLinkageWeberTurnLut = new[] { 0f, 0f, 0.02f, 0.016f };
				Assert.That(actuator.ValidateData(), Is.Empty);
				return actuator;
			}
		}

		#endregion
	}
}
