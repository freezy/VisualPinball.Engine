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
		public void ALegacyRubberIsNotEligible()
		{
			var rubber = CreateRubber(ThreePosts);
			rubber.GetComponent<RubberColliderComponent>().Mode = RubberColliderMode.Legacy;

			Assert.That(RubberSlingshotAuthoring.IsEligibleRubber(rubber), Is.False);
			Assert.That(RubberSlingshotAuthoring.FindSpans(rubber), Is.Empty);
			Assert.That(RubberSlingshotAuthoring.DescribeMissing(rubber),
				Does.Contain("Physical"));
		}

		[Test]
		public void AddingPutsTheSlingshotOnTheRubberBoundToTheChosenSpan()
		{
			var rubber = CreateRubber(ThreePosts);
			var actuator = CreateActuator();
			var chosen = RubberSlingshotAuthoring.FindSpans(rubber)[1];

			var component = RubberSlingshotAuthoring.Add(rubber, chosen.Binding, actuator);

			Assert.That(component.gameObject, Is.SameAs(rubber.gameObject));
			Assert.That(component.Rubber, Is.SameAs(rubber));
			Assert.That(component.Actuator, Is.SameAs(actuator));
			Assert.That(component.TryResolveSpan(out var resolved, out var error), Is.True, error);
			Assert.That(resolved.PathElementIndex, Is.EqualTo(chosen.PathElementIndex));
			Assert.That(component.SelfWired, Is.EqualTo(RubberSlingshotComponent.DefaultSelfWired));
			Assert.That(component.KickSpeed, Is.EqualTo(RubberSlingshotComponent.DefaultKickSpeed));
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
			var actuator = CreateActuator();
			var spans = RubberSlingshotAuthoring.FindSpans(rubber);
			var first = RubberSlingshotAuthoring.Add(rubber, spans[0].Binding, actuator);

			var second = RubberSlingshotAuthoring.Add(rubber, spans[2].Binding, actuator);

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
			var slingshot = RubberSlingshotAuthoring.Add(rubber, spans[0].Binding, CreateActuator());

			RubberSlingshotAuthoring.Bind(slingshot, spans[1].Binding);

			Assert.That(slingshot.TryResolveSpan(out var resolved, out var error), Is.True, error);
			Assert.That(resolved.PathElementIndex, Is.EqualTo(spans[1].PathElementIndex));
			var after = RubberSlingshotAuthoring.FindSpans(rubber);
			Assert.That(after.Single(span => span.IsBound).PathElementIndex,
				Is.EqualTo(spans[1].PathElementIndex));
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
		public void TheHintNamesTheFirstMissingPrerequisite()
		{
			var rubber = CreateRubber(ThreePosts);
			var collider = rubber.GetComponent<RubberColliderComponent>();

			Assert.That(RubberSlingshotAuthoring.DescribeMissing(rubber),
				Does.Contain("rest length"), "no rest length yet");

			rubber.RestLength = 100f;
			Assert.That(RubberSlingshotAuthoring.DescribeMissing(rubber),
				Does.Contain("compliant rebound"), "rebound not enabled yet");

			collider.DeformationEnabled = true;
			Assert.That(RubberSlingshotAuthoring.DescribeMissing(rubber),
				Does.Contain("not been baked"), "rebound enabled but never baked");
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
			var collider = rubberObject.AddComponent<RubberColliderComponent>();
			collider.Mode = RubberColliderMode.Physical;
			rubber.SetGuideBindings(guides.Select(guide =>
				new RubberGuideBinding(guide, guide.Slots[0].Id)));
			Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError), Is.True, bakeError);
			return rubber;
		}

		private SlingshotActuatorAsset CreateActuator()
		{
			var actuator = ScriptableObject.CreateInstance<SlingshotActuatorAsset>();
			_assets.Add(actuator);
			return actuator;
		}

		#endregion
	}
}
