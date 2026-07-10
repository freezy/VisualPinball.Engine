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
using Unity.Mathematics;
using UnityEngine.Splines;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.Test
{
	public class DragPointSplineConverterTests
	{
		[TestCase(false)]
		[TestCase(true)]
		public void ShouldRoundTripDragPointsBitIdentically(bool loop)
		{
			var expected = CreateDragPoints();
			var spline = DragPointSplineConverter.ToSpline(expected, loop);
			var actual = DragPointSplineConverter.ToDragPoints(spline,
				DragPointSplineConverter.ToMetadata(expected));

			Assert.That(spline.Closed, Is.EqualTo(loop));
			Assert.That(actual.Length, Is.EqualTo(expected.Length));
			for (var i = 0; i < expected.Length; i++) {
				AssertDragPointEqual(expected[i], actual[i], i);
				Assert.That(spline.GetTangentMode(i), Is.EqualTo(TangentMode.Broken));
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ShouldMatchCentripetalCatmullRomSegments(bool loop)
		{
			var dragPoints = CreateDragPoints();
			var spline = DragPointSplineConverter.ToSpline(dragPoints, loop);
			var segmentCount = loop ? dragPoints.Length : dragPoints.Length - 1;
			for (var i = 0; i < segmentCount; i++) {
				var nextIndex = i < dragPoints.Length - 1 ? i + 1 : 0;
				var previousIndex = dragPoints[i].IsSmooth ? i - 1 : i;
				if (previousIndex < 0) {
					previousIndex = loop ? dragPoints.Length - 1 : 0;
				}

				var followingIndex = dragPoints[nextIndex].IsSmooth ? i + 2 : i + 1;
				if (followingIndex >= dragPoints.Length) {
					followingIndex = loop ? followingIndex - dragPoints.Length : dragPoints.Length - 1;
				}

				var catmull = CatmullCurve<RenderVertex3D>.GetInstance<
					CatmullCurve3DCatmullCurveFactory>(
					dragPoints[previousIndex].Center,
					dragPoints[i].Center,
					dragPoints[nextIndex].Center,
					dragPoints[followingIndex].Center);
				var bezier = spline.GetCurve(i);
				for (var sample = 0; sample <= 20; sample++) {
					var t = sample / 20f;
					var expected = catmull.GetPointAt(t);
					var actual = CurveUtility.EvaluatePosition(bezier, t);
					Assert.That(math.distance(actual,
						new float3(expected.X, expected.Y, expected.Z)), Is.LessThan(2e-5f),
						$"segment {i}, t={t}");
				}
			}
		}

		[Test]
		public void ShouldRestoreDerivedTangentsAfterSplineEdits()
		{
			var dragPoints = CreateDragPoints();
			var metadata = DragPointSplineConverter.ToMetadata(dragPoints);
			var spline = DragPointSplineConverter.ToSpline(dragPoints, true);
			var knot = spline[1];
			knot.Position += new float3(12.5f, -4.25f, 7.75f);
			knot.TangentIn = new float3(900f);
			knot.TangentOut = new float3(-900f);
			spline.SetKnot(1, knot);

			DragPointSplineConverter.RecalculateTangents(spline, metadata);
			var expected = DragPointSplineConverter.ToSpline(
				DragPointSplineConverter.ToDragPoints(spline, metadata), true);
			for (var i = 0; i < spline.Count; i++) {
				Assert.That(spline[i], Is.EqualTo(expected[i]));
			}
		}

		private static DragPointData[] CreateDragPoints()
		{
			return new[] {
				CreateDragPoint("a", -120.25f, 15.5f, 1.25f, true, false, 0.125f),
				CreateDragPoint("b", -25.75f, 140.125f, 18.5f, false, true, 0.375f),
				CreateDragPoint("c", 95.375f, 80.625f, -7.75f, true, false, 0.625f),
				CreateDragPoint("d", 155.875f, -45.25f, 32.125f, true, true, 0.875f),
			};
		}

		private static DragPointData CreateDragPoint(string id, float x, float y, float z,
			bool smooth, bool slingshot, float textureCoord)
		{
			return new DragPointData(new Vertex3D(x, y, z)) {
				Id = id,
				IsSmooth = smooth,
				IsSlingshot = slingshot,
				HasAutoTexture = false,
				TextureCoord = textureCoord,
				IsLocked = id == "c",
				EditorLayer = id[0],
				EditorLayerName = $"layer-{id}",
				EditorLayerVisibility = id != "b",
				CalcHeight = textureCoord * 100f,
			};
		}

		private static void AssertDragPointEqual(DragPointData expected, DragPointData actual,
			int index)
		{
			Assert.That(actual.Center, Is.EqualTo(expected.Center), $"point {index}, center");
			Assert.That(actual.IsSmooth, Is.EqualTo(expected.IsSmooth), $"point {index}, smooth");
			Assert.That(actual.IsSlingshot, Is.EqualTo(expected.IsSlingshot), $"point {index}, slingshot");
			Assert.That(actual.HasAutoTexture, Is.EqualTo(expected.HasAutoTexture), $"point {index}, auto texture");
			Assert.That(actual.TextureCoord, Is.EqualTo(expected.TextureCoord), $"point {index}, texture coordinate");
			Assert.That(actual.IsLocked, Is.EqualTo(expected.IsLocked), $"point {index}, locked");
			Assert.That(actual.EditorLayer, Is.EqualTo(expected.EditorLayer), $"point {index}, editor layer");
			Assert.That(actual.EditorLayerName, Is.EqualTo(expected.EditorLayerName), $"point {index}, editor layer name");
			Assert.That(actual.EditorLayerVisibility, Is.EqualTo(expected.EditorLayerVisibility),
				$"point {index}, editor layer visibility");
			Assert.That(actual.Id, Is.EqualTo(expected.Id), $"point {index}, id");
			Assert.That(actual.CalcHeight, Is.EqualTo(expected.CalcHeight), $"point {index}, calculated height");
		}
	}
}
