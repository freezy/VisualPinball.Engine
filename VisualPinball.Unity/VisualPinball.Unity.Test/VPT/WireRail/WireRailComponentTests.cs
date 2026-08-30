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
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace VisualPinball.Unity.Test
{
	public class WireRailComponentTests
	{
		[TearDown]
		public void TearDown()
		{
			Undo.ClearAll();
		}

		[Test]
		public void ShouldCreateAThreeDimensionalVpxSpline()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var container = component.SplineContainer;

				Assert.That(container.transform.parent, Is.EqualTo(go.transform));
				Assert.That(container.Spline.Count, Is.EqualTo(2));
				Assert.That(container.Spline[0].Position, Is.EqualTo(float3.zero));
				Assert.That(container.Spline[1].Position,
					Is.EqualTo(new float3(0f, 500f, 0f)));
				Assert.That(container.transform.localScale,
					Is.EqualTo(Physics.ScaleInvVector));

				var knot = container.Spline[1];
				knot.Position = new float3(125f, 500f, 75f);
				container.Spline.SetKnot(1, knot);
				Assert.That(container.Spline[1].Position,
					Is.EqualTo(new float3(125f, 500f, 75f)));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldCreateFourBallClearanceRailsByDefault()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();

				Assert.That(component.Segments, Has.Count.EqualTo(1));
				AssertOffsets(component.Segments[0],
					new Vector2(-19f, 0f),
					new Vector2(19f, 0f),
					new Vector2(-19f, 44f),
					new Vector2(19f, 44f));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOnlyRecreateAMissingSplineChildExplicitly()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				Object.DestroyImmediate(component.SplineContainer.gameObject);

				Assert.That(component.SplineContainer, Is.Null);
				Assert.That(go.transform.childCount, Is.Zero);

				var recreated = component.EnsureSplineContainerExists();
				Assert.That(recreated, Is.Not.Null);
				Assert.That(recreated.transform.parent, Is.SameAs(go.transform));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldCreateContinuousLinearConnectionsByDefault()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(0f, 1000f, 0f)), TangentMode.AutoSmooth);
				AddMidpointLayout(component);

				var connection = component.Segments[0].ConnectionToNext;
				Assert.That(connection.WireCount, Is.EqualTo(4));
				for (var wireIndex = 0; wireIndex < connection.WireCount; wireIndex++) {
					Assert.That(connection.IsWireOverridden(wireIndex), Is.False);
					Assert.That(connection.IsWireContinuous(wireIndex), Is.True);
					Assert.That(connection.GetWireCurve(wireIndex).Evaluate(0.5f),
						Is.EqualTo(0.5f).Within(0.001f));
				}
				Assert.That(component.Segments[1].ConnectionToNext.WireCount, Is.Zero);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldTrackAndResetExplicitTransitionOverrides()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(0f, 1000f, 0f)), TangentMode.AutoSmooth);
				AddMidpointLayout(component);

				component.SetWireTransitionOverride(0, 2, true);
				var connection = component.Segments[0].ConnectionToNext;
				Assert.That(connection.IsWireOverridden(2), Is.True);
				Assert.That(connection.IsWireContinuous(2), Is.True);
				Assert.That(connection.GetWireCurve(2).Evaluate(0.25f),
					Is.EqualTo(0.25f).Within(0.001f));

				component.SetWireContinuous(0, 2, false);
				connection = component.Segments[0].ConnectionToNext;
				Assert.That(connection.IsWireOverridden(2), Is.True);
				Assert.That(connection.IsWireContinuous(2), Is.False);

				component.SetWireTransitionOverride(0, 2, false);
				connection = component.Segments[0].ConnectionToNext;
				Assert.That(connection.IsWireOverridden(2), Is.False);
				Assert.That(connection.IsWireContinuous(2), Is.True);
				Assert.That(connection.GetWireCurve(2).Evaluate(0.25f),
					Is.EqualTo(0.25f).Within(0.001f));

				component.SetWireTransitionCurve(0, 2, new AnimationCurve(
					new Keyframe(0f, 0f), new Keyframe(0.5f, 0.2f),
					new Keyframe(1f, 1f)));
				connection = component.Segments[0].ConnectionToNext;
				Assert.That(connection.IsWireOverridden(2), Is.True);
				Assert.That(connection.GetWireCurve(2).Evaluate(0.5f),
					Is.EqualTo(0.2f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldUseLayoutPositionsAsTransitionEndpoints()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(0f, 1000f, 0f)),
					TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(1);
				component.SetRailOffset(0, 0, new Vector2(0f, 0f));
				component.SetRailOffset(1, 0, new Vector2(40f, 0f));

				var firstStart = WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 0, 0f);
				var firstMiddle = WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 0, 0.5f);
				var firstEnd = WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 0, 1f);
				var secondStart = WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 1, 0, 0f);
				var secondEnd = WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 1, 0, 1f);

				Assert.That(firstStart.x, Is.EqualTo(0f).Within(0.001f));
				Assert.That(firstMiddle.x, Is.EqualTo(20f).Within(0.001f));
				Assert.That(firstEnd.x, Is.EqualTo(40f).Within(0.001f));
				Assert.That(secondStart.x, Is.EqualTo(40f).Within(0.001f));
				Assert.That(secondEnd.x, Is.EqualTo(40f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldTransitionEveryContinuousWireToTheNextLayout()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(0f, 1000f, 0f)),
					TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(2);
				component.SetRailOffset(0, 0, new Vector2(-20f, 0f));
				component.SetRailOffset(0, 1, new Vector2(20f, 0f));
				component.SetRailOffset(1, 0, new Vector2(-40f, 0f));
				component.SetRailOffset(1, 1, new Vector2(60f, 0f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 0, 1f).x, Is.EqualTo(-40f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 1, 0, 0f).x, Is.EqualTo(-40f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 1, 1f).x, Is.EqualTo(60f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 1, 1, 0f).x, Is.EqualTo(60f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldShapeTheSpanBetweenLayoutsWithTheWireCurve()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(0f, 1000f, 0f)),
					TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(1);
				component.SetRailOffset(0, 0, Vector2.zero);
				component.SetRailOffset(1, 0, new Vector2(40f, 0f));
				component.SetWireTransitionCurve(0, 0, new AnimationCurve(
					new Keyframe(0f, 0f), new Keyframe(0.5f, 0.25f),
					new Keyframe(1f, 1f)));

				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 0, 0.5f).x, Is.EqualTo(10f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 1, 0, 0.5f).x, Is.EqualTo(40f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 0, 1f).x, Is.EqualTo(40f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 1, 0, 0f).x, Is.EqualTo(40f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOnlyBlendTheSelectedWireIndices()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(0f, 1000f, 0f)),
					TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(2);
				component.SetRailOffset(0, 0, new Vector2(-20f, 0f));
				component.SetRailOffset(0, 1, new Vector2(20f, 0f));
				component.SetRailOffset(1, 0, new Vector2(-40f, 0f));
				component.SetRailOffset(1, 1, new Vector2(40f, 0f));
				component.SetWireContinuous(0, 1, false);

				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 0, 1f).x, Is.EqualTo(-40f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 1, 0, 0f).x, Is.EqualTo(-40f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 0, 1, 1f).x, Is.EqualTo(20f).Within(0.001f));
				Assert.That(WireRailSplineGeometry.EvaluateRailOffset(spline,
					component.Segments, 1, 1, 0f).x, Is.EqualTo(40f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRemoveInternalCapsFromAContinuousWire()
		{
			const int radialSegments = 8;
			const int trianglesPerRingPair = radialSegments * 2;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(
					new BezierKnot(new float3(0f, 1000f, 0f)), TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(1);
				component.SetRailOffset(0, 0, new Vector2(0f, 0f));
				component.SetRailOffset(1, 0, new Vector2(40f, 0f));
				component.SetWireContinuous(0, 0, false);
				var disconnectedTriangleCount = component.RenderMesh.triangles.Length / 3;
				var disconnectedBodyTriangleCount = 0;
				for (var segmentIndex = 0; segmentIndex < 2; segmentIndex++) {
					disconnectedBodyTriangleCount += (WireRailRenderMeshGenerator
						.BuildSampleParameters(spline, component.Segments, segmentIndex, 0, 16)
						.Count - 1) * trianglesPerRingPair;
				}

				component.SetWireContinuous(0, 0, true);

				var continuousTriangleCount = component.RenderMesh.triangles.Length / 3;
				var continuousBodyTriangleCount = 0;
				for (var segmentIndex = 0; segmentIndex < 2; segmentIndex++) {
					continuousBodyTriangleCount += (WireRailRenderMeshGenerator
						.BuildSampleParameters(spline, component.Segments, segmentIndex, 0, 16)
						.Count - 1) * trianglesPerRingPair;
				}

				Assert.That(disconnectedTriangleCount - disconnectedBodyTriangleCount,
					Is.EqualTo(radialSegments * 4), "all four open ends should be capped");
				Assert.That(continuousTriangleCount - continuousBodyTriangleCount,
					Is.EqualTo(radialSegments * 2), "only the two outer ends should be capped");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOrientRenderRingsPerpendicularToTheWireCenterline()
		{
			const int radialSegments = 8;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(0f, 1000f, 0f)) {
						Rotation = quaternion.RotateX(math.PI * 0.5f),
					}, TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(1);
				component.SetRailOffset(0, 0, Vector2.zero);
				component.SetRailOffset(1, 0, new Vector2(500f, 0f));

				var vertices = component.RenderMesh.vertices;
				var samples = WireRailRenderMeshGenerator.BuildSampleParameters(
					component.SplineContainer.Spline, component.Segments, 0, 0, 16);
				var sampleIndex = samples.FindIndex(value => Mathf.Approximately(value, 0.5f));
				var ringStart = sampleIndex * radialSegments;
				var center = AverageRing(vertices, ringStart, radialSegments);
				var tangentStep = math.min(0.5f - samples[sampleIndex - 1],
					samples[sampleIndex + 1] - 0.5f);
				Assert.That(WireRailSplineGeometry.TryEvaluateRailFrame(
					component.SplineContainer.Spline, component.Segments, 0, 0, 0.5f,
					tangentStep, out var frame), Is.True);

				AssertRingPerpendicular(vertices, ringStart, radialSegments, center,
					frame.Tangent);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldUseTheSameWireFrameAndTangentOnBothSidesOfAConnectedKnot()
		{
			const int radialSegments = 8;
			const int capVertexCount = radialSegments + 1;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(0f, 1000f, 0f)) {
						Rotation = quaternion.RotateX(math.PI * 0.5f),
					}, TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(1);
				component.SetRailOffset(0, 0, Vector2.zero);
				component.SetRailOffset(1, 0, new Vector2(500f, 0f));

				var vertices = component.RenderMesh.vertices;
				var firstSamples = WireRailRenderMeshGenerator.BuildSampleParameters(
					component.SplineContainer.Spline, component.Segments, 0, 0, 16);
				var secondSamples = WireRailRenderMeshGenerator.BuildSampleParameters(
					component.SplineContainer.Spline, component.Segments, 1, 0, 16);
				var firstEndRing = (firstSamples.Count - 1) * radialSegments;
				var secondStartRing = firstSamples.Count * radialSegments + capVertexCount;
				var firstCenter = AverageRing(vertices, firstEndRing, radialSegments);
				var secondCenter = AverageRing(vertices, secondStartRing, radialSegments);
				var tangentStep = math.min(1f - firstSamples[^2], secondSamples[1]);
				Assert.That(WireRailSplineGeometry.TryEvaluateRailFrame(
					component.SplineContainer.Spline, component.Segments, 0, 0, 1f,
					tangentStep, out var frame), Is.True);

				Assert.That(Vector3.Distance(firstCenter, secondCenter), Is.LessThan(0.001f));
				AssertRingPerpendicular(vertices, firstEndRing, radialSegments, firstCenter,
					frame.Tangent);
				AssertRingPerpendicular(vertices, secondStartRing, radialSegments, secondCenter,
					frame.Tangent);
				var firstRadial = math.normalize((float3)(vertices[firstEndRing] - firstCenter));
				var secondRadial = math.normalize((float3)(vertices[secondStartRing] - secondCenter));
				Assert.That(math.dot(firstRadial, secondRadial), Is.GreaterThan(0.9999f));

				const float curveStep = 1f / 1024f;
				Assert.That(WireRailSplineGeometry.TryEvaluateRailPosition(
					component.SplineContainer.Spline, component.Segments, 0, 0,
					1f - curveStep, out var beforeKnot), Is.True);
				Assert.That(WireRailSplineGeometry.TryEvaluateRailPosition(
					component.SplineContainer.Spline, component.Segments, 0, 0,
					1f, out var atKnot), Is.True);
				Assert.That(WireRailSplineGeometry.TryEvaluateRailPosition(
					component.SplineContainer.Spline, component.Segments, 1, 0,
					curveStep, out var afterKnot), Is.True);
				var incoming = math.normalizesafe(atKnot - beforeKnot, frame.Tangent);
				var outgoing = math.normalizesafe(afterKnot - atKnot, frame.Tangent);
				Assert.That(math.dot(incoming, outgoing), Is.GreaterThan(0.9999f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepAnUnchangingFourWireLayoutRigidAndParallel()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				var middle = spline[1];
				middle.Position = new float3(-400f, 300f, 0f);
				spline.SetKnot(1, middle);
				spline.Add(new BezierKnot(new float3(0f, 700f, 0f)) {
					Rotation = quaternion.RotateX(math.PI * 0.5f),
				}, TangentMode.AutoSmooth);

				for (var segmentIndex = 0; segmentIndex < component.Segments.Count;
					segmentIndex++) {
					for (var sampleIndex = 0; sampleIndex <= 32; sampleIndex++) {
						var curveT = sampleIndex / 32f;
						Assert.That(WireRailSplineGeometry.TryEvaluateLayout(spline,
							component.Segments, segmentIndex,
							curveT, out var mainFrame), Is.True);
						var referenceTangent = float3.zero;
						for (var railIndex = 0; railIndex < 4; railIndex++) {
							Assert.That(WireRailSplineGeometry.TryEvaluateRailPosition(spline,
								component.Segments, segmentIndex, railIndex, curveT,
								out var position), Is.True);
							var expected = mainFrame.TransformOffset(
								(float2)component.Segments[segmentIndex].GetRailOffset(railIndex));
							Assert.That(math.distance(position, expected), Is.LessThan(0.001f),
								$"wire {railIndex + 1} drifted at segment "
								+ $"{segmentIndex + 1}, t={curveT}");
							Assert.That(WireRailSplineGeometry.TryEvaluateRailFrame(spline,
								component.Segments, segmentIndex, railIndex, curveT, 1f / 128f,
								out var railFrame), Is.True);
							if (railIndex == 0) {
								referenceTangent = railFrame.Tangent;
							} else {
								Assert.That(math.dot(referenceTangent, railFrame.Tangent),
									Is.GreaterThan(0.9999f), $"wire {railIndex + 1} was not "
									+ $"parallel at segment {segmentIndex + 1}, t={curveT}");
							}
						}
					}
				}
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldPlaceBraceFixturesByDistanceAcrossTheWholeSpline()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(0f, 1000f, 0f)) {
					Rotation = quaternion.RotateX(math.PI * 0.5f),
				}, TangentMode.AutoSmooth);
				var fixtureIndex = component.AddBraceFixture(750f);
				var brace = (WireRailBraceFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateBraceProfile(spline,
					component.Segments, brace, out var profile), Is.True);
				Assert.That(math.distance(profile.Frame.Position, new float3(0f, 750f, 0f)),
					Is.LessThan(0.1f));
				Assert.That(math.abs(math.dot(profile.GetCenterlinePosition(0f) - profile.Center,
					profile.Frame.Tangent)), Is.LessThan(0.001f),
					"the brace ring should be perpendicular to the spline");
				Assert.That(component.Fixtures.Count, Is.EqualTo(1));
				Assert.That(component.Segments.Count, Is.EqualTo(1));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldConnectTheTwoBottomRailsWithACrossWireByDefault()
		{
			const int radialSegments = 8;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var railTriangleCount = component.RenderMesh.triangles.Length / 3;
				var fixtureIndex = component.AddCrossWireFixture(250f);
				var crossWire = (WireRailCrossWireFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateCrossWireProfile(
					component.SplineContainer.Spline, component.Segments, crossWire,
					out var profile), Is.True);
				Assert.That(crossWire.StartRailIndex, Is.EqualTo(0));
				Assert.That(crossWire.EndRailIndex, Is.EqualTo(1));
				Assert.That(crossWire.Angle, Is.EqualTo(0f));
				var expectedStart = profile.StartRailOffset
					+ new float2(profile.StartRailRadius, 0f);
				var expectedEnd = profile.EndRailOffset
					- new float2(profile.EndRailRadius, 0f);
				Assert.That(math.distance(profile.StartOffset, expectedStart),
					Is.LessThan(0.001f));
				Assert.That(math.distance(profile.EndOffset, expectedEnd),
					Is.LessThan(0.001f));
				Assert.That(math.abs(math.dot(profile.End - profile.Start,
					profile.Frame.Tangent)), Is.LessThan(0.001f));
				Assert.That(component.RenderMesh.triangles.Length / 3 - railTriangleCount,
					Is.EqualTo(radialSegments * 4),
					"the cross wire should have one tube span and two caps");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOffsetAngleAndResizeACrossWire()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddCrossWireFixture(250f);
				component.SetCrossWireFixtureProperties(fixtureIndex, 250f,
					90f, 6f, -3f, 12f);
				var crossWire = (WireRailCrossWireFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateCrossWireProfile(
					component.SplineContainer.Spline, component.Segments, crossWire,
					out var profile), Is.True);
				var railDirection = math.normalize(profile.EndRailOffset
					- profile.StartRailOffset);
				var attachmentStart = profile.StartRailOffset
					+ railDirection * profile.StartRailRadius;
				var attachmentEnd = profile.EndRailOffset
					- railDirection * profile.EndRailRadius;
				var direction = new float2(0f, 1f);
				var expectedLength = math.distance(attachmentStart, attachmentEnd) + 12f;
				var bottomCenter = (attachmentStart + attachmentEnd) * 0.5f;
				var envelopeMinimum = new float2(float.PositiveInfinity);
				var envelopeMaximum = new float2(float.NegativeInfinity);
				var segment = component.Segments[0];
				for (var railIndex = 0; railIndex < segment.RailCount; railIndex++) {
					if (!segment.IsRailActive(railIndex)) {
						continue;
					}
					var railOffset = (float2)segment.GetRailOffset(railIndex);
					var railRadius = segment.GetWireDiameter(railIndex) * 0.5f;
					envelopeMinimum = math.min(envelopeMinimum, railOffset - railRadius);
					envelopeMaximum = math.max(envelopeMaximum, railOffset + railRadius);
				}
				var expectedRotationOrigin = (envelopeMinimum + envelopeMaximum) * 0.5f;
				Assert.That(math.distance(profile.RotationOriginOffset,
					expectedRotationOrigin), Is.LessThan(0.001f));
				var relativeBottomCenter = bottomCenter - profile.RotationOriginOffset;
				var expectedCenter = profile.RotationOriginOffset
					+ new float2(-relativeBottomCenter.y, relativeBottomCenter.x)
					+ new float2(6f, -3f);
				Assert.That(math.distance(profile.StartOffset,
					expectedCenter - direction * expectedLength * 0.5f),
					Is.LessThan(0.001f));
				Assert.That(math.distance(profile.EndOffset,
					expectedCenter + direction * expectedLength * 0.5f),
					Is.LessThan(0.001f));
				Assert.That(math.distance(profile.StartOffset, profile.EndOffset),
					Is.EqualTo(expectedLength).Within(0.001f));
				Assert.That(math.dot(math.normalize(profile.EndOffset - profile.StartOffset),
					direction), Is.EqualTo(1f).Within(0.001f));
				Assert.That(math.distance(profile.RotationOriginOffset, bottomCenter),
					Is.GreaterThan(0.001f), "the default four-rail envelope should rotate "
					+ "around a point above the bottom rails");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepCrossWireAngleRelativeToTheSpline()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailOffset(0, 0, new Vector2(-19f, -8f));
				component.SetRailOffset(0, 1, new Vector2(19f, 8f));
				var fixtureIndex = component.AddCrossWireFixture(250f);
				var crossWire = (WireRailCrossWireFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateCrossWireProfile(
					component.SplineContainer.Spline, component.Segments, crossWire,
					out var profile), Is.True);
				Assert.That(math.dot(math.normalize(profile.EndOffset - profile.StartOffset),
					new float2(1f, 0f)), Is.EqualTo(1f).Within(0.001f),
					"angle zero must stay horizontal even when the bottom rails are stepped");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOmitDegenerateCrossWireBodyAtMaximumBevel()
		{
			const int radialSegments = 8;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.SetWireCapBevelSize(2f);
				var railTriangleCount = component.RenderMesh.triangles.Length / 3;
				var fixtureIndex = component.AddCrossWireFixture(250f);
				component.SetCrossWireFixtureProperties(fixtureIndex, 250f,
					0f, 0f, 0f, -1000f);

				var fixtureTriangleCount = component.RenderMesh.triangles.Length / 3
					- railTriangleCount;
				Assert.That(fixtureTriangleCount, Is.EqualTo(radialSegments * 6),
					"the two beveled caps should meet without a zero-length tube body");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldHideACrossWireWhenEitherBottomRailIsInactive()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var fixtureIndex = component.AddCrossWireFixture(250f);
				var crossWire = (WireRailCrossWireFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateCrossWireProfile(
					component.SplineContainer.Spline, component.Segments, crossWire, out _),
					Is.True);

				component.SetRailsActive(0, new[] { 1 }, false);

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateCrossWireProfile(
					component.SplineContainer.Spline, component.Segments, crossWire, out _),
					Is.False);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldMigrateLegacyBraceRadiusOffsetToScale()
		{
			var brace = new WireRailBraceFixture();
			JsonUtility.FromJsonOverwrite(
				"{\"_diameter\":8,\"_scale\":1,\"_radiusOffset\":10,\"_scaleInitialized\":false}",
				brace);

			Assert.That(brace.EnsureScaleInitialized(50f), Is.True);
			Assert.That(brace.Scale, Is.EqualTo(1.2f).Within(0.001f));
			Assert.That(brace.EnsureScaleInitialized(50f), Is.False);
		}

		[Test]
		public void ShouldKeepBraceScaleWhenMovingAcrossDifferentWireLayouts()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(0f, 1000f, 0f)), TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(2);
				component.SetRailOffset(0, 0, new Vector2(-20f, 0f));
				component.SetRailOffset(0, 1, new Vector2(20f, 0f));
				component.SetRailOffset(1, 0, new Vector2(-40f, 0f));
				component.SetRailOffset(1, 1, new Vector2(40f, 0f));
				var fixtureIndex = component.AddBraceFixture(250f);
				component.SetBraceFixtureProperties(fixtureIndex, 250f,
					false, 0f, 0f, scale: 1.5f);

				Assert.That(component.TryGetBraceCrossSection(fixtureIndex,
					out var firstCrossSection), Is.True);
				component.SetBraceFixtureProperties(fixtureIndex, 750f,
					false, 0f, 0f, scale: 1.5f);
				Assert.That(component.TryGetBraceCrossSection(fixtureIndex,
					out var secondCrossSection), Is.True);

				Assert.That(math.abs(secondCrossSection.BaseRadius
					- firstCrossSection.BaseRadius), Is.GreaterThan(0.001f));
				Assert.That(firstCrossSection.Radius / firstCrossSection.BaseRadius,
					Is.EqualTo(1.5f).Within(0.001f));
				Assert.That(secondCrossSection.Radius / secondCrossSection.BaseRadius,
					Is.EqualTo(1.5f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldExposeBraceCrossSectionForInspectorScale()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddBraceFixture(250f);

				Assert.That(component.TryGetBraceCrossSection(fixtureIndex,
					out var defaultCrossSection), Is.True);
				Assert.That(defaultCrossSection.Radius,
					Is.EqualTo(defaultCrossSection.BaseRadius).Within(0.001f));

				component.SetBraceFixtureProperties(fixtureIndex, 250f,
					false, 0f, 0f, scale: 1.5f);

				Assert.That(component.TryGetBraceCrossSection(fixtureIndex,
					out var scaledCrossSection), Is.True);
				Assert.That(scaledCrossSection.BaseRadius,
					Is.EqualTo(defaultCrossSection.BaseRadius).Within(0.001f));
				Assert.That(scaledCrossSection.Radius,
					Is.EqualTo(defaultCrossSection.BaseRadius * 1.5f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldGenerateFullAndCutoutBraceGeometry()
		{
			const int radialSegments = 8;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var railTriangleCount = component.RenderMesh.triangles.Length / 3;
				var fixtureIndex = component.AddBraceFixture(250f);
				var fullBraceTriangleCount = component.RenderMesh.triangles.Length / 3
					- railTriangleCount;
				Assert.That(fullBraceTriangleCount, Is.EqualTo(32 * radialSegments * 2));

				component.SetBraceFixtureProperties(fixtureIndex, 250f,
					true, 0f, 90f);
				var cutoutBraceTriangleCount = component.RenderMesh.triangles.Length / 3
					- railTriangleCount;
				Assert.That(cutoutBraceTriangleCount,
					Is.EqualTo(24 * radialSegments * 2 + radialSegments * 2),
					"a 90-degree cutout should leave a capped three-quarter brace");

			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldBevelEveryExposedRailAndFixtureCap()
		{
			const int radialSegments = 8;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var fixtureIndex = component.AddBraceFixture(250f);
				component.SetBraceFixtureProperties(fixtureIndex, 250f,
					true, 0f, 90f);
				var flatVertices = component.RenderMesh.vertexCount;
				var flatTriangles = component.RenderMesh.triangles.Length / 3;

				component.SetWireCapBevelSize(2f);

				const int exposedCaps = 6; // Four rail ends plus two brace cutout ends.
				Assert.That(component.RenderMesh.vertexCount - flatVertices,
					Is.EqualTo(exposedCaps * radialSegments * 2));
				Assert.That(component.RenderMesh.triangles.Length / 3 - flatTriangles,
					Is.EqualTo(exposedCaps * radialSegments * 2));
				Assert.That(component.WireCapBevelSize, Is.EqualTo(2f));

				var samples = WireRailRenderMeshGenerator.BuildSampleParameters(
					component.SplineContainer.Spline, component.Segments, 0, 0, 16);
				var firstBevelVertex = samples.Count * radialSegments;
				Assert.That(Vector3.Distance(component.RenderMesh.vertices[0],
					component.RenderMesh.vertices[firstBevelVertex]), Is.LessThan(0.001f));
				Assert.That(WireRailSplineGeometry.TryEvaluateRailFrame(
					component.SplineContainer.Spline, component.Segments, 0, 0, 0f,
					samples[1], out var frame), Is.True);
				var capNormal = -frame.Tangent;
				var bevelNormal = (float3)component.RenderMesh.normals[firstBevelVertex];
				Assert.That(math.dot(bevelNormal, capNormal),
					Is.EqualTo(math.sqrt(0.5f)).Within(0.001f));
				var flatCapCenter = firstBevelVertex + radialSegments * 2;
				Assert.That(math.dot((float3)component.RenderMesh.normals[flatCapCenter],
					capNormal), Is.EqualTo(1f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOffsetAndReplacePartOfABraceWithAStraightChord()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddBraceFixture(250f);
				var brace = (WireRailBraceFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateBraceProfile(
					component.SplineContainer.Spline, component.Segments, brace,
					out var original), Is.True);

				component.SetBraceFixtureProperties(fixtureIndex, 250f,
					false, 60f, 120f,
					true, 210f, 330f, 12f, -7f, 1.25f);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateBraceProfile(
					component.SplineContainer.Spline, component.Segments, brace,
					out var changed), Is.True);
				Assert.That(math.distance(changed.Center, original.Center
					+ original.Frame.Right * 12f - original.Frame.Up * 7f),
					Is.LessThan(0.001f));
				Assert.That(changed.Radius, Is.EqualTo(original.Radius * 1.25f).Within(0.001f));

				var start = brace.EvaluateCenterlineOffset(math.radians(210f), changed.Radius);
				var middle = brace.EvaluateCenterlineOffset(math.radians(270f), changed.Radius);
				var end = brace.EvaluateCenterlineOffset(math.radians(330f), changed.Radius);
				Assert.That(math.abs((middle.x - start.x) * (end.y - start.y)
					- (middle.y - start.y) * (end.x - start.x)), Is.LessThan(0.001f));
				Assert.That(middle.y, Is.EqualTo(-changed.Radius * 0.5f).Within(0.001f));

				component.SetBraceFixtureProperties(fixtureIndex, 250f,
					false, 60f, 120f,
					true, 210f, 330f, 12f, -7f, 0.75f);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateBraceProfile(
					component.SplineContainer.Spline, component.Segments, brace,
					out var shrunk), Is.True);
				Assert.That(shrunk.Radius, Is.EqualTo(original.Radius * 0.75f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(40f, 100f, 60f, 120f)]
		[TestCase(210f, 300f, 225f, 315f)]
		[TestCase(10f, 300f, 305f, 235f)]
		public void ShouldAlignBraceAngleRangesWithoutChangingTheirSweep(float start,
			float end, float expectedStart, float expectedEnd)
		{
			var aligned = WireRailBraceFixture.AlignAngleRangeHorizontally(start, end);

			Assert.That(aligned.x, Is.EqualTo(expectedStart).Within(0.001f));
			Assert.That(aligned.y, Is.EqualTo(expectedEnd).Within(0.001f));
			var sourceSweep = math.fmod(end - start + 360f, 360f);
			var alignedSweep = math.fmod(aligned.y - aligned.x + 360f, 360f);
			Assert.That(alignedSweep, Is.EqualTo(sourceSweep).Within(0.001f));
			Assert.That(math.sin(math.radians(aligned.x)),
				Is.EqualTo(math.sin(math.radians(aligned.y))).Within(0.001f));
		}

		[Test]
		public void ShouldApplyBracePropertiesToAllWithoutChangingPositions()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddBraceFixture(100f);
				component.AddBraceFixture(250f);
				component.AddBraceFixture(400f);
				component.SetBraceFixtureProperties(1, 250f,
					true, 35f, 145f, true, 215f, 325f, 7f, -11f, 1.35f);
				component.SetBraceFixtureProperties(0, 100f,
					false, 60f, 120f, false, 210f, 330f, 0f, 0f, 0.8f);
				component.SetBraceFixtureProperties(2, 400f,
					false, 50f, 130f, false, 220f, 320f, -3f, 5f, 1.8f);

				component.ApplyBracePropertiesToAll(1);

				Assert.That(component.Fixtures.Select(fixture => fixture.Distance),
					Is.EqualTo(new[] { 100f, 250f, 400f }));
				foreach (var brace in component.Fixtures.Cast<WireRailBraceFixture>()) {
					Assert.That(brace.HasCutout, Is.True);
					Assert.That(brace.CutoutStartAngle, Is.EqualTo(35f));
					Assert.That(brace.CutoutEndAngle, Is.EqualTo(145f));
					Assert.That(brace.HasStraightSection, Is.True);
					Assert.That(brace.StraightStartAngle, Is.EqualTo(215f));
					Assert.That(brace.StraightEndAngle, Is.EqualTo(325f));
					Assert.That(brace.LateralOffset, Is.EqualTo(7f));
					Assert.That(brace.VerticalOffset, Is.EqualTo(-11f));
					Assert.That(brace.Scale, Is.EqualTo(1.35f));
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDuplicateEveryBraceSetting()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var sourceIndex = component.AddBraceFixture(175f);
				component.SetBraceFixtureProperties(sourceIndex, 175f,
					true, 35f, 125f, true, 205f, 315f, 6f, -9f, 1.4f);
				var duplicateIndex = component.DuplicateBraceFixture(sourceIndex);
				var source = (WireRailBraceFixture)component.Fixtures[sourceIndex];
				var duplicate = (WireRailBraceFixture)component.Fixtures[duplicateIndex];

				Assert.That(duplicateIndex, Is.EqualTo(sourceIndex + 1));
				Assert.That(component.Fixtures, Has.Count.EqualTo(2));
				Assert.That(duplicate, Is.Not.SameAs(source));
				Assert.That(duplicate.Distance, Is.EqualTo(source.Distance));
				Assert.That(duplicate.Diameter, Is.EqualTo(source.Diameter));
				Assert.That(duplicate.HasCutout, Is.EqualTo(source.HasCutout));
				Assert.That(duplicate.CutoutStartAngle, Is.EqualTo(source.CutoutStartAngle));
				Assert.That(duplicate.CutoutEndAngle, Is.EqualTo(source.CutoutEndAngle));
				Assert.That(duplicate.HasStraightSection, Is.EqualTo(source.HasStraightSection));
				Assert.That(duplicate.StraightStartAngle, Is.EqualTo(source.StraightStartAngle));
				Assert.That(duplicate.StraightEndAngle, Is.EqualTo(source.StraightEndAngle));
				Assert.That(duplicate.LateralOffset, Is.EqualTo(source.LateralOffset));
				Assert.That(duplicate.VerticalOffset, Is.EqualTo(source.VerticalOffset));
				Assert.That(duplicate.Scale, Is.EqualTo(source.Scale));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDuplicateEveryCrossWireSetting()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var sourceIndex = component.AddCrossWireFixture(175f);
				component.SetCrossWireFixtureProperties(sourceIndex, 175f,
					25f, 6f, -9f, 14f);
				var duplicateIndex = component.DuplicateCrossWireFixture(sourceIndex);
				var source = (WireRailCrossWireFixture)component.Fixtures[sourceIndex];
				var duplicate = (WireRailCrossWireFixture)component.Fixtures[duplicateIndex];

				Assert.That(duplicateIndex, Is.EqualTo(sourceIndex + 1));
				Assert.That(duplicate, Is.Not.SameAs(source));
				Assert.That(duplicate.Distance, Is.EqualTo(source.Distance));
				Assert.That(duplicate.Diameter, Is.EqualTo(source.Diameter));
				Assert.That(duplicate.StartRailIndex, Is.EqualTo(source.StartRailIndex));
				Assert.That(duplicate.EndRailIndex, Is.EqualTo(source.EndRailIndex));
				Assert.That(duplicate.Angle, Is.EqualTo(source.Angle));
				Assert.That(duplicate.LateralOffset, Is.EqualTo(source.LateralOffset));
				Assert.That(duplicate.VerticalOffset, Is.EqualTo(source.VerticalOffset));
				Assert.That(duplicate.LengthAdjustment,
					Is.EqualTo(source.LengthAdjustment));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldReorderFixturesWithoutChangingTheirPositions()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddBraceFixture(100f);
				component.AddBraceFixture(300f);
				component.SetBraceFixtureProperties(0, 100f, false, 0f, 0f);
				component.SetBraceFixtureProperties(1, 300f, false, 0f, 0f);

				component.MoveFixture(1, 0);

				Assert.That(component.Fixtures[0].Distance, Is.EqualTo(300f).Within(0.001f));
				Assert.That(component.Fixtures[1].Distance, Is.EqualTo(100f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldUseOneRailCountAcrossEveryLayout()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(250f);
				component.SetRailCount(5);

				Assert.That(component.RailCount, Is.EqualTo(5));
				Assert.That(component.Segments.Select(layout => layout.RailCount),
					Is.EqualTo(new[] { 5, 5 }));
				Assert.That(component.Segments.Select(CountActiveRails),
					Is.EqualTo(new[] { 5, 5 }));
				Assert.That(component.Segments[0].ConnectionToNext.WireCount, Is.EqualTo(5));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldGenerateOnlyRailsEnabledForTheLayoutSpan()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var allRailTriangles = component.RenderMesh.triangles.Length;

				component.SetRailsActive(0, new[] { 0, 1 }, false);

				Assert.That(component.RailCount, Is.EqualTo(4));
				Assert.That(CountActiveRails(component.Segments[0]), Is.EqualTo(2));
				Assert.That(component.RenderMesh.triangles.Length, Is.LessThan(allRailTriangles));
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldIgnoreAnInactiveLayoutOffsetWhenTheRailStartsLater()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(250f);
				component.SetRailCount(1);
				component.SetRailOffset(0, 0, new Vector2(300f, 0f));
				component.SetRailOffset(1, 0, new Vector2(20f, 0f));
				component.SetRailsActive(0, new[] { 0 }, false);
				var spline = component.SplineContainer.Spline;

				Assert.That(WireRailSplineGeometry.TryEvaluateRailPosition(spline,
					component.Segments, 1, 0, 0f, out var before), Is.True);
				component.SetRailOffset(0, 0, new Vector2(-300f, 100f));
				Assert.That(WireRailSplineGeometry.TryEvaluateRailPosition(spline,
					component.Segments, 1, 0, 0f, out var after), Is.True);
				Assert.That(WireRailSplineGeometry.TryEvaluateLayout(spline,
					component.Segments, 1, 0f, out var frame), Is.True);

				Assert.That(math.distance(before, after), Is.LessThan(0.001f));
				Assert.That(math.distance(after,
					frame.TransformOffset(new float2(20f, 0f))), Is.LessThan(0.001f));
				Assert.That(WireRailSplineGeometry.IsContinuousAtStart(spline,
					component.Segments, 1, 0), Is.False);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldMigrateLegacyLayoutCountsToInactiveRails()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var twoRailLayout = new WireRailSegment();
				twoRailLayout.ResizeRailCount(2, 8f, true, true);
				var fiveRailLayout = new WireRailSegment();
				fiveRailLayout.ResizeRailCount(5, 8f, true, true);
				var flags = BindingFlags.Instance | BindingFlags.NonPublic;
				typeof(WireRailComponent).GetField("_segments", flags)?.SetValue(component,
					new List<WireRailSegment> { twoRailLayout, fiveRailLayout });
				typeof(WireRailComponent).GetField("_railCountInitialized", flags)
					?.SetValue(component, false);

				component.SynchronizeSegments();

				Assert.That(component.RailCount, Is.EqualTo(5));
				Assert.That(component.Segments.Select(layout => layout.RailCount),
					Is.EqualTo(new[] { 5, 5 }));
				Assert.That(component.Segments.Select(CountActiveRails),
					Is.EqualTo(new[] { 2, 5 }));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldPlaceWireLayoutsIndependentlyFromSplineKnots()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var knotCount = component.SplineContainer.Spline.Count;
				component.AddLayout(125f);
				component.AddLayout(375f);
				component.SetLayoutDistance(1, 200f);
				component.SetRailCount(2);

				Assert.That(component.SplineContainer.Spline.Count, Is.EqualTo(knotCount));
				Assert.That(component.Segments, Has.Count.EqualTo(3));
				Assert.That(component.Segments[0].Distance, Is.Zero);
				Assert.That(component.Segments[1].Distance, Is.EqualTo(200f).Within(0.001f));
				Assert.That(component.Segments[2].Distance, Is.EqualTo(375f).Within(0.001f));
				Assert.That(component.Segments[1].RailCount, Is.EqualTo(2));

				component.RemoveLayout(1);
				Assert.That(component.Segments, Has.Count.EqualTo(2));
				Assert.That(component.SplineContainer.Spline.Count, Is.EqualTo(knotCount));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldReorderLayoutsAcrossTheirExistingPositionSlots()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(200f);
				component.AddLayout(400f);
				component.SetRailCount(3);
				component.SetRailsActive(0, new[] { 1, 2 }, false);
				component.SetRailsActive(1, new[] { 2 }, false);

				component.MoveLayout(2, 0);

				Assert.That(component.Segments.Select(layout => layout.Distance),
					Is.EqualTo(new[] { 0f, 200f, 400f }));
				Assert.That(component.Segments.Select(CountActiveRails),
					Is.EqualTo(new[] { 3, 1, 2 }));
				Assert.That(component.Segments[0].ConnectionToNext.WireCount, Is.EqualTo(3));
				Assert.That(component.Segments[1].ConnectionToNext.WireCount, Is.EqualTo(3));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepFixturesIndependentFromSegmentChanges()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddBraceFixture(100f);
				component.AddBraceFixture(400f);
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(0f, 1000f, 0f)), TangentMode.AutoSmooth);

				Assert.That(component.Segments.Count, Is.EqualTo(1));
				Assert.That(component.Fixtures.Count, Is.EqualTo(2));
				Assert.That(component.Fixtures[0].Distance, Is.EqualTo(100f).Within(0.001f));
				Assert.That(component.Fixtures[1].Distance, Is.EqualTo(400f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldAdaptAndKeepTheLastRingsOnAForwardWirePath()
		{
			const int radialSegments = 8;
			const int capVertexCount = radialSegments + 1;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(400f, 650f, 200f)) {
					Rotation = quaternion.RotateX(math.PI * 0.5f),
				}, TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetRailCount(1);
				component.SetRailOffset(0, 0, Vector2.zero);
				component.SetRailOffset(1, 0, new Vector2(500f, 0f));
				component.SetWireTransitionCurve(0, 0, new AnimationCurve(
					new Keyframe(0f, 0f, 0f, 0f),
					new Keyframe(1f, 1f, 2f, 2f)));

				var firstSamples = WireRailRenderMeshGenerator.BuildSampleParameters(
					spline, component.Segments, 0, 0, 16);
				var secondSamples = WireRailRenderMeshGenerator.BuildSampleParameters(
					spline, component.Segments, 1, 0, 16);
				var secondTubeStart = firstSamples.Count * radialSegments + capVertexCount;
				var lastRingStart = secondTubeStart
					+ (secondSamples.Count - 1) * radialSegments;
				var vertices = component.RenderMesh.vertices;
				var center = AverageRing(vertices, lastRingStart, radialSegments);
				var tangentStep = 1f - secondSamples[^2];
				Assert.That(WireRailSplineGeometry.TryEvaluateRailFrame(spline,
					component.Segments, 1, 0, 1f, tangentStep, out var frame), Is.True);

				AssertRingPerpendicular(vertices, lastRingStart, radialSegments, center,
					frame.Tangent);
				Assert.That(secondSamples.Count, Is.GreaterThan(17),
					"curved wire spans should receive additional render rings");

				Assert.That(WireRailSplineGeometry.TryEvaluateRailPosition(spline,
					component.Segments, 1, 0, 0f, out var previousPosition), Is.True);
				for (var sampleIndex = 1; sampleIndex <= 128; sampleIndex++) {
					var curveT = sampleIndex / 128f;
					Assert.That(WireRailSplineGeometry.TryEvaluateRailPosition(spline,
						component.Segments, 1, 0, curveT, out var position), Is.True);
					Assert.That(WireRailSplineGeometry.TryEvaluateLayout(spline,
						component.Segments, 1, curveT,
						out var mainFrame), Is.True);
					var direction = math.normalizesafe(position - previousPosition,
						mainFrame.Tangent);
					Assert.That(math.dot(direction, mainFrame.Tangent), Is.GreaterThan(0f),
						$"wire path reversed at t={curveT}");
					previousPosition = position;
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldBlendTheBallChannelThroughAContinuousConnection()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(0f, 1000f, 0f)) {
						Rotation = quaternion.RotateX(math.PI * 0.5f),
					}, TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				var secondOffsets = new Vector2[4];
				var indices = new int[4];
				for (var wireIndex = 0; wireIndex < 4; wireIndex++) {
					indices[wireIndex] = wireIndex;
					secondOffsets[wireIndex] = component.Segments[1].GetRailOffset(wireIndex)
						+ new Vector2(40f, 0f);
				}
				component.SetWireProperties(1, indices, secondOffsets);
				var transitionCurve = new AnimationCurve(
					new Keyframe(0f, 0f), new Keyframe(0.5f, 0.25f),
					new Keyframe(1f, 1f));
				for (var wireIndex = 0; wireIndex < 4; wireIndex++) {
					component.SetWireTransitionCurve(0, wireIndex, transitionCurve);
				}
				Assert.That(WireRailChannelProfile.TryCreate(
					WireRailLayout.CreateDefaultOffsets(4), 4f, 25f,
					out var profile, out var error), Is.True, error);
				var rowSize = profile.Vertices.Count;
				var vertices = component.ColliderMesh.vertices;
				var firstStartX = AverageRowX(vertices, 0, rowSize);
				var firstMiddleX = AverageRowX(vertices, 4 * rowSize, rowSize);
				var firstEndX = AverageRowX(vertices, 8 * rowSize, rowSize);
				var secondStartX = AverageRowX(vertices, 9 * rowSize, rowSize);
				var secondMiddleX = AverageRowX(vertices, 13 * rowSize, rowSize);
				var secondEndX = AverageRowX(vertices, 17 * rowSize, rowSize);

				Assert.That(firstMiddleX - firstStartX, Is.EqualTo(10f).Within(0.01f));
				Assert.That(firstEndX - firstStartX, Is.EqualTo(40f).Within(0.01f));
				Assert.That(secondStartX, Is.EqualTo(firstEndX).Within(0.01f));
				Assert.That(secondMiddleX - firstStartX, Is.EqualTo(40f).Within(0.01f));
				Assert.That(secondEndX - firstStartX, Is.EqualTo(40f).Within(0.01f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepLayoutConnectionsWhenAKnotSplitsTheRoute()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(0f, 1000f, 0f)),
					TangentMode.AutoSmooth);
				AddMidpointLayout(component);
				component.SetWireTransitionCurve(0, 0, new AnimationCurve(
					new Keyframe(0f, 0f), new Keyframe(0.5f, 0.2f),
					new Keyframe(1f, 1f)));

				spline.Insert(1, new BezierKnot(new float3(0f, 250f, 0f)),
					TangentMode.AutoSmooth);

				Assert.That(component.Segments[0].ConnectionToNext.IsWireContinuous(0),
					Is.True);
				Assert.That(component.Segments[0].ConnectionToNext.GetWireCurve(0)
					.Evaluate(0.5f), Is.EqualTo(0.2f).Within(0.001f));
				Assert.That(component.Segments, Has.Count.EqualTo(2));

				spline.RemoveAt(1);

				Assert.That(component.Segments[0].ConnectionToNext.IsWireContinuous(0),
					Is.True);
				Assert.That(component.Segments[0].ConnectionToNext.GetWireCurve(0)
					.Evaluate(0.5f), Is.EqualTo(0.2f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldApplyUsefulLayoutsFromOneThroughFiveRails()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();

				component.SetRailCount(1);
				AssertOffsets(component.Segments[0], Vector2.zero);

				component.SetRailCount(2);
				AssertOffsets(component.Segments[0],
					new Vector2(-19f, 0f), new Vector2(19f, 0f));

				component.SetRailCount(3);
				AssertOffsets(component.Segments[0],
					new Vector2(-19f, 0f), new Vector2(19f, 0f),
					new Vector2(19f, 44f));
				component.SetThirdRailSide(0, WireRailThirdRailSide.Left);
				Assert.That(component.Segments[0].GetRailOffset(2),
					Is.EqualTo(new Vector2(-19f, 44f)));

				component.SetRailCount(4);
				AssertOffsets(component.Segments[0],
					new Vector2(-19f, 0f), new Vector2(19f, 0f),
					new Vector2(-19f, 44f), new Vector2(19f, 44f));

				component.SetRailCount(5);
				Assert.That(component.Segments[0].GetRailOffset(4),
					Is.EqualTo(new Vector2(0f, 52f)));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDistributeAdditionalRailsAcrossTheTop()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(7);

			Assert.That(offsets, Has.Length.EqualTo(7));
			Assert.That(offsets[4], Is.EqualTo(new Vector2(-19f, 52f)));
			Assert.That(offsets[5], Is.EqualTo(new Vector2(0f, 52f)));
			Assert.That(offsets[6], Is.EqualTo(new Vector2(19f, 52f)));
		}

		[Test]
		public void ShouldEditSelectedWirePositionsTogether()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetWireProperties(0, new[] { 0, 2 },
					new[] { new Vector2(-22f, 1f), new Vector2(-20f, 46f) });

				Assert.That(component.Segments[0].GetRailOffset(0),
					Is.EqualTo(new Vector2(-22f, 1f)));
				Assert.That(component.Segments[0].GetWireDiameter(0), Is.EqualTo(8f));
				Assert.That(component.Segments[0].GetWireDiameter(1), Is.EqualTo(8f));
				Assert.That(component.Segments[0].GetWireDiameter(2), Is.EqualTo(8f));
				Assert.That(component.RenderMesh.bounds.min.x, Is.EqualTo(-26f).Within(0.05f));
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldApplySelectedWirePositionsToEveryLayout()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(250f);
				component.AddLayout(500f);
				component.SetRailCount(4);
				component.SetRailOffset(0, 0, new Vector2(-31f, 2f));
				component.SetRailOffset(0, 2, new Vector2(-29f, 49f));
				component.SetRailOffset(1, 0, new Vector2(-20f, 5f));
				component.SetRailOffset(1, 1, new Vector2(24f, 6f));
				component.SetRailOffset(1, 2, new Vector2(-18f, 45f));
				component.SetRailOffset(2, 0, new Vector2(-16f, 8f));
				component.SetRailOffset(2, 2, new Vector2(-15f, 42f));
				component.SetRailsActive(2, new[] { 0 }, false);

				component.ApplyWirePositionsToAllLayouts(0, new[] { 0, 2 });

				foreach (var layout in component.Layouts) {
					Assert.That(layout.GetRailOffset(0),
						Is.EqualTo(new Vector2(-31f, 2f)));
					Assert.That(layout.GetRailOffset(2),
						Is.EqualTo(new Vector2(-29f, 49f)));
				}
				Assert.That(component.Layouts[1].GetRailOffset(1),
					Is.EqualTo(new Vector2(24f, 6f)));
				Assert.That(component.Layouts[2].IsRailActive(0), Is.False);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldCenterThePivotWithoutMovingTheSpline()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var container = component.SplineContainer;
				var spline = container.Spline;
				spline.Insert(1, new BezierKnot(new float3(100f, 180f, 35f)),
					TangentMode.AutoSmooth);
				go.transform.position = new Vector3(17f, 29f, -11f);
				go.transform.rotation = Quaternion.Euler(13f, 27f, 9f);
				go.transform.localScale = new Vector3(1.25f, 0.8f, 1.6f);
				var worldKnotPositions = Enumerable.Range(0, spline.Count)
					.Select(index => container.transform.TransformPoint((Vector3)spline[index].Position))
					.ToArray();
				Assert.That(WireRailSplineGeometry.TryEvaluateLayoutPosition(spline,
					component.Layouts, 0, 0.5f, out var midpoint), Is.True);
				var expectedPivot = container.transform.TransformPoint((Vector3)midpoint);
				var originalRotation = go.transform.rotation;
				var originalScale = go.transform.localScale;

				Assert.That(component.CenterPivot(), Is.True);

				Assert.That(Vector3.Distance(go.transform.position, expectedPivot),
					Is.LessThan(0.0001f));
				Assert.That(Quaternion.Angle(go.transform.rotation, originalRotation),
					Is.LessThan(0.0001f));
				Assert.That(go.transform.localScale, Is.EqualTo(originalScale));
				for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++) {
					var worldPosition = container.transform.TransformPoint(
						(Vector3)spline[knotIndex].Position);
					Assert.That(Vector3.Distance(worldPosition, worldKnotPositions[knotIndex]),
						Is.LessThan(0.0001f));
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldApplyOneDiameterToEveryWireAndFixture()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(250f);
				component.SetRailCount(5);
				component.AddBraceFixture(125f);
				component.AddCrossWireFixture(375f);

				component.SetWireDiameter(12f);

				foreach (var layout in component.Layouts) {
					for (var wireIndex = 0; wireIndex < layout.RailCount; wireIndex++) {
						Assert.That(layout.GetWireDiameter(wireIndex), Is.EqualTo(12f));
					}
				}
				Assert.That(component.WireDiameter, Is.EqualTo(12f));
				Assert.That(((WireRailBraceFixture)component.Fixtures[0]).Diameter,
					Is.EqualTo(12f));
				Assert.That(((WireRailCrossWireFixture)component.Fixtures[1]).Diameter,
					Is.EqualTo(12f));
				Assert.That(component.RenderMesh.bounds.size.x, Is.GreaterThan(0f));
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepLayoutActivationAndPositionsIndependent()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Add(
					new BezierKnot(new float3(50f, 750f, 100f)), TangentMode.AutoSmooth);
				AddMidpointLayout(component);

				component.SetRailCount(5);
				component.SetRailsActive(0, new[] { 2, 3, 4 }, false);
				component.SetRailOffset(1, 4, new Vector2(3f, 61f));

				Assert.That(component.RailCount, Is.EqualTo(5));
				Assert.That(component.Segments[0].RailCount, Is.EqualTo(5));
				Assert.That(component.Segments[1].RailCount, Is.EqualTo(5));
				Assert.That(CountActiveRails(component.Segments[0]), Is.EqualTo(2));
				Assert.That(CountActiveRails(component.Segments[1]), Is.EqualTo(5));
				Assert.That(component.Segments[1].GetRailOffset(4),
					Is.EqualTo(new Vector2(3f, 61f)));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldNotCreateALayoutWhenAKnotSplitsTheRoute()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(3);
				component.SetRailOffset(0, 2, new Vector2(23f, 48f));
				component.SetWireDiameter(11f);

				component.SplineContainer.Spline.Insert(1,
					new BezierKnot(new float3(0f, 250f, 25f)), TangentMode.AutoSmooth);

				Assert.That(component.Segments, Has.Count.EqualTo(1));
				Assert.That(component.Segments[0].GetRailOffset(2),
					Is.EqualTo(new Vector2(23f, 48f)));
				Assert.That(component.Segments[0].GetWireDiameter(2), Is.EqualTo(11f));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepDistanceLayoutsWhenInsertingAndRemovingAKnot()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(100f, 1000f, 100f)),
					TangentMode.AutoSmooth);
				component.AddLayout(600f);
				component.SetRailCount(5);
				component.SetRailsActive(0, new[] { 2, 3, 4 }, false);

				spline.Insert(1, new BezierKnot(new float3(25f, 250f, 20f)),
					TangentMode.AutoSmooth);

				Assert.That(component.Segments, Has.Count.EqualTo(2));
				Assert.That(component.Segments[0].RailCount, Is.EqualTo(5));
				Assert.That(component.Segments[1].RailCount, Is.EqualTo(5));
				Assert.That(CountActiveRails(component.Segments[0]), Is.EqualTo(2));
				Assert.That(CountActiveRails(component.Segments[1]), Is.EqualTo(5));
				Assert.That(component.Segments[1].Distance, Is.EqualTo(600f).Within(0.001f));

				spline.RemoveAt(1);

				Assert.That(component.Segments, Has.Count.EqualTo(2));
				Assert.That(component.Segments[0].RailCount, Is.EqualTo(5));
				Assert.That(component.Segments[1].RailCount, Is.EqualTo(5));
				Assert.That(CountActiveRails(component.Segments[0]), Is.EqualTo(2));
				Assert.That(CountActiveRails(component.Segments[1]), Is.EqualTo(5));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepLayoutsWhenTheSplineClosesAndOpens()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				spline.Add(new BezierKnot(new float3(100f, 600f, 50f)),
					TangentMode.AutoSmooth);

				Assert.That(component.Segments, Has.Count.EqualTo(1));
				spline.Closed = true;
				Assert.That(component.Segments, Has.Count.EqualTo(1));
				spline.Closed = false;
				Assert.That(component.Segments, Has.Count.EqualTo(1));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldGenerateVisibleOctagonalWireTubes()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var mesh = component.RenderMesh;

				Assert.That(mesh, Is.Not.Null);
				Assert.That(mesh.vertexCount, Is.GreaterThan(0));
				Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount));
				Assert.That(mesh.bounds.min.x, Is.EqualTo(-23f).Within(0.05f));
				Assert.That(mesh.bounds.max.x, Is.EqualTo(23f).Within(0.05f));
				Assert.That(mesh.hideFlags & HideFlags.DontSaveInEditor,
					Is.EqualTo(HideFlags.DontSaveInEditor));
				Assert.That(mesh.hideFlags & HideFlags.DontSaveInBuild,
					Is.EqualTo(HideFlags.DontSaveInBuild));
				Assert.That(component.ColliderMesh.hideFlags & HideFlags.DontSaveInEditor,
					Is.EqualTo(HideFlags.DontSaveInEditor));
				Assert.That(component.ColliderMesh.hideFlags & HideFlags.DontSaveInBuild,
					Is.EqualTo(HideFlags.DontSaveInBuild));
				var serializedComponent = new SerializedObject(component);
				Assert.That(serializedComponent.FindProperty("_renderMesh"), Is.Null);
				Assert.That(serializedComponent.FindProperty("_colliderMesh"), Is.Null);
				Assert.That(component.SplineContainer.GetComponent<MeshFilter>().sharedMesh,
					Is.SameAs(mesh));
				Assert.That(component.SplineContainer.GetComponent<MeshRenderer>(), Is.Not.Null);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldReleaseAndRegenerateDerivedMeshesWhenDisabled()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var renderMesh = component.RenderMesh;
				var colliderMesh = component.ColliderMesh;

				component.enabled = false;

				Assert.That(renderMesh == null, Is.True);
				Assert.That(colliderMesh == null, Is.True);
				Assert.That(component.RenderMesh, Is.Null);
				Assert.That(component.ColliderMesh, Is.Null);

				component.enabled = true;
				Assert.That(component.RenderMesh, Is.Not.Null);
				Assert.That(component.ColliderMesh, Is.Not.Null);
				Assert.That(component.RenderMesh, Is.Not.SameAs(renderMesh));
				Assert.That(component.ColliderMesh, Is.Not.SameAs(colliderMesh));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(2, 3, false)]
		[TestCase(3, 5, false)]
		[TestCase(4, 7, false)]
		[TestCase(5, 8, true)]
		[TestCase(9, 8, true)]
		public void ShouldCreateOneSelectivelyOpenBallChannel(int railCount,
			int expectedFacets, bool expectedClosed)
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(railCount);

			Assert.That(WireRailChannelProfile.TryCreate(offsets, 4f, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(profile.Spans, Has.Count.EqualTo(expectedFacets));
			Assert.That(profile.IsClosed, Is.EqualTo(expectedClosed));
			Assert.That(profile.Spans.Count, Is.LessThanOrEqualTo(8));
		}

		[Test]
		public void ShouldFitTheChannelToDifferentWireDiameters()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(2);
			var radii = new[] { 3f, 6f };

			Assert.That(WireRailChannelProfile.TryCreate(offsets, radii, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(math.distance(profile.RestingBallCenter, (float2)offsets[0]),
				Is.EqualTo(28f).Within(0.01f));
			Assert.That(math.distance(profile.RestingBallCenter, (float2)offsets[1]),
				Is.EqualTo(31f).Within(0.01f));
		}

		[Test]
		public void ShouldGenerateAChannelColliderInsteadOfPerWireTubes()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();

				Assert.That(component.ColliderMesh, Is.Not.Null);
				Assert.That(component.ColliderMesh.triangles, Has.Length.EqualTo(224 * 3));
				Assert.That(component.ColliderMesh.normals, Is.Empty);
				Assert.That(component.ColliderMesh.uv, Is.Empty);
				Assert.That(component.RenderMesh.vertexCount,
					Is.GreaterThan(component.ColliderMesh.vertexCount));

				component.SetRailCount(2);
				Assert.That(component.ColliderMesh.triangles, Has.Length.EqualTo(96 * 3));
				Assert.That(component, Is.InstanceOf<ICollidableComponent>());
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRebuildGeometryWhenTheSplineMoves()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				var end = spline[1];
				end.Position = new float3(150f, 500f, 100f);

				spline.SetKnot(1, end);

				Assert.That(component.RenderMesh.bounds.max.x, Is.GreaterThan(140f));
				Assert.That(component.ColliderMesh.bounds.max.x, Is.GreaterThan(130f));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldExpandTheChannelMeshIntoVpePhysicsColliders()
		{
			var go = new GameObject("Wire Rail");
			var transforms = new NativeParallelHashMap<int, float4x4>(0, Allocator.Temp);
			var colliders = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var component = go.AddComponent<WireRailComponent>();
				var collidable = (ICollidableComponent)component;

				collidable.GetColliders(null, null, ref colliders, float4x4.identity, 0f);

				Assert.That(collidable.IsCollidable, Is.True);
				Assert.That(colliders.Count, Is.EqualTo(360));
				for (var i = 0; i < colliders.Count; i++) {
					Assert.That(colliders[i], Is.Not.InstanceOf<CircleCollider>(),
						"the channel must not expand into per-wire tube colliders");
				}
			}
			finally {
				colliders.Dispose();
				transforms.Dispose();
				Object.DestroyImmediate(go);
			}
		}

		private static void AddMidpointLayout(WireRailComponent component)
		{
			component.AddLayout(component.SplineLength * 0.5f);
		}

		private static void AssertOffsets(WireRailSegment segment,
			params Vector2[] expected)
		{
			Assert.That(segment.RailCount, Is.EqualTo(expected.Length));
			for (var i = 0; i < expected.Length; i++) {
				Assert.That(segment.GetRailOffset(i), Is.EqualTo(expected[i]));
			}
		}

		private static int CountActiveRails(WireRailSegment segment)
			=> Enumerable.Range(0, segment.RailCount).Count(segment.IsRailActive);

		private static float AverageRowX(Vector3[] vertices, int start, int count)
		{
			var sum = 0f;
			for (var index = start; index < start + count; index++) {
				sum += vertices[index].x;
			}
			return sum / count;
		}

		private static Vector3 AverageRing(Vector3[] vertices, int start, int count)
		{
			var sum = Vector3.zero;
			for (var index = start; index < start + count; index++) {
				sum += vertices[index];
			}
			return sum / count;
		}

		private static void AssertRingPerpendicular(Vector3[] vertices, int start,
			int count, Vector3 center, float3 tangent)
		{
			for (var index = start; index < start + count; index++) {
				var radial = (float3)(vertices[index] - center);
				Assert.That(math.abs(math.dot(radial, tangent)), Is.LessThan(0.001f));
			}
		}

	}
}
