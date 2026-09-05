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
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class WireRailPackagingTests
	{
		[TearDown]
		public void TearDown()
		{
			Undo.ClearAll();
		}

		[Test]
		public void ShouldRoundTripEverySettingThroughThePackables()
		{
			var sourceGo = new GameObject("Wire Rail");
			var targetGo = new GameObject("Wire Rail Restored");
			var reversedGo = new GameObject("Wire Rail Restored (reversed order)");
			try {
				var source = Author(sourceGo.AddComponent<WireRailComponent>());
				var railBytes = source.Pack();
				var splineBytes = source.SplineContainer
					.GetComponent<WireRailSplineComponent>().Pack();

				// spline first, then the rail data
				var target = targetGo.AddComponent<WireRailComponent>();
				target.SplineContainer.GetComponent<WireRailSplineComponent>().Unpack(splineBytes);
				target.Unpack(railBytes);
				AssertRestored(source, target);

				// rail data first, then the spline: the rebuild must wait for the route
				var reversed = reversedGo.AddComponent<WireRailComponent>();
				reversed.Unpack(railBytes);
				reversed.SplineContainer.GetComponent<WireRailSplineComponent>().Unpack(splineBytes);
				AssertRestored(source, reversed);
			} finally {
				Object.DestroyImmediate(sourceGo);
				Object.DestroyImmediate(targetGo);
				Object.DestroyImmediate(reversedGo);
			}
		}

		// Frozen payloads as written by the version 1 format, for the rail that Author()
		// builds. When the format evolves, keep these files untouched and add the new version
		// alongside: they prove that tables packaged by earlier builds still load into the
		// same rail.
		private const string GoldenFolder =
			"Packages/org.visualpinball.engine.unity/VisualPinball.Unity/VisualPinball.Unity.Test/VPT/WireRail/Golden/";

		[Test]
		public void ShouldLoadVersion1Payloads()
		{
			var go = new GameObject("Wire Rail");
			try {
				var rail = go.AddComponent<WireRailComponent>();
				rail.SplineContainer.GetComponent<WireRailSplineComponent>()
					.Unpack(LoadGolden("wire-rail-spline-v1.json"));
				rail.Unpack(LoadGolden("wire-rail-v1.json"));
				AssertRestored(null, rail);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		private static byte[] LoadGolden(string fileName)
		{
			var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(GoldenFolder + fileName);
			Assert.That(asset, Is.Not.Null, $"golden payload {fileName} is missing");
			return asset.bytes;
		}

		[UnityTest]
		public IEnumerator ShouldSurviveAPackageRoundTripAndCollide()
		{
			var firstPackage = Path.Combine(Path.GetTempPath(),
				$"vpe-wire-rail-{Guid.NewGuid():N}.vpe");
			var secondPackage = Path.Combine(Path.GetTempPath(),
				$"vpe-wire-rail-{Guid.NewGuid():N}.vpe");
			GameObject source = null;
			GameObject imported = null;
			try {
				source = new GameObject("Table");
				source.AddComponent<TableComponent>();
				var railGo = new GameObject("Wire Rail");
				railGo.transform.SetParent(source.transform, false);
				var sourceRail = Author(railGo.AddComponent<WireRailComponent>());
				Assert.That(sourceRail.RenderMesh, Is.Not.Null);
				Assert.That(((ICollidableComponent)sourceRail).IsCollidable, Is.True);

				new PackageWriter(source).WritePackageSync(firstPackage);

				var importTask = new RuntimePackageReader(firstPackage).ImportIntoScene();
				while (!importTask.IsCompleted) {
					yield return null;
				}
				if (importTask.IsFaulted) {
					throw importTask.Exception!.GetBaseException();
				}
				imported = importTask.Result;

				var rails = imported.GetComponentsInChildren<WireRailComponent>(true);
				Assert.That(rails, Has.Length.EqualTo(1));
				var rail = rails[0];
				AssertRestored(sourceRail, rail);
				Assert.That(rail.transform.Cast<Transform>()
					.Count(child => child.GetComponent<SplineContainer>()), Is.EqualTo(1),
					"exactly one spline child, no duplicate created on import");
				Assert.That(rail.transform.childCount, Is.EqualTo(1));
				Assert.That(rail.SplineContainer.GetComponent<MeshRenderer>().sharedMaterial,
					Is.Not.Null, "the imported material stays on the generated mesh");
				Assert.That(((ICollidableComponent)rail).IsCollidable, Is.True,
					"a restored wire rail must produce its ball channel collider");
				Assert.That(rail.ColliderMesh.vertexCount, Is.GreaterThan(0));

				// and out again, unchanged
				new PackageWriter(imported).WritePackageSync(secondPackage);
			} finally {
				if (source) {
					Object.DestroyImmediate(source);
				}
				if (imported) {
					Object.DestroyImmediate(imported);
				}
				File.Delete(firstPackage);
				File.Delete(secondPackage);
			}
		}

		/// <summary>
		/// A rail that exercises every packed field: a curved three-knot route, three
		/// layouts with custom offsets, an inactive wire, transition overrides, every fixture
		/// kind, and non-default settings.
		/// </summary>
		private static WireRailComponent Author(WireRailComponent rail)
		{
			rail.SplineContainer.Spline.Add(new BezierKnot(new float3(120f, 900f, 40f)),
				TangentMode.AutoSmooth);
			rail.SetRailCount(5);
			rail.SetWireDiameter(7.5f);
			rail.SetShowColliderPreview(true);
			rail.AddLayout(200f);
			rail.AddLayout(400f);
			rail.SetRailOffset(1, 0, new Vector2(-21f, 3f));
			rail.SetRailsActive(1, new[] { 4 }, false);
			rail.SetWireTransitionOverride(0, 2, true);
			rail.SetWireContinuous(0, 2, false);
			rail.SetWireTransitionCurve(0, 1, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));

			var ring = rail.AddRingFixture(120f);
			rail.SetRingFixtureProperties(ring, 120f, true, 30f, 90f, true, 200f, 300f,
				2f, -3f, 1.4f, 24);
			rail.AddCradleFixture(150f);
			var rung = rail.AddRungFixture(180f);
			rail.SetFixtureSolderSize(rung, 1.5f);
			var stand = rail.AddStandFixture(220f);
			rail.SetFixtureEnabled(stand, false);
			var hairpin = rail.AddHairpinFixture();
			rail.SetHairpinFixtureOffset(hairpin, 12f);
			rail.AddElbowFixture(WireRailEndpoint.Start);
			var trim = rail.AddRailTrimFixture();
			rail.SetRailTrimFixtureProperties(trim, WireRailEndpoint.End,
				new[] { 0f, 0f, 0f, 25f, 0f });
			rail.RebuildRenderGeometry();
			return rail;
		}

		/// <summary>
		/// Checks a restored rail against what <see cref="Author"/> built. With a source the
		/// route and mesh are compared exactly; without one (golden payloads) against the
		/// known authored values.
		/// </summary>
		private static void AssertRestored(WireRailComponent source, WireRailComponent rail)
		{
			Assert.That(rail.SplineContainer, Is.Not.Null);
			var spline = rail.SplineContainer.Spline;
			if (source != null) {
				var sourceSpline = source.SplineContainer.Spline;
				Assert.That(spline.Count, Is.EqualTo(sourceSpline.Count));
				Assert.That(spline.Closed, Is.EqualTo(sourceSpline.Closed));
				for (var i = 0; i < spline.Count; i++) {
					Assert.That(math.distance(spline[i].Position, sourceSpline[i].Position),
						Is.LessThan(0.001f), $"knot {i} position");
					Assert.That(spline.GetTangentMode(i), Is.EqualTo(sourceSpline.GetTangentMode(i)));
				}
				Assert.That(rail.SplineLength, Is.EqualTo(source.SplineLength).Within(0.01f));
			} else {
				Assert.That(spline.Count, Is.EqualTo(3));
				Assert.That(spline.Closed, Is.False);
				Assert.That(math.distance(spline[2].Position, new float3(120f, 900f, 40f)),
					Is.LessThan(0.001f));
				Assert.That(rail.SplineLength, Is.GreaterThan(900f));
			}

			Assert.That(rail.RailCount, Is.EqualTo(5));
			Assert.That(rail.WireDiameter, Is.EqualTo(7.5f).Within(0.001f));
			Assert.That(rail.ShowColliderPreview, Is.True);
			Assert.That(rail.Segments.Select(layout => layout.Distance),
				Is.EqualTo(new[] { 0f, 200f, 400f }).Within(0.001f));
			Assert.That(rail.Segments[1].GetRailOffset(0), Is.EqualTo(new Vector2(-21f, 3f)));
			Assert.That(rail.Segments[1].IsRailActive(4), Is.False);
			Assert.That(rail.Segments[1].IsRailActive(0), Is.True);
			var connection = rail.Segments[0].ConnectionToNext;
			Assert.That(connection.IsWireOverridden(2), Is.True);
			Assert.That(connection.IsWireContinuous(2), Is.False);
			Assert.That(connection.IsWireOverridden(1), Is.True);
			Assert.That(connection.GetWireCurve(1).keys.Length,
				Is.EqualTo(AnimationCurve.EaseInOut(0f, 0f, 1f, 1f).keys.Length));
			Assert.That(rail.LayoutDisplayOrder, Is.EqualTo(new[] { 0, 1, 2 }));

			Assert.That(rail.Fixtures.Select(fixture => fixture.GetType()), Is.EqualTo(new[] {
				typeof(WireRailRingFixture), typeof(WireRailCradleFixture),
				typeof(WireRailRungFixture), typeof(WireRailStandFixture),
				typeof(WireRailHairpinFixture), typeof(WireRailElbowFixture),
				typeof(WireRailTrimFixture),
			}));
			var ring = (WireRailRingFixture)rail.Fixtures[0];
			Assert.That(ring.Distance, Is.EqualTo(120f).Within(0.001f));
			Assert.That(ring.HasCutout, Is.True);
			Assert.That(ring.CutoutEndAngle, Is.EqualTo(90f).Within(0.001f));
			Assert.That(ring.HasStraightSection, Is.True);
			Assert.That(ring.Scale, Is.EqualTo(1.4f).Within(0.001f));
			Assert.That(ring.RingDensity, Is.EqualTo(24));
			Assert.That(ring.LateralOffset, Is.EqualTo(2f).Within(0.001f));
			Assert.That(rail.Fixtures[2].SolderSize, Is.EqualTo(1.5f).Within(0.001f));
			Assert.That(rail.Fixtures[3].Enabled, Is.False);
			Assert.That(((WireRailHairpinFixture)rail.Fixtures[4]).RailOffset,
				Is.EqualTo(12f).Within(0.001f));
			Assert.That(((WireRailElbowFixture)rail.Fixtures[5]).Endpoint,
				Is.EqualTo(WireRailEndpoint.Start));
			Assert.That(((WireRailTrimFixture)rail.Fixtures[6]).RailOffsets[3],
				Is.EqualTo(25f).Within(0.001f));

			Assert.That(rail.RenderMesh, Is.Not.Null, "geometry is rebuilt once both halves are restored");
			if (source != null) {
				Assert.That(rail.RenderMesh.vertexCount, Is.EqualTo(source.RenderMesh.vertexCount));
			} else {
				Assert.That(rail.RenderMesh.vertexCount, Is.GreaterThan(0));
			}
		}
	}
}
