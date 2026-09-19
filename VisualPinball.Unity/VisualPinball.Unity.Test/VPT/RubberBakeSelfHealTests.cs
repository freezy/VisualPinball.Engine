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

namespace VisualPinball.Unity.Test
{
	/// <summary>
	/// The bakes stored on a guided Physical rubber are caches: whoever needs them
	/// brings them up to date, so the author never presses a bake button.
	/// </summary>
	public class RubberBakeSelfHealTests
	{
		private readonly List<Object> _objects = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var obj in _objects) {
				Object.DestroyImmediate(obj);
			}
			_objects.Clear();
		}

		[Test]
		public void AMovedGuideIsRefitWhenTheBakesAreEnsured()
		{
			var (rubber, collider, guides) = CreateRubber(withRebound: false);
			Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);

			guides[2].transform.position += new Vector3(0.02f, 0f, 0f);
			var status = RubberAutofit.GetStatus(rubber);
			Assert.That(status.IsValid, Is.False, "moving a guide makes the fit stale");
			Assert.That(status.IsStale, Is.True);

			Assert.That(collider.EnsureBakesCurrent(), Is.True);
			Assert.That(RubberAutofit.GetStatus(rubber).IsValid, Is.True);
		}

		[Test]
		public void AChangedReboundInputIsRebakedWhenTheBakesAreEnsured()
		{
			var (rubber, collider, _) = CreateRubber(withRebound: true);
			Assert.That(RubberDeformationBaker.IsBakeCurrent(collider), Is.False, "never baked");

			Assert.That(collider.EnsureBakesCurrent(), Is.True, collider.BakeReason);
			Assert.That(RubberDeformationBaker.IsBakeCurrent(collider), Is.True);
			Assert.That(collider.BakeOutcome, Is.EqualTo(RubberDeformationBakeOutcome.Deformable));
			var hash = collider.DeformationBakeHash;

			collider.DeflectionClearance += 5f;
			Assert.That(RubberDeformationBaker.IsBakeCurrent(collider), Is.False, "an input changed");

			Assert.That(collider.EnsureBakesCurrent(), Is.True, collider.BakeReason);
			Assert.That(RubberDeformationBaker.IsBakeCurrent(collider), Is.True);
			Assert.That(collider.DeformationBakeHash, Is.Not.EqualTo(hash));
		}

		[Test]
		public void EnsuringIsIdempotent()
		{
			var (_, collider, _) = CreateRubber(withRebound: true);
			Assert.That(collider.EnsureBakesCurrent(), Is.True, collider.BakeReason);
			var hash = collider.DeformationBakeHash;

			Assert.That(collider.EnsureBakesCurrent(), Is.True);
			Assert.That(collider.DeformationBakeHash, Is.EqualTo(hash));
		}

		[Test]
		public void ResolvingASpanRefitsAStaleGuidedPath()
		{
			var (rubber, _, guides) = CreateRubber(withRebound: false);
			var element = rubber.BakedPath.First(path => path.Type == RubberPathElementType.FreeSpan);
			var span = new RubberSpanBinding(
				rubber.GuideBindings[element.StartBindingIndex],
				rubber.GuideBindings[element.EndBindingIndex]);
			guides[2].transform.position += new Vector3(0.02f, 0f, 0f);
			Assert.That(rubber.HasValidGuidedPath, Is.False);

			Assert.That(RubberSpanResolver.TryResolve(rubber, span, out _, out var error), Is.True, error);
			Assert.That(rubber.HasValidGuidedPath, Is.True);
		}

		[Test]
		public void ARoundedRubberBakesDeformableOnEverySpan()
		{
			var (rubber, collider, _) = CreateRubber(withRebound: true);
			rubber.Roundness = 0.5f;
			Assert.That(RubberAutofit.TryBake(rubber, out _, out var error), Is.True, error);
			rubber.RestLength = rubber.BakedPath.Sum(element => element.Length) / 1.1f;
			Assert.That(rubber.BakedPath.Any(element => element.Type == RubberPathElementType.FreeSpan
				&& !RubberPathMath.IsStraightFreeSpan(element)), Is.True, "roundness bends the free spans");

			Assert.That(collider.EnsureBakesCurrent(), Is.True, collider.BakeReason);

			Assert.That(collider.BakeOutcome, Is.EqualTo(RubberDeformationBakeOutcome.Deformable));
			Assert.That(collider.SpanDescriptors.All(descriptor => descriptor.ReboundEligible), Is.True);
			Assert.That(RubberSpanReboundEligibility.EligibleDescriptors(collider), Has.Count.EqualTo(3));
		}

		[Test]
		public void ARubberWithoutRestLengthReportsWhyItCannotDeform()
		{
			var (rubber, collider, _) = CreateRubber(withRebound: true);
			rubber.RestLength = 0f;

			Assert.That(collider.EnsureBakesCurrent(), Is.False);
			Assert.That(RubberDeformationBaker.IsBakeCurrent(collider), Is.True, "a bake was still written");
			Assert.That(collider.BakeOutcome, Is.EqualTo(RubberDeformationBakeOutcome.StaticFallback));
			Assert.That(collider.BakeReason, Does.Contain("rest length"));
		}

		#region Fixtures

		private static readonly Vector3[] ThreePosts = {
			new(-0.1f, 0f, -0.05f),
			new(0.1f, 0f, -0.05f),
			new(0f, 0f, 0.1f),
		};

		private (RubberComponent, RubberColliderComponent, RubberGuideComponent[]) CreateRubber(bool withRebound)
		{
			var guides = new RubberGuideComponent[ThreePosts.Length];
			for (var i = 0; i < ThreePosts.Length; i++) {
				var guideObject = new GameObject($"Guide {i}");
				_objects.Add(guideObject);
				guideObject.transform.position = ThreePosts[i];
				guides[i] = guideObject.AddComponent<RubberGuideComponent>();
				guides[i].Slots = new[] { RubberGuideSlot.Create("Default", 0.01f) };
			}
			var rubberObject = new GameObject("Rubber");
			_objects.Add(rubberObject);
			var rubber = rubberObject.AddComponent<RubberComponent>();
			var collider = rubberObject.AddComponent<RubberColliderComponent>();
			collider.Mode = RubberColliderMode.Physical;
			rubber.SetGuideBindings(guides.Select(guide =>
				new RubberGuideBinding(guide, guide.Slots[0].Id)));
			Assert.That(RubberAutofit.TryBake(rubber, out _, out var bakeError), Is.True, bakeError);
			rubber.RestLength = rubber.BakedPath.Sum(element => element.Length) / 1.1f;
			if (withRebound) {
				var material = ScriptableObject.CreateInstance<RubberPhysicsMaterialAsset>();
				_objects.Add(material);
				collider.DeformationEnabled = true;
				collider.RubberPhysicsMaterial = material;
			}
			return (rubber, collider, guides);
		}

		#endregion
	}
}
