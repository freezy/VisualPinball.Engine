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
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The span enumeration and slingshot creation the physical slingshot wizard and the
	/// rubber's scene handles share.
	/// </summary>
	public class RubberSlingshotAuthoringTests
	{
		private readonly List<GameObject> _objects = new();
		private readonly List<Object> _assets = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var gameObject in _objects) {
				Object.DestroyImmediate(gameObject);
			}
			foreach (var asset in _assets) {
				Object.DestroyImmediate(asset);
			}
			_objects.Clear();
			_assets.Clear();
		}

		[Test]
		public void AThreePostRubberOffersEachSpanOnce()
		{
			var rubber = CreateRubber(ThreePosts);

			var spans = RubberSlingshotAuthoring.FindSpans(rubber);

			Assert.That(spans, Has.Count.EqualTo(3));
			Assert.That(spans.Select(span => span.PathElementIndex), Is.Unique);
			Assert.That(spans.All(span => !span.IsBound), Is.True);
			var labels = spans.Select(span => RubberSlingshotAuthoring.Label(span.Binding)).ToList();
			Assert.That(labels, Is.Unique);
			Assert.That(labels, Has.All.Contains("->"));
			Assert.That(RubberSlingshotAuthoring.FindUnboundSpans(rubber), Has.Count.EqualTo(3));
		}

		[Test]
		public void ATwoPostRubberOffersNoSpan()
		{
			var rubber = CreateRubber(TwoPosts);

			Assert.That(RubberSlingshotAuthoring.IsEligibleRubber(rubber), Is.True);
			Assert.That(RubberSlingshotAuthoring.FindSpans(rubber), Is.Empty);
		}

		[Test]
		public void ARubberOffItsGuidesIsNotEligible()
		{
			var rubber = CreateRubber(ThreePosts);
			Assert.That(RubberSlingshotAuthoring.DescribeMissing(rubber), Is.Null);

			rubber.DetachFromGuides();

			Assert.That(RubberSlingshotAuthoring.IsEligibleRubber(rubber), Is.False);
			Assert.That(RubberSlingshotAuthoring.FindSpans(rubber), Is.Empty);
			Assert.That(RubberSlingshotAuthoring.DescribeMissing(rubber),
				Does.Contain("guides"));
		}

		[Test]
		public void AddingPutsTheSlingshotOnTheRubberBoundToTheChosenSpan()
		{
			var rubber = CreateRubber(ThreePosts);
			var chosen = RubberSlingshotAuthoring.FindSpans(rubber)[1];

			var component = RubberSlingshotAuthoring.Add(rubber, chosen.Binding);

			Assert.That(component.gameObject, Is.SameAs(rubber.gameObject));
			Assert.That(component.Rubber, Is.SameAs(rubber));
			Assert.That(component.TryResolveSpan(out var resolved, out var error), Is.True, error);
			Assert.That(resolved.PathElementIndex, Is.EqualTo(chosen.PathElementIndex));
			Assert.That(component.SelfWired, Is.EqualTo(RubberSlingshotComponent.DefaultSelfWired));
			Assert.That(component.SelfWirePulseMs,
				Is.EqualTo(RubberSlingshotComponent.DefaultSelfWirePulseMs));
			Assert.That(component.ValidateConfiguration(), Is.Empty);
			Assert.That(RubberSlingshotAuthoring.SlingshotOf(rubber), Is.SameAs(component));

			var spans = RubberSlingshotAuthoring.FindSpans(rubber);
			Assert.That(spans, Has.Count.EqualTo(3), "every span stays listed");
			var bound = spans.Single(span => span.PathElementIndex == chosen.PathElementIndex);
			Assert.That(bound.Existing, Is.SameAs(component));
			Assert.That(spans.Count(span => span.IsBound), Is.EqualTo(1));
			Assert.That(RubberSlingshotAuthoring.FindUnboundSpans(rubber), Is.Empty,
				"a rubber has at most one slingshot");
		}

		[Test]
		public void AddingAgainRebindsTheExistingSlingshot()
		{
			var rubber = CreateRubber(ThreePosts);
			var spans = RubberSlingshotAuthoring.FindSpans(rubber);
			var first = RubberSlingshotAuthoring.Add(rubber, spans[0].Binding);

			var second = RubberSlingshotAuthoring.Add(rubber, spans[2].Binding);

			Assert.That(second, Is.SameAs(first));
			Assert.That(rubber.GetComponents<RubberSlingshotComponent>(), Has.Length.EqualTo(1));
			Assert.That(first.TryResolveSpan(out var resolved, out var error), Is.True, error);
			Assert.That(resolved.PathElementIndex, Is.EqualTo(spans[2].PathElementIndex));
		}

		[Test]
		public void BindingMovesTheSlingshotToAnotherSpan()
		{
			var rubber = CreateRubber(ThreePosts);
			var spans = RubberSlingshotAuthoring.FindSpans(rubber);
			var slingshot = RubberSlingshotAuthoring.Add(rubber, spans[0].Binding);
			var wall = slingshot.SlingshotWall;

			RubberSlingshotAuthoring.Bind(slingshot, spans[1].Binding);

			Assert.That(slingshot.TryResolveSpan(out var resolved, out var error), Is.True, error);
			Assert.That(resolved.PathElementIndex, Is.EqualTo(spans[1].PathElementIndex));
			Assert.That(slingshot.SlingshotWall, Is.SameAs(wall), "the wall is refitted, not replaced");
			AssertWallFacesOutOfTheSpan(slingshot);
			var after = RubberSlingshotAuthoring.FindSpans(rubber);
			Assert.That(after.Single(span => span.IsBound).PathElementIndex,
				Is.EqualTo(spans[1].PathElementIndex));
		}

		[Test]
		public void ThePreviewPosesTheArmThroughItsMotionControllerAndPutsItBack()
		{
			var rubber = CreateRubber(ThreePosts);
			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding);
			var controllerObject = new GameObject("Arm Motion Controller");
			_objects.Add(controllerObject);
			controllerObject.SetActive(false);
			var controller = controllerObject.AddComponent<MotionControllerComponent>();
			var arm = new GameObject("Arm");
			_objects.Add(arm);
			arm.SetActive(false);
			arm.transform.localEulerAngles = new Vector3(0f, 0f, 5f);
			var follower = arm.AddComponent<MotionTransformComponent>();
			follower._emitter = controller;
			follower.AnimatePosition = false;
			follower.AnimateRotation = true;
			follower.RotationOffset = new Vector3(0f, 0f, 20f);
			slingshot.MotionController = controller;

			slingshot.Preview(0.5f);
			Assert.That(slingshot.IsPreviewing, Is.True);
			Assert.That(arm.transform.localEulerAngles.z, Is.EqualTo(15f).Within(1e-3f),
				"half way along the follower's rotation offset");

			slingshot.Preview(1f);
			Assert.That(arm.transform.localEulerAngles.z, Is.EqualTo(25f).Within(1e-3f));

			slingshot.Preview(0f);
			Assert.That(slingshot.IsPreviewing, Is.False);
			Assert.That(arm.transform.localEulerAngles.z, Is.EqualTo(5f).Within(1e-3f),
				"the arm is back where it was authored");
		}

		[Test]
		public void ThePreviewFindsAFollowerThatResolvesItsControllerAtRuntime()
		{
			var rubber = CreateRubber(ThreePosts);
			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding);
			// controller and follower on the same object, emitter left unassigned, as the
			// runtime resolves it through the hierarchy
			var arm = new GameObject("Arm");
			_objects.Add(arm);
			arm.SetActive(false);
			var controller = arm.AddComponent<MotionControllerComponent>();
			var follower = arm.AddComponent<MotionTransformComponent>();
			follower.AnimatePosition = false;
			follower.AnimateRotation = true;
			follower.RotationOffset = new Vector3(0f, 0f, 20f);
			slingshot.MotionController = controller;

			slingshot.Preview(1f);
			Assert.That(arm.transform.localEulerAngles.z, Is.EqualTo(20f).Within(1e-3f));

			slingshot.Preview(0f);
			Assert.That(arm.transform.localEulerAngles.z, Is.EqualTo(0f).Within(1e-3f));
		}

		[Test]
		public void AddingGivesTheRubberTheRendererThatAnimatesItsCord()
		{
			var rubber = CreateRubber(ThreePosts);

			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding);

			Assert.That(rubber.GetComponents<RubberDeformationRenderer>(), Has.Length.EqualTo(1));
			Assert.That(slingshot.DeformationRenderer, Is.Not.Null);
		}

		[Test]
		public void SettingUpTheArmSwingsItsTipOutThroughTheCord()
		{
			var rubber = CreateRubber(ThreePosts);
			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding);
			Assert.That(slingshot.TryResolveSpan(out var span, out var error), Is.True, error);
			var contact01 = slingshot.ArmContactPosition01;
			var contact = span.PointAt(contact01);
			var inward = span.InwardAt(contact01);
			var tipRest = ToWorld(rubber, contact + inward * slingshot.ArmRestClearance);
			var tipFired = ToWorld(rubber,
				contact - inward * (slingshot.ArmTipTravel - slingshot.ArmRestClearance));
			var outward = (tipFired - tipRest).normalized;

			// the pivot under the playfield, below the arm's rest point, and turned
			// arbitrarily, so the swing is not just an Euler angle of the world
			var arm = new GameObject("Arm");
			_objects.Add(arm);
			arm.SetActive(false);
			arm.transform.SetPositionAndRotation(tipRest + Vector3.down * 0.04f,
				Quaternion.Euler(0f, 160f, -5f));
			var tipLocal = arm.transform.InverseTransformPoint(tipRest);

			Assert.That(RubberSlingshotAuthoring.TrySetUpArm(slingshot, arm.transform,
				out error), Is.True, error);

			var controller = arm.GetComponent<MotionControllerComponent>();
			var follower = arm.GetComponent<MotionTransformComponent>();
			Assert.That(slingshot.MotionController, Is.SameAs(controller));
			Assert.That(controller.CoilMode, Is.EqualTo(MotionCoilMode.OneShot));
			Assert.That(follower._emitter, Is.SameAs(controller));
			Assert.That(follower.AnimateRotation, Is.True);
			Assert.That(follower.AnimatePosition, Is.False);

			follower.ApplyValue(1f);
			var tipMoved = arm.transform.TransformPoint(tipLocal);
			Assert.That(Vector3.Dot(tipMoved - tipRest, outward), Is.GreaterThan(0f),
				"the tip swings out, through the cord");
			Assert.That(Vector3.Angle(tipMoved - arm.transform.position,
					tipFired - arm.transform.position), Is.LessThan(0.5f),
				"the tip points at its full travel");
			Assert.That(Vector3.Dot(tipMoved - tipRest, (tipFired - tipRest).normalized),
				Is.EqualTo((tipFired - tipRest).magnitude).Within(0.1f * (tipFired - tipRest).magnitude),
				"and has travelled the arm's travel");

			follower.ApplyValue(0f);
			Assert.That(Vector3.Distance(arm.transform.TransformPoint(tipLocal), tipRest),
				Is.LessThan(1e-5f), "at rest the arm is where it was authored");
		}

		[Test]
		public void AnArmWhosePivotIsOnTheCordIsRefused()
		{
			var rubber = CreateRubber(ThreePosts);
			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding);
			Assert.That(slingshot.TryResolveSpan(out var span, out var error), Is.True, error);
			var arm = new GameObject("Arm");
			_objects.Add(arm);
			arm.transform.position = ToWorld(rubber, span.PointAt(slingshot.ArmContactPosition01));

			Assert.That(RubberSlingshotAuthoring.TrySetUpArm(slingshot, arm.transform,
				out error), Is.False);
			Assert.That(error, Does.Contain("pivot"));
			Assert.That(arm.GetComponent<MotionControllerComponent>(), Is.Null);
			Assert.That(slingshot.MotionController, Is.Null);
		}

		[Test]
		public void TheSameSpanIsRecognizedInEitherDirection()
		{
			var rubber = CreateRubber(ThreePosts);
			var span = RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding;
			var reversed = new RubberSpanBinding(span.EndSupport, span.StartSupport);

			Assert.That(RubberSlingshotAuthoring.SameBinding(span, reversed), Is.True);
			Assert.That(RubberSlingshotAuthoring.SameBinding(span,
				RubberSlingshotAuthoring.FindSpans(rubber)[1].Binding), Is.False);
		}

		[Test]
		public void AddingPutsASlingshotWallInFrontOfTheSpan()
		{
			var rubber = CreateRubber(ThreePosts);

			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[2].Binding);

			var wall = slingshot.SlingshotWall;
			Assert.That(wall, Is.Not.Null);
			Assert.That(wall.name, Is.EqualTo(rubber.name + RubberSlingshotAuthoring.SlingshotWallSuffix));
			Assert.That(wall.transform.parent, Is.SameAs(rubber.transform));
			Assert.That(wall.GetComponent<SurfaceColliderComponent>().HitEvent, Is.True,
				"the wall's slingshot event closes the switch");
			Assert.That(wall.GetComponents<MeshRenderer>(), Is.Empty, "the wall is not drawn");
			Assert.That(wall.HeightTop, Is.GreaterThan(wall.HeightBottom));
			AssertWallFacesOutOfTheSpan(slingshot);
		}

		[Test]
		public void EverySlingshotWallOfATableHasItsOwnName()
		{
			var table = new GameObject("Table");
			_objects.Add(table);
			var left = CreateRubber(ThreePosts);
			var right = CreateRubber(ThreePosts);
			left.transform.SetParent(table.transform, true);
			right.transform.SetParent(table.transform, true);

			var leftWall = RubberSlingshotAuthoring.Add(left,
				RubberSlingshotAuthoring.FindSpans(left)[0].Binding).SlingshotWall;
			var rightWall = RubberSlingshotAuthoring.Add(right,
				RubberSlingshotAuthoring.FindSpans(right)[0].Binding).SlingshotWall;

			Assert.That(left.name, Is.EqualTo(right.name), "the rubbers share their name");
			Assert.That(rightWall.name, Is.Not.EqualTo(leftWall.name),
				"table items are looked up by name");
		}

		[Test]
		public void TheWallFollowsTheRubbersRefit()
		{
			var rubber = CreateRubber(ThreePosts);
			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding);
			var guide = rubber.GuideBindings[0].Guide;
			guide.transform.position += new Vector3(0.02f, 0f, 0.01f);
			Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError), Is.True, bakeError);

			RubberSlingshotAuthoring.RefitSlingshotWallAfterBake(rubber, false);

			AssertWallFacesOutOfTheSpan(slingshot);
		}

		[Test]
		public void AScaledRubberWrapsItsGuidesAtItsRenderedThickness()
		{
			var rubber = CreateRubber(ThreePosts);
			rubber.transform.localScale = Vector3.one * 2f;

			Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError), Is.True, bakeError);

			var slotRadius = Physics.ScaleToVpx(0.01f);
			foreach (var arc in rubber.BakedPath.Where(element => element.Type == RubberPathElementType.SupportedArc)) {
				// the cord's centreline, in the world-sized bake plane, sits a rendered
				// half-thickness, twice the rubber's own, outside the slot
				Assert.That(arc.Radius, Is.EqualTo(slotRadius + rubber.Thickness).Within(0.01f));
			}
		}

		[Test]
		public void FittingTheWallAgainKeepsTheSameWall()
		{
			var rubber = CreateRubber(ThreePosts);
			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding);
			var wall = slingshot.SlingshotWall;

			Assert.That(RubberSlingshotAuthoring.TryFitSlingshotWall(slingshot, out var error),
				Is.True, error);

			Assert.That(slingshot.SlingshotWall, Is.SameAs(wall));
			Assert.That(rubber.GetComponentsInChildren<SurfaceComponent>(), Has.Length.EqualTo(1));
		}

		[Test]
		public void TheWallOfARoundedSpanClearsItsBulge()
		{
			var rubber = CreateRubber(ThreePosts);
			rubber.Roundness = 0.6f;
			Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError), Is.True, bakeError);
			var candidate = RubberSlingshotAuthoring.FindSpans(rubber)[0];
			Assert.That(RubberPathMath.IsStraightFreeSpan(candidate.Element), Is.False,
				"the span bulges");

			var slingshot = RubberSlingshotAuthoring.Add(rubber, candidate.Binding);

			AssertWallFacesOutOfTheSpan(slingshot);
		}

		[Test]
		public void AWallOfARubberWithoutPhysicsBouncesLikeARubber()
		{
			var rubber = CreateRubber(ThreePosts);
			var rubberCollider = rubber.GetComponent<RubberColliderComponent>();
			Assert.That(rubberCollider.PhysicsMaterial, Is.Null);
			Assert.That(rubberCollider.Elasticity, Is.Zero);

			var slingshot = RubberSlingshotAuthoring.Add(rubber,
				RubberSlingshotAuthoring.FindSpans(rubber)[0].Binding);

			var wallCollider = slingshot.SlingshotWall.GetComponent<SurfaceColliderComponent>();
			Assert.That(wallCollider.OverwritePhysics, Is.True);
			Assert.That(wallCollider.Elasticity, Is.EqualTo(0.8f),
				"the kick is the wall's bounce of the boosted ball");

			rubberCollider.Elasticity = 0.6f;
			Assert.That(RubberSlingshotAuthoring.TryFitSlingshotWall(slingshot, out var error),
				Is.True, error);
			Assert.That(wallCollider.Elasticity, Is.EqualTo(0.6f), "a rubber with physics lends them");
		}

		/// <summary>
		/// The wall's front edge is its one slingshot segment. It faces the ball, its
		/// normal being its direction turned left, and the cord, sampled along the span,
		/// lies at least a cord radius and the gap behind it, exactly that at the cord's
		/// outermost point.
		/// </summary>
		private static void AssertWallFacesOutOfTheSpan(RubberSlingshotComponent slingshot)
		{
			var rubber = slingshot.Rubber;
			var wall = slingshot.SlingshotWall;
			Assert.That(slingshot.TryResolveSpan(out var span, out var error), Is.True, error);
			var points = wall.DragPoints;
			Assert.That(points, Has.Length.EqualTo(4));
			Assert.That(points[0].IsSlingshot, Is.True);
			Assert.That(points.Skip(1).Any(point => point.IsSlingshot), Is.False);

			Vector2 Local(global::Unity.Mathematics.float2 point)
				=> ToWorld(rubber, point).TranslateToVpx(wall.transform);
			Vector2 Point(int index) => new(points[index].Center.X, points[index].Center.Y);
			var start = Point(0);
			var end = Point(1);
			var direction = end - start;
			var normal = new Vector2(-direction.y, direction.x).normalized;
			var inside = (Point(2) + Point(3)) * 0.5f;
			Assert.That(Vector2.Dot(normal, (start + end) * 0.5f - inside), Is.GreaterThan(0f),
				"the slingshot edge faces the ball");

			var expected = rubber.Thickness * 0.5f + RubberSlingshotAuthoring.SlingshotWallGap;
			var nearest = float.PositiveInfinity;
			for (var k = 0; k <= 64; k++) {
				var cord = Local(span.PointAt(k / 64f));
				var behind = Vector2.Dot(start - cord, normal);
				// the wall finds the bulge from fewer samples than this, which the gap absorbs
				Assert.That(behind, Is.GreaterThanOrEqualTo(expected - 0.05f),
					$"the cord at {k / 64f:F3} lies behind the slingshot edge");
				nearest = Mathf.Min(nearest, behind);
			}
			Assert.That(nearest, Is.EqualTo(expected).Within(0.05f),
				"the cord's outermost point lies just behind the slingshot edge");
		}

		#region Fixtures

		private static readonly Vector3[] ThreePosts = {
			new(-0.1f, 0f, -0.05f),
			new(0.1f, 0f, -0.05f),
			new(0f, 0f, 0.1f),
		};

		private static readonly Vector3[] TwoPosts = {
			new(-0.1f, 0f, 0f),
			new(0.1f, 0f, 0f),
		};

		private RubberComponent CreateRubber(IReadOnlyList<Vector3> postPositions)
		{
			var guides = new List<RubberGuideComponent>();
			for (var i = 0; i < postPositions.Count; i++) {
				var guideObject = new GameObject($"Guide {i}");
				_objects.Add(guideObject);
				guideObject.transform.position = postPositions[i];
				var guide = guideObject.AddComponent<RubberGuideComponent>();
				guide.Slots = new[] { RubberGuideSlot.Create("Default", 0.01f) };
				guides.Add(guide);
			}
			var rubberObject = new GameObject("Rubber");
			_objects.Add(rubberObject);
			var rubber = rubberObject.AddComponent<RubberComponent>();
			rubberObject.AddComponent<RubberColliderComponent>();
			rubber.SetGuideBindings(guides.Select(guide =>
				new RubberGuideBinding(guide, guide.Slots[0].Id)));
			Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError), Is.True, bakeError);
			return rubber;
		}

		private static Vector3 ToWorld(RubberComponent rubber, global::Unity.Mathematics.float2 point)
			=> rubber.BakeFrameToLocal.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f))
				.TranslateToWorld(rubber.transform);

		#endregion
	}
}
