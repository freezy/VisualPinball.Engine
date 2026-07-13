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

using NUnit.Framework;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.TestTools;
using VisualPinball.Engine.Math;
using VisualPinball.Unity.Editor;

namespace VisualPinball.Unity.Test
{
	public class DragPointSplineComponentTests
	{
		[Test]
		public void ShouldStoreRawVpxFloatsUnderTheBasisTransform()
		{
			var go = new GameObject("Rubber");
			try {
				go.transform.SetPositionAndRotation(new Vector3(2.5f, -1.25f, 4.75f),
					Quaternion.Euler(13f, 27f, -9f));
				go.transform.localScale = new Vector3(1.25f, 0.75f, 1.5f);
				var rubber = go.AddComponent<RubberComponent>();
				var dragPoints = CreateDragPoints();
				rubber.DragPoints = dragPoints;

				var spline = rubber.DragPointSpline.Container.Spline;
				for (var i = 0; i < spline.Count; i++) {
					Assert.That(spline[i].Position,
						Is.EqualTo(new float3(dragPoints[i].Center.X, dragPoints[i].Center.Y,
							dragPoints[i].Center.Z)));
					var expectedWorld = dragPoints[i].Center.ToUnityVector3()
						.TranslateToWorld(go.transform);
					var actualWorld = rubber.DragPointSpline.Container.transform
						.TransformPoint(spline[i].Position);
					Assert.That(Vector3.Distance(actualWorld, expectedWorld), Is.LessThan(1e-5f));
				}
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepMetadataAlignedWhenKnotsAreInsertedAndRemoved()
		{
			var go = new GameObject("Rubber");
			try {
				var rubber = go.AddComponent<RubberComponent>();
				rubber.DragPoints = CreateDragPoints();
				var dragPointSpline = rubber.DragPointSpline;
				var inheritedId = dragPointSpline.Metadata[1].Id;

				dragPointSpline.Container.Spline.Insert(1,
					new float3(-80f, 72f, 15f), TangentMode.Broken);

				Assert.That(dragPointSpline.Metadata.Count, Is.EqualTo(5));
				Assert.That(dragPointSpline.Metadata[1].IsSmooth,
					Is.EqualTo(dragPointSpline.Metadata[2].IsSmooth));
				Assert.That(dragPointSpline.Metadata[1].IsSlingshot,
					Is.EqualTo(dragPointSpline.Metadata[2].IsSlingshot));
				Assert.That(dragPointSpline.Metadata[1].Id, Is.Not.EqualTo(inheritedId));
				Assert.That(dragPointSpline.DragPoints[1].Center,
					Is.EqualTo(new Vertex3D(-80f, 72f, 0f)));

				dragPointSpline.Container.Spline.RemoveAt(1);
				Assert.That(dragPointSpline.Metadata.Count, Is.EqualTo(4));
				Assert.That(dragPointSpline.DragPoints[1].Id, Is.EqualTo(inheritedId));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldClampPlanarKnotEditsAndRestoreDerivedTangents()
		{
			var go = new GameObject("Surface");
			try {
				var surface = go.AddComponent<SurfaceComponent>();
				surface.DragPoints = CreateDragPoints();
				var spline = surface.DragPointSpline.Container.Spline;
				var knot = spline[2];
				knot.Position.z = 123f;
				knot.TangentIn = new float3(900f);
				knot.TangentOut = new float3(-900f);
				spline.SetKnot(2, knot);

				Assert.That(spline[2].Position.z, Is.Zero);
				Assert.That(math.length(spline[2].TangentIn), Is.LessThan(900f));
				Assert.That(math.length(spline[2].TangentOut), Is.LessThan(900f));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldMapRampEndpointZEditsToHeights()
		{
			var go = new GameObject("Ramp");
			try {
				var ramp = go.AddComponent<RampComponent>();
				ramp._heightBottom = 10f;
				ramp._heightTop = 80f;
				ramp.DragPoints = CreateDragPoints();
				var spline = ramp.DragPointSpline.Container.Spline;
				var bottomZ = spline[0].Position.z;
				var topZ = spline[^1].Position.z;

				var bottom = spline[0];
				bottom.Position.z += 12.5f;
				spline.SetKnot(0, bottom);
				Assert.That(ramp._heightBottom, Is.EqualTo(22.5f));
				Assert.That(spline[0].Position.z, Is.EqualTo(bottomZ));

				var top = spline[^1];
				top.Position.z -= 4.25f;
				spline.SetKnot(spline.Count - 1, top);
				Assert.That(ramp._heightTop, Is.EqualTo(75.75f));
				Assert.That(spline[^1].Position.z, Is.EqualTo(topZ));

				var middle = spline[1];
				middle.Position.z += 6f;
				spline.SetKnot(1, middle);
				Assert.That(spline[1].Position.z, Is.EqualTo(middle.Position.z));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldUpgradeTheTriggerDefaultArrayLazily()
		{
			var go = new GameObject("Trigger");
			try {
				var trigger = go.AddComponent<TriggerComponent>();
				Assert.That(trigger.DragPoints.Length, Is.EqualTo(4));
				Assert.That(trigger.DragPointSpline.Container.Spline.Closed, Is.True);
				Assert.That(trigger.DragPointSpline.transform.parent, Is.EqualTo(go.transform));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRebindTheGeneratedSplineWhenTheSerializedReferenceIsMissing()
		{
			var go = new GameObject("Rubber");
			try {
				var rubber = go.AddComponent<RubberComponent>();
				rubber.DragPoints = CreateDragPoints();
				var original = rubber.DragPointSpline;

				ClearSplineReference(rubber);

				Assert.That(rubber.DragPointSpline, Is.SameAs(original));
				Assert.That(go.GetComponentsInChildren<DragPointSplineComponent>(true),
					Has.Length.EqualTo(1));
				Assert.That(original.GetComponent<GeneratedDragPointSplineComponent>(), Is.Not.Null);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRemoveDuplicateGeneratedSplinesDeterministically()
		{
			var go = new GameObject("Rubber");
			try {
				var rubber = go.AddComponent<RubberComponent>();
				rubber.DragPoints = CreateDragPoints();
				var original = rubber.DragPointSpline;
				DragPointSplineComponent.Create(rubber, CreateDragPoints());
				ClearSplineReference(rubber);
				LogAssert.Expect(LogType.Warning,
					"Removing generated spline child 'Spline' from 'Rubber' because it duplicates another drag-point spline.");

				Assert.That(rubber.DragPointSpline, Is.SameAs(original));
				Assert.That(go.GetComponentsInChildren<DragPointSplineComponent>(true),
					Has.Length.EqualTo(1));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRemoveInvalidGeneratedChildrenWithoutSkippingTheSpline()
		{
			var go = new GameObject("Rubber");
			try {
				var rubber = go.AddComponent<RubberComponent>();
				rubber.DragPoints = CreateDragPoints();
				var original = rubber.DragPointSpline;
				var invalid = new GameObject("Invalid Spline");
				invalid.transform.SetParent(go.transform, false);
				invalid.transform.SetSiblingIndex(0);
				invalid.AddComponent<GeneratedDragPointSplineComponent>();
				ClearSplineReference(rubber);
				LogAssert.Expect(LogType.Warning,
					"Removing generated spline child 'Invalid Spline' from 'Rubber' because it has no functional drag-point spline.");

				Assert.That(rubber.DragPointSpline, Is.SameAs(original));
				Assert.That(go.GetComponentsInChildren<DragPointSplineComponent>(true),
					Has.Length.EqualTo(1));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldReverseAroundTheFirstKnotAndRotateSlingshots()
		{
			var go = new GameObject("Surface");
			try {
				var surface = go.AddComponent<SurfaceComponent>();
				surface.DragPoints = CreateDragPoints();

				DragPointSplineInspectorGUI.Reverse(surface.DragPointSpline);

				var dragPoints = surface.DragPoints;
				Assert.That(dragPoints, Has.Length.EqualTo(4));
				Assert.That(dragPoints[0].Id, Is.EqualTo("a"));
				Assert.That(dragPoints[1].Id, Is.EqualTo("d"));
				Assert.That(dragPoints[2].Id, Is.EqualTo("c"));
				Assert.That(dragPoints[3].Id, Is.EqualTo("b"));
				Assert.That(dragPoints[0].IsSlingshot, Is.True);
				Assert.That(dragPoints[1].IsSlingshot, Is.False);
				Assert.That(dragPoints[2].IsSlingshot, Is.True);
				Assert.That(dragPoints[3].IsSlingshot, Is.False);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldCenterTheOriginWithoutMovingTheWorldCurve()
		{
			var go = new GameObject("Rubber");
			try {
				var rubber = go.AddComponent<RubberComponent>();
				rubber.DragPoints = CreateDragPoints();
				var before = GetWorldPositions(rubber.DragPointSpline.Container);

				DragPointSplineInspectorGUI.CenterOrigin(rubber.DragPointSpline);

				var after = GetWorldPositions(rubber.DragPointSpline.Container);
				for (var i = 0; i < before.Length; i++) {
					Assert.That(Vector3.Distance(after[i], before[i]), Is.LessThan(1e-5f));
				}
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		private static Vector3[] GetWorldPositions(SplineContainer container)
		{
			var positions = new Vector3[container.Spline.Count];
			for (var i = 0; i < positions.Length; i++) {
				positions[i] = container.transform.TransformPoint(container.Spline[i].Position);
			}
			return positions;
		}

		private static void ClearSplineReference(RubberComponent rubber)
		{
			typeof(RubberComponent)
				.GetField("_dragPointSpline", BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(rubber, null);
		}

		private static DragPointData[] CreateDragPoints()
		{
			return new[] {
				CreateDragPoint("a", -120.25f, 15.5f, 1.25f, true, false),
				CreateDragPoint("b", -25.75f, 140.125f, 18.5f, false, true),
				CreateDragPoint("c", 95.375f, 80.625f, -7.75f, true, false),
				CreateDragPoint("d", 155.875f, -45.25f, 32.125f, true, true),
			};
		}

		private static DragPointData CreateDragPoint(string id, float x, float y, float z,
			bool smooth, bool slingshot)
		{
			return new DragPointData(new Vertex3D(x, y, z)) {
				Id = id,
				IsSmooth = smooth,
				IsSlingshot = slingshot,
				HasAutoTexture = false,
				TextureCoord = (id[0] - 'a') * 0.25f,
			};
		}
	}
}
