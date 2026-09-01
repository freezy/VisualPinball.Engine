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
		public void ShouldCreateFourRailsOnTheDefaultAuthoringGrid()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();

				Assert.That(component.Segments, Has.Count.EqualTo(1));
				AssertOffsets(component.Segments[0],
					new Vector2(-15f, 0f),
					new Vector2(15f, 0f),
					new Vector2(-30f, 30f),
					new Vector2(30f, 30f));
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
			const int radialSegments = 10;
			const int trianglesPerRingPair = radialSegments * 2;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetWireCapBevelSize(0f);
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
			const int radialSegments = 10;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetWireCapBevelSize(0f);
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
			const int radialSegments = 10;
			const int capVertexCount = radialSegments + 1;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetWireCapBevelSize(0f);
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
		public void ShouldFitTheDefaultCrossWireArmsToTheBottomAndMiddleRails()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var railTriangleCount = component.RenderMesh.triangles.Length / 3;
				var fixtureIndex = component.AddVBraceFixture(250f);
				var vBrace = (WireRailVBraceFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var profile), Is.True);
				Assert.That(vBrace.Angle,
					Is.EqualTo(WireRailVBraceFixture.DefaultAngle).Within(0.001f));
				Assert.That(vBrace.BottomLength,
					Is.EqualTo(WireRailVBraceFixture.DefaultBottomLength));
				Assert.That(vBrace.LeftLength,
					Is.EqualTo(WireRailVBraceFixture.DefaultLeftLength));
				Assert.That(vBrace.RightLength,
					Is.EqualTo(WireRailVBraceFixture.DefaultRightLength));
				Assert.That(profile.RailOffsets, Has.Count.EqualTo(4));

				var centerline = profile.CenterlinePoints.Select(ToOffset).ToArray();
				var leftDirection = math.normalize(centerline[1] - centerline[0]);
				var rightDirection = math.normalize(centerline[^1] - centerline[^2]);
				AssertTouches(0, centerline[0], leftDirection);
				AssertTouches(2, centerline[0], leftDirection);
				AssertTouches(1, centerline[^1], rightDirection);
				AssertTouches(3, centerline[^1], rightDirection);
				var leftUp = -leftDirection;
				var includedAngle = math.degrees(math.acos(math.clamp(
					math.dot(leftUp, rightDirection), -1f, 1f)));
				Assert.That(includedAngle, Is.EqualTo(vBrace.Angle).Within(0.01f));
				Assert.That(component.RenderMesh.triangles.Length / 3,
					Is.GreaterThan(railTriangleCount));

				float2 ToOffset(float3 point)
				{
					var relative = point - profile.Frame.Position;
					return new float2(math.dot(relative, profile.Frame.Right),
						math.dot(relative, profile.Frame.Up));
				}

				void AssertTouches(int railIndex, float2 linePoint, float2 direction)
				{
					var relative = profile.RailOffsets[railIndex] - linePoint;
					var distance = math.abs(direction.x * relative.y
						- direction.y * relative.x);
					Assert.That(distance, Is.EqualTo(profile.RailRadii[railIndex]
						+ vBrace.Diameter * 0.5f).Within(0.01f));
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldAlwaysGenerateTheBottomWireAndRoundItsArmCorners()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddVBraceFixture(250f);
				var vBrace = (WireRailVBraceFixture)component.Fixtures[fixtureIndex];
				component.SetVBraceFixtureProperties(fixtureIndex, vBrace.Distance,
					64, 0f, 0f, 24f, vBrace.LeftLength, vBrace.RightLength,
					vBrace.Angle, 0f, 12f);

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var rounded), Is.True);
				var roundedOffsets = rounded.CenterlinePoints.Select(point =>
					ToOffset(rounded, point)).ToArray();
				Assert.That(roundedOffsets.Zip(roundedOffsets.Skip(1),
					(left, right) => math.abs(left.y - rounded.OriginOffset.y) < 0.001f
						&& math.abs(right.y - rounded.OriginOffset.y) < 0.001f
						&& math.abs(left.x - right.x) > 0.1f).Any(matches => matches),
					Is.True, "the rounded corners should retain a straight bottom span");
				Assert.That(CalculateMaximumTurn(roundedOffsets),
					Is.LessThanOrEqualTo(360f / 64f + 0.1f));

				component.SetVBraceFixtureProperties(fixtureIndex, vBrace.Distance,
					3, 0f, 0f, vBrace.BottomLength, vBrace.LeftLength,
					vBrace.RightLength, vBrace.Angle, 0f, 12f);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var lowDensity), Is.True);
				var lowDensityOffsets = lowDensity.CenterlinePoints.Select(point =>
					ToOffset(lowDensity, point)).ToArray();
				Assert.That(CalculateMaximumTurn(lowDensityOffsets),
					Is.LessThanOrEqualTo(15.1f),
					"low density must not introduce a visible miter waist");

				static float2 ToOffset(WireRailVBraceProfile profile, float3 point)
				{
					var relative = point - profile.Frame.Position;
					return new float2(math.dot(relative, profile.Frame.Right),
						math.dot(relative, profile.Frame.Up));
				}
				static float CalculateMaximumTurn(IReadOnlyList<float2> points)
				{
					var maximumTurn = 0f;
					for (var pointIndex = 1; pointIndex < points.Count - 1; pointIndex++) {
						var incoming = math.normalize(points[pointIndex]
							- points[pointIndex - 1]);
						var outgoing = math.normalize(points[pointIndex + 1]
							- points[pointIndex]);
						maximumTurn = math.max(maximumTurn, math.degrees(math.acos(
							math.clamp(math.dot(incoming, outgoing), -1f, 1f))));
					}
					return maximumTurn;
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOffsetRotateResizeAndDuplicateACrossWireWithArms()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddVBraceFixture(250f);
				var vBrace = (WireRailVBraceFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var original), Is.True);

				component.SetVBraceFixtureProperties(fixtureIndex, 175f, 48,
					6f, -4f, 24f, 70f, 95f, 60f, 30f, 10f);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var changed), Is.True);
				Assert.That(math.distance(changed.OriginOffset,
					original.OriginOffset + new float2(6f, -4f)), Is.LessThan(0.001f));
				var endpoints = new[] {
					ToOffset(changed, changed.CenterlinePoints[0]),
					ToOffset(changed, changed.CenterlinePoints[^1]),
				};
				var leftBottom = changed.OriginOffset + Rotate(new float2(-12f, 0f), 30f);
				var rightBottom = changed.OriginOffset + Rotate(new float2(12f, 0f), 30f);
				Assert.That(math.distance(endpoints[0], leftBottom),
					Is.EqualTo(70f).Within(0.001f));
				Assert.That(math.distance(endpoints[1], rightBottom),
					Is.EqualTo(95f).Within(0.001f));
				var expectedLeftDirection = Rotate(new float2(-0.5f,
					math.cos(math.radians(30f))), 30f);
				Assert.That(math.dot(math.normalize(endpoints[0] - leftBottom),
					expectedLeftDirection), Is.EqualTo(1f).Within(0.001f));

				var duplicateIndex = component.DuplicateVBraceFixture(fixtureIndex);
				var duplicate = (WireRailVBraceFixture)component.Fixtures[duplicateIndex];
				Assert.That(duplicate, Is.Not.SameAs(vBrace));
				Assert.That(duplicate.Distance, Is.EqualTo(vBrace.Distance));
				Assert.That(duplicate.RingDensity, Is.EqualTo(vBrace.RingDensity));
				Assert.That(duplicate.LateralOffset, Is.EqualTo(vBrace.LateralOffset));
				Assert.That(duplicate.VerticalOffset, Is.EqualTo(vBrace.VerticalOffset));
				Assert.That(duplicate.BottomLength, Is.EqualTo(vBrace.BottomLength));
				Assert.That(duplicate.LeftLength, Is.EqualTo(vBrace.LeftLength));
				Assert.That(duplicate.RightLength, Is.EqualTo(vBrace.RightLength));
				Assert.That(duplicate.Angle, Is.EqualTo(vBrace.Angle));
				Assert.That(duplicate.Rotation, Is.EqualTo(vBrace.Rotation));
				Assert.That(duplicate.CornerRadius, Is.EqualTo(vBrace.CornerRadius));

				static float2 ToOffset(WireRailVBraceProfile profile, float3 point)
				{
					var relative = point - profile.Frame.Position;
					return new float2(math.dot(relative, profile.Frame.Right),
						math.dot(relative, profile.Frame.Up));
				}
				static float2 Rotate(float2 point, float degrees)
				{
					var direction = new float2(math.cos(math.radians(degrees)),
						math.sin(math.radians(degrees)));
					return new float2(point.x * direction.x - point.y * direction.y,
						point.x * direction.y + point.y * direction.x);
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldClampCrossWireArmSettingsAndShareTheWireDiameter()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddVBraceFixture(250f);
				component.SetVBraceFixtureProperties(fixtureIndex, 250f, 1000,
					0f, 0f, -1000f, -5f, -10f, 500f, 500f, 0.1f);
				var vBrace = (WireRailVBraceFixture)component.Fixtures[fixtureIndex];

				Assert.That(vBrace.RingDensity, Is.EqualTo(128));
				Assert.That(vBrace.BottomLength, Is.EqualTo(0.1f));
				Assert.That(vBrace.LeftLength, Is.Zero);
				Assert.That(vBrace.RightLength, Is.Zero);
				Assert.That(vBrace.Angle, Is.EqualTo(179f));
				Assert.That(vBrace.Rotation, Is.EqualTo(360f));
				Assert.That(vBrace.CornerRadius,
					Is.EqualTo(component.WireDiameter * 0.5f));

				component.SetVBraceFixtureProperties(fixtureIndex, 250f,
					vBrace.RingDensity, 0f, 0f, 0.1f, 0.1f, 0f,
					WireRailVBraceFixture.DefaultAngle, 0f, vBrace.CornerRadius);
				Assert.That(vBrace.BottomLength, Is.GreaterThan(0.1f));
				Assert.That(vBrace.LeftLength, Is.GreaterThan(0.1f));
				Assert.That(vBrace.RightLength, Is.Zero);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace, out _),
					Is.True);

				component.SetWireDiameter(12f);
				Assert.That(vBrace.Diameter, Is.EqualTo(12f));
				Assert.That(vBrace.CornerRadius, Is.GreaterThanOrEqualTo(6f));
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace, out _),
					Is.True);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOmitZeroLengthArmsWithoutOmittingTheBottomWire()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddVBraceFixture(250f);
				var vBrace = (WireRailVBraceFixture)component.Fixtures[fixtureIndex];

				component.SetVBraceFixtureProperties(fixtureIndex, vBrace.Distance,
					vBrace.RingDensity, 0f, 0f, 40f, 0f, 0f, 60f,
					0f, vBrace.CornerRadius);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var bottomOnly), Is.True);
				Assert.That(bottomOnly.CenterlinePoints.Count, Is.EqualTo(2));
				Assert.That(math.distance(bottomOnly.CenterlinePoints[0],
					bottomOnly.CenterlinePoints[1]), Is.EqualTo(40f).Within(0.001f));

				component.SetVBraceFixtureProperties(fixtureIndex, vBrace.Distance,
					vBrace.RingDensity, 0f, 0f, 40f, 0f, 50f, 60f,
					0f, vBrace.CornerRadius);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var rightArmOnly), Is.True);
				Assert.That(rightArmOnly.CenterlinePoints.Count, Is.GreaterThan(2));
				var firstOffset = ToOffset(rightArmOnly,
					rightArmOnly.CenterlinePoints[0]);
				var lastOffset = ToOffset(rightArmOnly,
					rightArmOnly.CenterlinePoints[^1]);
				Assert.That(math.distance(firstOffset,
					rightArmOnly.OriginOffset + new float2(-20f, 0f)), Is.LessThan(0.001f));
				Assert.That(math.distance(lastOffset,
					rightArmOnly.OriginOffset + new float2(20f, 0f)),
					Is.EqualTo(50f).Within(0.001f));

				static float2 ToOffset(WireRailVBraceProfile profile, float3 point)
				{
					var relative = point - profile.Frame.Position;
					return new float2(math.dot(relative, profile.Frame.Right),
						math.dot(relative, profile.Frame.Up));
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepCrossWireArmsAvailableWithFewerThanFourActiveRails()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(3);
				var fixtureIndex = component.AddVBraceFixture(250f);
				var vBrace = (WireRailVBraceFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var threeRailProfile), Is.True);
				Assert.That(threeRailProfile.RailOffsets, Has.Count.EqualTo(3));

				component.SetRailsActive(0, new[] { 2 }, false);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var twoRailProfile), Is.True);
				Assert.That(twoRailProfile.RailOffsets, Has.Count.EqualTo(2));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(false)]
		[TestCase(true)]
		public void ShouldFallBackBelowTheEnvelopeForAmbiguousFourRailLayouts(
			bool swapBottomRails)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				if (swapBottomRails) {
					component.SetRailOffset(0, 0, new Vector2(15f, 0f));
					component.SetRailOffset(0, 1, new Vector2(-15f, 0f));
				} else {
					component.SetRailOffset(0, 2, new Vector2(-10f, 30f));
					component.SetRailOffset(0, 3, new Vector2(10f, 30f));
				}
				var fixtureIndex = component.AddVBraceFixture(250f);
				var vBrace = (WireRailVBraceFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateVBraceProfile(
					component.SplineContainer.Spline, component.Segments, vBrace,
					out var profile), Is.True);
				var segment = component.Segments[0];
				var minimumHeight = Enumerable.Range(0, segment.RailCount)
					.Where(segment.IsRailActive)
					.Min(railIndex => segment.GetRailOffset(railIndex).y
						- segment.GetWireDiameter(railIndex) * 0.5f);
				Assert.That(profile.OriginOffset.y, Is.LessThan(minimumHeight),
					"invalid outer-tangent intersections must use the below-envelope fallback");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldConnectTheTwoBottomRailsWithACrossWireByDefault()
		{
			const int radialSegments = 10;
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
				var touches = new List<WireRailTouch>();
				WireRailSolderMeshGenerator.CollectTouches(component.SplineContainer.Spline,
					component.Segments, component.Fixtures, crossWire, touches);
				Assert.That(component.RenderMesh.triangles.Length / 3 - railTriangleCount,
					Is.EqualTo(radialSegments * 8 + touches.Count
						* WireRailSolderMeshGenerator.TrianglesPerBlob),
					"the cross wire should have one tube span, two beveled caps, and its solder joins");
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
			const int radialSegments = 10;
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
		public void ShouldCreateAConnectedLegAndUHookFootByDefault()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var railTriangleCount = component.RenderMesh.triangles.Length / 3;
				var fixtureIndex = component.AddLegFixture(250f);
				var leg = (WireRailLegFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments, leg,
					out var profile), Is.True);
				Assert.That(leg.LegSide, Is.EqualTo(WireRailLegSide.Right));
				Assert.That(leg.LateralOffset, Is.Zero);
				Assert.That(leg.VerticalOffset, Is.Zero);
				Assert.That(leg.LengthAdjustment, Is.Zero);
				Assert.That(math.distance(profile.LegPoints[0],
					profile.AttachmentProfile.End), Is.LessThan(0.001f),
					"the vertical part of the L should begin at the right end by default");
				Assert.That(math.distance(profile.CombinedPath[0],
					profile.AttachmentProfile.Start), Is.LessThan(0.001f));
				Assert.That(profile.CombinedPath.Any(point => math.distance(
					point, profile.LegPoints[0]) < 0.001f), Is.False,
					"the attachment-to-leg corner should be replaced by a rounded bend");
				Assert.That(math.distance(profile.LegPoints[0], profile.LegPoints[1]),
					Is.EqualTo(WireRailLegFixture.DefaultStartLength).Within(0.001f));
				Assert.That(math.dot(math.normalize(profile.LegPoints[1]
					- profile.LegPoints[0]), -profile.AttachmentProfile.Frame.Up),
					Is.EqualTo(1f).Within(0.001f));
				Assert.That(math.distance(profile.LegPoints[^1], profile.FootPoints[0]),
					Is.LessThan(0.001f), "the leg must meet the open end of the U-hook");
				Assert.That(leg.FootConnectionLength,
					Is.EqualTo(WireRailLegFixture.DefaultFootConnectionLength));
				Assert.That(math.distance(profile.FootPoints[0], profile.FootPoints[1]),
					Is.EqualTo(WireRailLegFixture.DefaultFootConnectionLength)
						.Within(0.001f));
				Assert.That(profile.FootPoints.Count,
					Is.EqualTo(WireRailLegFixture.FootBendSegments + 3));
				Assert.That(component.RenderMesh.triangles.Length / 3,
					Is.GreaterThan(railTriangleCount));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldOffsetAndResizeTheLegRailAttachment()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddLegFixture(250f);
				var leg = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments, leg,
					out var original), Is.True);

				component.SetLegFixtureProperties(fixtureIndex, leg.Distance, leg.LegSide,
					leg.StartDirection, leg.StartLength, leg.FootPosition, leg.FootRotation,
					leg.FootWidth, leg.FootLength, leg.FootConnectionLength,
					6f, -3f, 12f);

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments, leg,
					out var adjusted), Is.True);
				Assert.That(leg.LateralOffset, Is.EqualTo(6f));
				Assert.That(leg.VerticalOffset, Is.EqualTo(-3f));
				Assert.That(leg.LengthAdjustment, Is.EqualTo(12f));
				var originalCenter = (original.AttachmentProfile.Start
					+ original.AttachmentProfile.End) * 0.5f;
				var adjustedCenter = (adjusted.AttachmentProfile.Start
					+ adjusted.AttachmentProfile.End) * 0.5f;
				Assert.That(math.distance(adjustedCenter, originalCenter
					+ original.AttachmentProfile.Frame.Right * 6f
					- original.AttachmentProfile.Frame.Up * 3f), Is.LessThan(0.001f));
				Assert.That(math.distance(adjusted.AttachmentProfile.Start,
					adjusted.AttachmentProfile.End), Is.EqualTo(math.distance(
						original.AttachmentProfile.Start, original.AttachmentProfile.End)
						+ 12f).Within(0.001f));
				Assert.That(math.distance(adjusted.LegPoints[0],
					adjusted.AttachmentProfile.End), Is.LessThan(0.001f));
				var legDelta = adjusted.LegPoints[0] - original.LegPoints[0];
				for (var pointIndex = 0; pointIndex < original.FootPoints.Count;
					pointIndex++) {
					Assert.That(math.distance(adjusted.FootPoints[pointIndex],
						original.FootPoints[pointIndex] + legDelta), Is.LessThan(0.001f));
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRoundLegJointsAndKeepTubeRingsAtTheWireRadius()
		{
			const int radialSegments = 8;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddLegFixture(250f);
				var leg = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments, leg,
					out var profile), Is.True);

				foreach (var sharpCorner in new[] {
					profile.LegPoints[0], profile.LegPoints[^1],
				}) {
					Assert.That(profile.CombinedPath.Any(point => math.distance(
						point, sharpCorner) < 0.001f), Is.False,
						"each sharp leg joint should be replaced by a rounded bend");
				}
				var maximumTurn = 0f;
				for (var pointIndex = 1; pointIndex < profile.CombinedPath.Count - 1;
					pointIndex++) {
					var incoming = math.normalize(profile.CombinedPath[pointIndex]
						- profile.CombinedPath[pointIndex - 1]);
					var outgoing = math.normalize(profile.CombinedPath[pointIndex + 1]
						- profile.CombinedPath[pointIndex]);
					maximumTurn = math.max(maximumTurn, math.degrees(math.acos(
						math.clamp(math.dot(incoming, outgoing), -1f, 1f))));
				}
				Assert.That(maximumTurn, Is.LessThanOrEqualTo(15.1f));

				var vertices = new List<Vector3>();
				var normals = new List<Vector3>();
				var uvs = new List<Vector2>();
				var indices = new List<int>();
				WireRailFixtureMeshGenerator.AppendPolylineTube(profile.CombinedPath,
					profile.AttachmentProfile.Frame, leg.Diameter, 0f, radialSegments,
					vertices, normals, uvs, indices);
				var expectedRadius = leg.Diameter * 0.5f;
				for (var pointIndex = 0; pointIndex < profile.CombinedPath.Count;
					pointIndex++) {
					for (var radialIndex = 0; radialIndex < radialSegments; radialIndex++) {
						var vertex = (float3)vertices[pointIndex * radialSegments + radialIndex];
						Assert.That(math.distance(vertex, profile.CombinedPath[pointIndex]),
							Is.EqualTo(expectedRadius).Within(0.001f));
					}
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRejectAReversingPolylineTube()
		{
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var uvs = new List<Vector2>();
			var indices = new List<int>();
			var path = new[] {
				new float3(0f, 0f, 0f),
				new float3(10f, 0f, 0f),
				new float3(0f, 0f, 0f),
			};

			WireRailFixtureMeshGenerator.AppendPolylineTube(path,
				new WireRailPathFrame(float3.zero, new float3(0f, 1f, 0f),
					new float3(1f, 0f, 0f), new float3(0f, 0f, 1f)),
				6f, 0f, 8, vertices, normals, uvs, indices);

			Assert.That(vertices, Is.Empty);
			Assert.That(normals, Is.Empty);
			Assert.That(uvs, Is.Empty);
			Assert.That(indices, Is.Empty);
		}

		[TestCase(0f)]
		[TestCase(0.02f)]
		public void ShouldRejectLegsThatFoldBackThroughTheirAttachment(float vertical)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddLegFixture(250f);
				var leg = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				component.SetLegFixtureProperties(fixtureIndex, leg.Distance,
					WireRailLegSide.Right, new Vector3(-1f, 0f, vertical),
					leg.StartLength, leg.FootPosition, leg.FootRotation, leg.FootWidth,
					leg.FootLength, leg.FootConnectionLength, leg.LateralOffset,
					leg.VerticalOffset, leg.LengthAdjustment);

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments,
					(WireRailLegFixture)component.Fixtures[fixtureIndex], out _), Is.False);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldPoseTheFootInThreeDimensionsAndKeepTheLegConnected()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddLegFixture(250f);
				component.SetLegFixtureProperties(fixtureIndex, 250f,
					WireRailLegSide.Left, new Vector3(0f, 1f, 0f), 25f,
					new Vector3(10f, 20f, -50f), new Vector3(90f, 0f, 30f),
					40f, 25f, 12f);
				var leg = (WireRailLegFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments, leg,
					out var profile), Is.True);
				Assert.That(math.distance(profile.LegPoints[0],
					profile.AttachmentProfile.Start), Is.LessThan(0.001f));
				Assert.That(math.dot(math.normalize(profile.LegPoints[1]
					- profile.LegPoints[0]), profile.AttachmentProfile.Frame.Tangent),
					Is.EqualTo(1f).Within(0.001f));
				Assert.That(math.distance(profile.LegPoints[0], profile.LegPoints[1]),
					Is.EqualTo(25f).Within(0.001f));
				Assert.That(math.distance(profile.LegPoints[^1], profile.FootPoints[0]),
					Is.LessThan(0.001f));
				Assert.That(math.distance(profile.FootPoints[0], profile.FootPoints[1]),
					Is.EqualTo(12f).Within(0.001f));
				Assert.That(math.distance(profile.FootPoints[^2], profile.FootPoints[^1]),
					Is.EqualTo(25f).Within(0.001f));
				const float footRadius = 20f;
				const float footArcCenterY = -25f * 0.5f + footRadius * 0.5f;
				var expectedLocalFootStart = new float3(10f, 20f, -50f)
					+ math.mul(quaternion.EulerXYZ(math.radians(new float3(90f, 0f, 30f))),
						new float3(-footRadius, footArcCenterY + 12f, 0f));
				var expectedFootStart = profile.LegPoints[0]
					+ profile.AttachmentProfile.Frame.Right * expectedLocalFootStart.x
					+ profile.AttachmentProfile.Frame.Tangent * expectedLocalFootStart.y
					+ profile.AttachmentProfile.Frame.Up * expectedLocalFootStart.z;
				Assert.That(math.distance(profile.FootPoints[0], expectedFootStart),
					Is.LessThan(0.001f));
				var footNormal = math.normalize(math.cross(
					profile.FootPoints[1] - profile.FootPoints[0],
					profile.FootPoints[2] - profile.FootPoints[1]));
				Assert.That(math.abs(math.dot(footNormal,
					profile.AttachmentProfile.Frame.Up)), Is.LessThan(0.99f),
					"the authored foot rotation should tilt its plane out of the default orientation");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(27.9f, 61f, -269.2f)]
		[TestCase(19f, 56.9f, -269.2f)]
		[TestCase(27.9f, 56.9f, -82f)]
		public void ShouldKeepAStandVisibleAcrossPracticalFootAdjustments(float armLength,
			float connectedArmLength, float zRotation)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var railVertexCount = component.RenderMesh.vertexCount;
				var fixtureIndex = component.AddLegFixture(250f);
				var stand = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				component.SetLegFixtureProperties(fixtureIndex, stand.Distance,
					WireRailLegSide.Right, new Vector3(1f, 0f, 0f), 17.36f,
					new Vector3(69.8f, 10.5f, -4.81f),
					new Vector3(0f, 0f, zRotation), 18.66f,
					armLength, connectedArmLength, -5.44f, -6.03f, 0f);

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments, stand,
					out var profile), Is.True);
				Assert.That(profile.CombinedPath.Count, Is.GreaterThan(1));
				Assert.That(component.RenderMesh.vertexCount, Is.GreaterThan(railVertexCount));

				var vertices = new List<Vector3>();
				var normals = new List<Vector3>();
				var uvs = new List<Vector2>();
				var indices = new List<int>();
				WireRailFixtureMeshGenerator.AppendPolylineTube(profile.CombinedPath,
					profile.AttachmentProfile.Frame, stand.Diameter, 0f, 8,
					vertices, normals, uvs, indices, true);
				Assert.That(vertices, Is.Not.Empty);
				Assert.That(indices, Is.Not.Empty);
				var expectedRadius = stand.Diameter * 0.5f;
				for (var pointIndex = 0; pointIndex < profile.CombinedPath.Count;
					pointIndex++) {
					for (var radialIndex = 0; radialIndex < 8; radialIndex++) {
						Assert.That(math.distance((float3)vertices[pointIndex * 8 + radialIndex],
							profile.CombinedPath[pointIndex]),
							Is.EqualTo(expectedRadius).Within(0.001f));
					}
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldHideALegAndFootWhenEitherBottomRailIsInactive()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var fixtureIndex = component.AddLegFixture(250f);
				var leg = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments, leg, out _),
					Is.True);

				component.SetRailsActive(0, new[] { 0 }, false);

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					component.SplineContainer.Spline, component.Segments, leg, out _),
					Is.False);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldAttachADropLoopToTwoRailsAtTheSelectedEndpoint()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var fixtureIndex = component.AddDropLoopFixture();
				var dropLoop = (WireRailDropLoopFixture)component.Fixtures[fixtureIndex];

				Assert.That(dropLoop.Endpoint, Is.EqualTo(WireRailEndpoint.End));
				Assert.That(dropLoop.Distance, Is.EqualTo(component.SplineLength));
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropLoopProfile(
					component.SplineContainer.Spline, component.Segments, dropLoop,
					out var endProfile), Is.True);
				Assert.That(math.distance(endProfile.CenterlinePoints[0],
					endProfile.FirstLeadPoints[0]), Is.LessThan(0.0001f));
				Assert.That(math.distance(endProfile.CenterlinePoints[^1],
					endProfile.SecondLeadPoints[^1]), Is.LessThan(0.0001f));
				Assert.That(endProfile.CenterlinePoints.Max(point => point.y),
					Is.GreaterThan(component.SplineLength));

				component.SetDropLoopFixtureProperties(fixtureIndex, WireRailEndpoint.Start,
					0, 1, dropLoop.LoopDiameter, dropLoop.LeadLength,
					dropLoop.TangentLength, dropLoop.RingDensity, 0f, 0f, 0f);
				Assert.That(dropLoop.Distance, Is.Zero);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropLoopProfile(
					component.SplineContainer.Spline, component.Segments, dropLoop,
					out var startProfile), Is.True);
				Assert.That(startProfile.CenterlinePoints.Min(point => point.y), Is.LessThan(0f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(WireRailEndpoint.Start)]
		[TestCase(WireRailEndpoint.End)]
		public void ShouldUseFlatRailCapsWhereADropLoopAttaches(WireRailEndpoint endpoint)
		{
			const int radialSegments = 10;
			const float capBevelSize = 2f;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.SetWireCapBevelSize(capBevelSize);
				component.AddDropLoopFixture(endpoint);

				var spline = component.SplineContainer.Spline;
				var samples = WireRailRenderMeshGenerator.BuildSampleParameters(
					spline, component.Segments, 0, 0, 16);
				var attachedAtStart = endpoint == WireRailEndpoint.Start;
				var attachedRingIndex = attachedAtStart ? 0 : samples.Count - 1;
				var exposedRingIndex = attachedAtStart ? samples.Count - 1 : 0;
				var attachedCurveT = samples[attachedRingIndex];
				var exposedCurveT = samples[exposedRingIndex];
				var attachedStep = attachedAtStart
					? samples[1] - samples[0]
					: samples[^1] - samples[^2];
				var exposedStep = attachedAtStart
					? samples[^1] - samples[^2]
					: samples[1] - samples[0];
				Assert.That(WireRailSplineGeometry.TryEvaluateRailFrame(spline,
					component.Segments, 0, 0, attachedCurveT, attachedStep,
					out var attachedFrame), Is.True);
				Assert.That(WireRailSplineGeometry.TryEvaluateRailFrame(spline,
					component.Segments, 0, 0, exposedCurveT, exposedStep,
					out var exposedFrame), Is.True);

				var vertices = component.RenderMesh.vertices;
				var attachedCenter = RingCenter(vertices,
					attachedRingIndex * radialSegments, radialSegments);
				var exposedCenter = RingCenter(vertices,
					exposedRingIndex * radialSegments, radialSegments);
				Assert.That(math.distance(attachedCenter, attachedFrame.Position),
					Is.LessThan(0.001f), "the fitting joint must not inset the rail cap");
				Assert.That(math.distance(exposedCenter, exposedFrame.Position),
					Is.EqualTo(capBevelSize).Within(0.001f),
					"the opposite exposed rail end should retain its cap bevel");
			} finally {
				Object.DestroyImmediate(go);
			}

			static float3 RingCenter(IReadOnlyList<Vector3> vertices, int start,
				int count)
			{
				var center = float3.zero;
				for (var index = 0; index < count; index++) {
					center += (float3)vertices[start + index];
				}
				return center / count;
			}
		}

		[TestCase(WireRailEndpoint.Start)]
		[TestCase(WireRailEndpoint.End)]
		public void ShouldCapADropLoopAndRailEndsWhenItsOffsetDetachesIt(
			WireRailEndpoint endpoint)
		{
			const int radialSegments = 10;
			const float capBevelSize = 2f;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.SetWireCapBevelSize(capBevelSize);
				var originalColliderIndexCount = component.ColliderMesh.GetIndexCount(0);
				var fixtureIndex = component.AddDropLoopFixture(endpoint);
				var dropLoop = (WireRailDropLoopFixture)component.Fixtures[fixtureIndex];
				component.SetDropLoopFixtureProperties(fixtureIndex, endpoint,
					dropLoop.FirstRailIndex, dropLoop.SecondRailIndex, dropLoop.LoopDiameter,
					dropLoop.LeadLength, dropLoop.TangentLength, dropLoop.RingDensity,
					5f, 0f, dropLoop.Rotation);

				var spline = component.SplineContainer.Spline;
				var samples = WireRailRenderMeshGenerator.BuildSampleParameters(
					spline, component.Segments, 0, 0, 16);
				var attachedRingIndex = endpoint == WireRailEndpoint.Start
					? 0 : samples.Count - 1;
				var attachedCurveT = samples[attachedRingIndex];
				var attachedStep = endpoint == WireRailEndpoint.Start
					? samples[1] - samples[0]
					: samples[^1] - samples[^2];
				Assert.That(WireRailSplineGeometry.TryEvaluateRailFrame(spline,
					component.Segments, 0, 0, attachedCurveT, attachedStep,
					out var attachedFrame), Is.True);
				var attachedCenter = RingCenter(component.RenderMesh.vertices,
					attachedRingIndex * radialSegments, radialSegments);
				Assert.That(math.distance(attachedCenter, attachedFrame.Position),
					Is.EqualTo(capBevelSize).Within(0.001f),
					"a detached fitting must restore the rail's exposed cap bevel");

				var fixtureVertices = new List<Vector3>();
				var fixtureNormals = new List<Vector3>();
				var fixtureUvs = new List<Vector2>();
				var fixtureIndices = new List<int>();
				WireRailFixtureMeshGenerator.Append(spline, component.Segments,
					component.Fixtures, capBevelSize, radialSegments, fixtureVertices,
					fixtureNormals, fixtureUvs, fixtureIndices);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropLoopProfile(spline,
					component.Segments, dropLoop, out var profile), Is.True);
				Assert.That(fixtureVertices.Count,
					Is.EqualTo(profile.CenterlinePoints.Count * radialSegments
						+ radialSegments * 6 + 2),
					"both fitting mouths should receive a beveled cap");
				Assert.That(component.ColliderMesh.GetIndexCount(0),
					Is.EqualTo(originalColliderIndexCount + 204),
					"the detached collider should add two flat box end faces");
				Assert.That(component.ColliderMesh.GetIndexCount(1), Is.EqualTo(288));
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropLoopColliderProfile(
					spline, component.Segments, dropLoop, out var colliderProfile), Is.True);
				var colliderVertices = component.ColliderMesh.vertices;
				var ordinaryIndices = component.ColliderMesh.GetIndices(0);
				var firstOutward = -math.normalizesafe(colliderProfile.CenterlinePoints[1]
					- colliderProfile.CenterlinePoints[0]);
				var lastOutward = math.normalizesafe(colliderProfile.CenterlinePoints[^1]
					- colliderProfile.CenterlinePoints[^2]);
				AssertCapNormals(ordinaryIndices.Length - 12, firstOutward);
				AssertCapNormals(ordinaryIndices.Length - 6, lastOutward);

				void AssertCapNormals(int firstIndex, float3 expectedNormal)
				{
					for (var triangleIndex = firstIndex;
						triangleIndex < firstIndex + 6; triangleIndex += 3) {
						var a = (float3)colliderVertices[ordinaryIndices[triangleIndex]];
						var b = (float3)colliderVertices[ordinaryIndices[triangleIndex + 1]];
						var c = (float3)colliderVertices[ordinaryIndices[triangleIndex + 2]];
						var normal = math.normalizesafe(math.cross(b - a, c - a));
						Assert.That(math.dot(normal, expectedNormal), Is.GreaterThan(0.999f),
							"detached collider cap normals must point out of the box");
					}
				}
			} finally {
				Object.DestroyImmediate(go);
			}

			static float3 RingCenter(IReadOnlyList<Vector3> vertices, int start, int count)
			{
				var center = float3.zero;
				for (var index = 0; index < count; index++) {
					center += (float3)vertices[start + index];
				}
				return center / count;
			}
		}

		[Test]
		public void ShouldOmitADropLoopThatConflictsWithARailTrim()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var trimIndex = component.AddRailTrimFixture(WireRailEndpoint.Start);
				component.SetRailTrimFixtureProperties(trimIndex, WireRailEndpoint.Start,
					new[] { 30f, 0f });
				var renderVertexCount = component.RenderMesh.vertexCount;
				var colliderVertexCount = component.ColliderMesh.vertexCount;
				var dropLoopIndex = component.AddDropLoopFixture(WireRailEndpoint.Start);
				var dropLoop = (WireRailDropLoopFixture)component.Fixtures[dropLoopIndex];

				Assert.That(component.HasRailTrimConflict(dropLoop.Endpoint,
					dropLoop.FirstRailIndex, dropLoop.SecondRailIndex), Is.True);
				Assert.That(component.RenderMesh.vertexCount, Is.EqualTo(renderVertexCount),
					"a conflicting loop must not leave floating render geometry");
				Assert.That(component.ColliderMesh.vertexCount,
					Is.EqualTo(colliderVertexCount),
					"a conflicting loop must not leave a floating collider");
				Assert.That(component.ColliderMesh.subMeshCount, Is.EqualTo(1));

				component.SetRailTrimFixtureProperties(trimIndex, WireRailEndpoint.Start,
					new[] { 0f, 0f });
				Assert.That(component.HasRailTrimConflict(dropLoop.Endpoint,
					dropLoop.FirstRailIndex, dropLoop.SecondRailIndex), Is.False);
				Assert.That(component.RenderMesh.vertexCount, Is.GreaterThan(renderVertexCount));
				Assert.That(component.ColliderMesh.subMeshCount, Is.EqualTo(2));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(WireRailEndpoint.Start)]
		[TestCase(WireRailEndpoint.End)]
		public void ShouldTrimEachRailIndependentlyAtAnEndpoint(WireRailEndpoint endpoint)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.SetWireCapBevelSize(0f);
				var fixtureIndex = component.AddRailTrimFixture(endpoint);
				component.SetRailTrimFixtureProperties(fixtureIndex, endpoint,
					new[] { 50f, 100f });

				var vertices = component.RenderMesh.vertices;
				var firstRail = vertices.Where(vertex => vertex.x < 0f).ToArray();
				var secondRail = vertices.Where(vertex => vertex.x > 0f).ToArray();
				Assert.That(firstRail, Is.Not.Empty);
				Assert.That(secondRail, Is.Not.Empty);
				if (endpoint == WireRailEndpoint.Start) {
					Assert.That(firstRail.Min(vertex => vertex.y),
						Is.EqualTo(50f).Within(0.25f));
					Assert.That(secondRail.Min(vertex => vertex.y),
						Is.EqualTo(100f).Within(0.25f));
				} else {
					Assert.That(firstRail.Max(vertex => vertex.y),
						Is.EqualTo(450f).Within(0.25f));
					Assert.That(secondRail.Max(vertex => vertex.y),
						Is.EqualTo(400f).Within(0.25f));
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(WireRailEndpoint.Start, 75f)]
		[TestCase(WireRailEndpoint.End, 425f)]
		public void ShouldTrimTheColliderToTheRemainingRailChannel(
			WireRailEndpoint endpoint, float expectedBoundary)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddRailTrimFixture(endpoint);
				component.SetRailTrimFixtureProperties(fixtureIndex, endpoint,
					new[] { 75f, 75f, 75f, 75f });

				var collider = component.ColliderMesh;
				Assert.That(collider, Is.Not.Null, component.GenerationError);
				var actualBoundary = endpoint == WireRailEndpoint.Start
					? collider.bounds.min.y : collider.bounds.max.y;
				Assert.That(actualBoundary, Is.EqualTo(expectedBoundary).Within(0.25f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepTheLargestRailTrimRegardlessOfFixtureOrder()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.SetWireCapBevelSize(0f);
				var firstIndex = component.AddRailTrimFixture(WireRailEndpoint.Start);
				component.SetRailTrimFixtureProperties(firstIndex, WireRailEndpoint.Start,
					new[] { 80f, 0f });
				var secondIndex = component.AddRailTrimFixture(WireRailEndpoint.Start);
				component.SetRailTrimFixtureProperties(secondIndex, WireRailEndpoint.Start,
					new[] { 30f, 0f });
				component.MoveFixture(secondIndex, firstIndex);

				var firstRail = component.RenderMesh.vertices
					.Where(vertex => vertex.x < 0f).ToArray();
				Assert.That(firstRail.Min(vertex => vertex.y),
					Is.EqualTo(80f).Within(0.25f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldResizeRailTrimOffsetsWithTheComponentRailCount()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddRailTrimFixture(WireRailEndpoint.Start);
				component.SetRailTrimFixtureProperties(fixtureIndex, WireRailEndpoint.Start,
					new[] { 10f, 20f, 30f, 40f });
				component.SetRailCount(6);
				component.SynchronizeSegments();

				var railTrim = (WireRailTrimFixture)component.Fixtures[fixtureIndex];
				Assert.That(railTrim.RailOffsets,
					Is.EqualTo(new[] { 10f, 20f, 30f, 40f, 0f, 0f }));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldMirrorAStandWithoutLosingItsGeometry()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddLegFixture(250f);
				component.SetLegFixtureProperties(fixtureIndex, 250f,
					WireRailLegSide.Right, new Vector3(1f, 0f, 0f), 19.5f,
					new Vector3(42.59f, 9.1f, 0.4f), new Vector3(0f, 0f, 90f),
					17.6f, 19.2f, 0f, -7.9f, -6.7f);
				var spline = component.SplineContainer.Spline;
				var stand = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					spline, component.Segments, stand, out var original), Is.True);

				component.MirrorLegFixture(fixtureIndex);

				stand = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				Assert.That(stand.LegSide, Is.EqualTo(WireRailLegSide.Left));
				Assert.That(stand.FootClockwise, Is.True);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					spline, component.Segments, stand, out var mirrored), Is.True);
				Assert.That(mirrored.CombinedPath, Has.Count.EqualTo(original.CombinedPath.Count));
				var maximumDeviation = 0f;
				for (var pointIndex = 0; pointIndex < original.CombinedPath.Count; pointIndex++) {
					var expected = original.CombinedPath[pointIndex];
					expected.x = -expected.x;
					maximumDeviation = math.max(maximumDeviation,
						math.distance(mirrored.CombinedPath[pointIndex], expected));
				}
				Assert.That(maximumDeviation, Is.LessThan(0.05f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldReverseAStandFootToClockwise()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var fixtureIndex = component.AddLegFixture(250f);
				var spline = component.SplineContainer.Spline;
				var stand = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					spline, component.Segments, stand, out var counterClockwise), Is.True);

				component.SetLegFixtureProperties(fixtureIndex, stand.Distance,
					stand.LegSide, stand.StartDirection, stand.StartLength,
					stand.FootPosition, stand.FootRotation, stand.FootWidth,
					stand.FootLength, stand.FootConnectionLength, stand.LateralOffset,
					stand.VerticalOffset, stand.LengthAdjustment, true);

				stand = (WireRailLegFixture)component.Fixtures[fixtureIndex];
				Assert.That(stand.FootClockwise, Is.True);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateLegProfile(
					spline, component.Segments, stand, out var clockwise), Is.True);
				var pivot = clockwise.LegPoints[0]
					+ clockwise.AttachmentProfile.Frame.Right * stand.FootPosition.x
					+ clockwise.AttachmentProfile.Frame.Tangent * stand.FootPosition.y
					+ clockwise.AttachmentProfile.Frame.Up * stand.FootPosition.z;
				var counterClockwiseStart = math.dot(counterClockwise.FootPoints[0] - pivot,
					clockwise.AttachmentProfile.Frame.Right);
				var counterClockwiseEnd = math.dot(counterClockwise.FootPoints[^1] - pivot,
					clockwise.AttachmentProfile.Frame.Right);
				var clockwiseStart = math.dot(clockwise.FootPoints[0] - pivot,
					clockwise.AttachmentProfile.Frame.Right);
				var clockwiseEnd = math.dot(clockwise.FootPoints[^1] - pivot,
					clockwise.AttachmentProfile.Frame.Right);
				Assert.That(counterClockwiseStart, Is.LessThan(counterClockwiseEnd));
				Assert.That(clockwiseStart, Is.GreaterThan(clockwiseEnd));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(WireRailEndpoint.Start, 100f)]
		[TestCase(WireRailEndpoint.End, 400f)]
		public void ShouldCapARailTrimAtAnExactLayoutBoundary(
			WireRailEndpoint endpoint, float boundaryDistance)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(1);
				component.SetWireCapBevelSize(0f);
				component.AddLayout(boundaryDistance);
				var offset = endpoint == WireRailEndpoint.Start
					? boundaryDistance : component.SplineLength - boundaryDistance;
				var fixtureIndex = component.AddRailTrimFixture(endpoint);
				component.SetRailTrimFixtureProperties(fixtureIndex, endpoint,
					new[] { offset });
				Assert.That(WireRailSplineGeometry.TryEvaluateDistance(
					component.SplineContainer.Spline, boundaryDistance, out var frame), Is.True);
				var expectedNormal = endpoint == WireRailEndpoint.Start
					? -frame.Tangent : frame.Tangent;
				var vertices = component.RenderMesh.vertices;
				var normals = component.RenderMesh.normals;
				var capVertexCount = 0;
				for (var vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++) {
					var fromBoundary = (float3)vertices[vertexIndex] - frame.Position;
					if (math.abs(math.dot(fromBoundary, frame.Tangent)) < 0.01f
						&& math.dot((float3)normals[vertexIndex], expectedNormal) > 0.99f) {
						capVertexCount++;
					}
				}
				Assert.That(capVertexCount, Is.GreaterThan(0));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldFlareDropLoopLeadsToTheAuthoredLoopDiameter()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.SetRailOffset(0, 0, new Vector2(-15f, 0f));
				component.SetRailOffset(0, 1, new Vector2(15f, 0f));
				var fixtureIndex = component.AddDropLoopFixture();
				component.SetDropLoopFixtureProperties(fixtureIndex, WireRailEndpoint.End,
					0, 1, 100f, 45f, 18f, 32, 0f, 0f, 0f);
				var dropLoop = (WireRailDropLoopFixture)component.Fixtures[fixtureIndex];

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropLoopProfile(
					component.SplineContainer.Spline, component.Segments, dropLoop,
					out var profile), Is.True);
				Assert.That(math.distance(profile.TerminalPoints[0],
					profile.TerminalPoints[^1]), Is.EqualTo(100f).Within(0.001f));
				Assert.That(math.distance(profile.FirstLeadPoints[0],
					profile.TerminalPoints[0]), Is.GreaterThan(20f));
				Assert.That(profile.TerminalPoints.Count, Is.EqualTo(17));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldHideADropLoopWhenAnAttachedRailIsInactive()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var fixtureIndex = component.AddDropLoopFixture();
				var dropLoop = (WireRailDropLoopFixture)component.Fixtures[fixtureIndex];
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropLoopProfile(
					component.SplineContainer.Spline, component.Segments, dropLoop, out _),
					Is.True);

				component.SetRailsActive(0, new[] { 1 }, false);

				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropLoopProfile(
					component.SplineContainer.Spline, component.Segments, dropLoop, out _),
					Is.False);

				component.SetRailsActive(0, new[] { 1 }, true);
				component.SplineContainer.Spline.Closed = true;
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropLoopProfile(
					component.SplineContainer.Spline, component.Segments, dropLoop, out _),
					Is.False, "a closed spline has no endpoint for an end fitting");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldPutTheDropLoopTerminalArcInASeparateColliderSubmesh()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var originalChannelIndexCount = component.ColliderMesh.GetIndexCount(0);
				var originalVertexCount = component.ColliderMesh.vertexCount;
				var fixtureIndex = component.AddDropLoopFixture();
				var dropLoop = (WireRailDropLoopFixture)component.Fixtures[fixtureIndex];

				Assert.That(component.ColliderMesh.subMeshCount, Is.EqualTo(2));
				Assert.That(component.ColliderMesh.GetIndexCount(0),
					Is.EqualTo(originalChannelIndexCount + 192),
					"four box faces over eight approach spans should use the ordinary material");
				Assert.That(component.ColliderMesh.GetIndexCount(1), Is.EqualTo(288),
					"the terminal semicircle should use twelve coarse box spans");
				Assert.That(component.ColliderMesh.vertexCount,
					Is.EqualTo(originalVertexCount + 84),
					"the complete fitting should use 21 four-corner box rings");
				Assert.That(component.ColliderMesh.GetIndices(0)
					.Intersect(component.ColliderMesh.GetIndices(1)).Count(), Is.EqualTo(8),
					"both material sections should share the two four-corner seam rings");

				component.SetDropLoopFixtureProperties(fixtureIndex, dropLoop.Endpoint,
					dropLoop.FirstRailIndex, dropLoop.SecondRailIndex, dropLoop.LoopDiameter,
					dropLoop.LeadLength, dropLoop.TangentLength, 128,
					dropLoop.LateralOffset, dropLoop.VerticalOffset, dropLoop.Rotation);
				Assert.That(component.ColliderMesh.GetIndexCount(0),
					Is.EqualTo(originalChannelIndexCount + 192));
				Assert.That(component.ColliderMesh.GetIndexCount(1), Is.EqualTo(288),
					"render ring density must not increase collider density");
				Assert.That(component.ColliderMesh.vertexCount,
					Is.EqualTo(originalVertexCount + 84));

				component.RemoveFixture(0);
				Assert.That(component.ColliderMesh.subMeshCount, Is.EqualTo(1));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldUseTheTerminalImpactMaterialOnlyForDropLoopArcTriangles()
		{
			var go = new GameObject("Wire Rail");
			var terminalMaterial = ScriptableObject.CreateInstance<PhysicsMaterialAsset>();
			var transforms = new NativeParallelHashMap<int, float4x4>(0, Allocator.Temp);
			var colliders = new ColliderReference(ref transforms, Allocator.Temp);
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.AddDropLoopFixture();
				terminalMaterial.Elasticity = 0.91f;
				terminalMaterial.ElasticityFalloff = 0.13f;
				terminalMaterial.Friction = 0.27f;
				terminalMaterial.ScatterAngle = 0.42f;
				Assert.That(component.PhysicsOverwrite, Is.True,
					"the terminal material is intentionally independent of the main overwrite mode");
				component.TerminalPhysicsMaterialReference = terminalMaterial;
				var expectedTerminalTriangleCount =
					(int)component.ColliderMesh.GetIndexCount(1) / 3;

				((ICollidableComponent)component).GetColliders(null, null, ref colliders,
					float4x4.identity, 0f);

				var triangleColliders = colliders.ToArray().OfType<TriangleCollider>().ToArray();
				var terminalColliderCount = triangleColliders.Count(collider =>
					math.abs(collider.Header.Material.Elasticity - 0.91f) < 0.0001f);
				Assert.That(terminalColliderCount, Is.EqualTo(expectedTerminalTriangleCount));
				Assert.That(triangleColliders.Any(collider =>
					math.abs(collider.Header.Material.Elasticity - 0.3f) < 0.0001f), Is.True,
					"the channel and approach leads should retain the ordinary material");
			} finally {
				colliders.Dispose();
				transforms.Dispose();
				Object.DestroyImmediate(terminalMaterial);
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDuplicateEveryDropLoopSettingAndSynchronizeItsDiameter()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var sourceIndex = component.AddDropLoopFixture(WireRailEndpoint.Start);
				component.SetDropLoopFixtureProperties(sourceIndex, WireRailEndpoint.Start,
					2, 3, 92f, 37f, 11f, 40, 6f, -9f, 27f);
				var duplicateIndex = component.DuplicateDropLoopFixture(sourceIndex);
				component.SetWireDiameter(12f);
				var source = (WireRailDropLoopFixture)component.Fixtures[sourceIndex];
				var duplicate = (WireRailDropLoopFixture)component.Fixtures[duplicateIndex];

				Assert.That(duplicateIndex, Is.EqualTo(sourceIndex + 1));
				Assert.That(duplicate, Is.Not.SameAs(source));
				Assert.That(duplicate.Endpoint, Is.EqualTo(source.Endpoint));
				Assert.That(duplicate.Distance, Is.Zero);
				Assert.That(duplicate.FirstRailIndex, Is.EqualTo(source.FirstRailIndex));
				Assert.That(duplicate.SecondRailIndex, Is.EqualTo(source.SecondRailIndex));
				Assert.That(duplicate.LoopDiameter, Is.EqualTo(source.LoopDiameter));
				Assert.That(duplicate.LeadLength, Is.EqualTo(source.LeadLength));
				Assert.That(duplicate.TangentLength, Is.EqualTo(source.TangentLength));
				Assert.That(duplicate.RingDensity, Is.EqualTo(source.RingDensity));
				Assert.That(duplicate.LateralOffset, Is.EqualTo(source.LateralOffset));
				Assert.That(duplicate.VerticalOffset, Is.EqualTo(source.VerticalOffset));
				Assert.That(duplicate.Rotation, Is.EqualTo(source.Rotation));
				Assert.That(source.Diameter, Is.EqualTo(12f));
				Assert.That(duplicate.Diameter, Is.EqualTo(12f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(WireRailEndpoint.Start, -1f)]
		[TestCase(WireRailEndpoint.End, 1f)]
		public void ShouldBuildTwoParallelRoundedDropRails(
			WireRailEndpoint endpoint, float endpointDirection)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var fixtureIndex = component.AddDropFixture(endpoint);
				var drop = (WireRailDropFixture)component.Fixtures[fixtureIndex];

				// Zero offset drops straight at the endpoint: no straight run, the rounded
				// bend begins at the attachment and ends the drop length below it.
				component.SetDropFixtureProperties(fixtureIndex, endpoint, 0, 1,
					0f, 90f, 30f, new[] { 0f, 0f });
				Assert.That(drop.Offset, Is.Zero);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropProfile(
					component.SplineContainer.Spline, component.Segments, drop,
					out var zeroProfile), Is.True);
				var outward = math.normalizesafe(math.mul(quaternion.AxisAngle(
					zeroProfile.Frame.Up, math.radians(30f)),
					zeroProfile.Frame.Tangent * endpointDirection));
				var attachAtEndpoint = zeroProfile.FirstRailPoints[0];
				Assert.That(math.distance(zeroProfile.FirstRailPoints[^1],
					attachAtEndpoint + outward * drop.Diameter
						- zeroProfile.Frame.Up * 90f),
					Is.LessThan(0.001f),
					"the drop rounds outward by the bend radius, then drops straight down");

				// A positive offset shortens the rails: the attachment slides inward along the
				// spline by the offset and the same rounded drop follows it, still no straight run.
				component.SetDropFixtureProperties(fixtureIndex, endpoint, 0, 1,
					45f, 90f, 30f, new[] { 0f, 0f });
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropProfile(
					component.SplineContainer.Spline, component.Segments, drop,
					out var profile), Is.True);
				Assert.That(profile.FirstRailPoints.Count, Is.GreaterThan(3));
				Assert.That(profile.SecondRailPoints.Count,
					Is.EqualTo(profile.FirstRailPoints.Count));
				var inward = math.normalizesafe(
					profile.Frame.Tangent * -endpointDirection);
				var attachmentShift = profile.FirstRailPoints[0] - attachAtEndpoint;
				Assert.That(math.length(attachmentShift), Is.EqualTo(45f).Within(1f),
					"a positive offset moves the drop inward from the endpoint by the offset");
				Assert.That(math.dot(math.normalizesafe(attachmentShift), inward),
					Is.GreaterThan(0.99f),
					"the drop must move inward along the rails, not sideways");
				Assert.That(math.distance(profile.FirstRailPoints[^1],
					profile.FirstRailPoints[0] + outward * drop.Diameter
						- profile.Frame.Up * 90f),
					Is.LessThan(0.01f),
					"the rounded drop still starts at the (shifted) attachment, no straight run");
				for (var pointIndex = 0; pointIndex < profile.FirstRailPoints.Count;
					pointIndex++) {
					Assert.That(math.distance(
						profile.SecondRailPoints[pointIndex]
							- profile.FirstRailPoints[pointIndex],
						profile.SecondRailPoints[0] - profile.FirstRailPoints[0]),
						Is.LessThan(0.001f));
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldChooseTheActiveRailPairForAStartDrop()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(6);
				component.SetWireCapBevelSize(2f);
				component.AddLayout(component.SplineLength * 0.5f);
				var endpointLayoutIndex = component.AddLayout(0f);
				component.SetRailsActive(endpointLayoutIndex,
					new[] { 0, 1, 2, 3 }, false);

				var fixtureIndex = component.AddDropFixture(WireRailEndpoint.Start);
				var drop = (WireRailDropFixture)component.Fixtures[fixtureIndex];

				Assert.That(drop.FirstRailIndex, Is.EqualTo(4));
				Assert.That(drop.SecondRailIndex, Is.EqualTo(5));
				Assert.That(component.AreEndpointRailsActive(WireRailEndpoint.Start,
					drop.FirstRailIndex, drop.SecondRailIndex), Is.True);
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropProfile(
					component.SplineContainer.Spline, component.Segments, drop,
					out var profile),
					Is.True);
				var attachedRingCenter = float3.zero;
				var renderVertices = component.RenderMesh.vertices;
				const int radialSegments = 10;
				for (var vertexIndex = 0; vertexIndex < radialSegments; vertexIndex++) {
					attachedRingCenter += (float3)renderVertices[vertexIndex];
				}
				attachedRingCenter /= radialSegments;
				Assert.That(math.distance(attachedRingCenter,
					profile.FirstRailPoints[0]), Is.LessThan(0.001f),
					"a duplicate-distance start layout must not leave a cap bevel inside the fitting");
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDeferLegacyDropPairMigrationUntilTwoRailsAreActive()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(6);
				component.SetRailsActive(0, new[] { 0, 1, 2, 3, 5 }, false);
				var drop = new WireRailDropFixture();
				JsonUtility.FromJsonOverwrite(
					"{\"_endpoint\":0,\"_firstRailIndex\":0,\"_secondRailIndex\":1,"
					+ "\"_railPairInitialized\":false}", drop);
				var fixturesField = typeof(WireRailComponent).GetField("_fixtures",
					BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.That(fixturesField, Is.Not.Null);
				fixturesField.SetValue(component, new List<WireRailFixture> { drop });

				component.SynchronizeSegments();

				Assert.That(drop.RailPairInitialized, Is.False);
				Assert.That(drop.FirstRailIndex, Is.EqualTo(0));
				Assert.That(drop.SecondRailIndex, Is.EqualTo(1));

				component.SetRailsActive(0, new[] { 5 }, true);
				component.SynchronizeSegments();

				Assert.That(drop.RailPairInitialized, Is.True);
				Assert.That(drop.FirstRailIndex, Is.EqualTo(4));
				Assert.That(drop.SecondRailIndex, Is.EqualTo(5));
				Assert.That(drop.EnsureRailPairInitialized(0, 1), Is.False);
				Assert.That(drop.FirstRailIndex, Is.EqualTo(4));
				Assert.That(drop.SecondRailIndex, Is.EqualTo(5));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldChooseANewActiveRailPairWhenTheDropEndpointChanges()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(6);
				var fixtureIndex = component.AddDropFixture(WireRailEndpoint.End);
				var drop = (WireRailDropFixture)component.Fixtures[fixtureIndex];
				Assert.That(drop.FirstRailIndex, Is.EqualTo(0));
				Assert.That(drop.SecondRailIndex, Is.EqualTo(1));
				component.SetRailsActive(0, new[] { 0, 1, 2, 3 }, false);

				component.SetDropFixtureProperties(fixtureIndex, WireRailEndpoint.Start,
					drop.FirstRailIndex, drop.SecondRailIndex,
					drop.Offset, drop.DropLength, drop.ZAngle,
					drop.RailOffsets);
				drop = (WireRailDropFixture)component.Fixtures[fixtureIndex];

				Assert.That(drop.FirstRailIndex, Is.EqualTo(4));
				Assert.That(drop.SecondRailIndex, Is.EqualTo(5));
				Assert.That(WireRailFixtureMeshGenerator.TryEvaluateDropProfile(
					component.SplineContainer.Spline, component.Segments, drop, out _),
					Is.True);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldApplyDropCutoffsOnlyToTheOtherRails()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(4);
				var fixtureIndex = component.AddDropFixture(WireRailEndpoint.End);
				component.SetDropFixtureProperties(fixtureIndex, WireRailEndpoint.End,
					1, 3, 40f, 80f, 0f, new[] { 25f, 50f, 75f, 100f });
				var drop = (WireRailDropFixture)component.Fixtures[fixtureIndex];
				var startOffsets = new float[WireRailEndpointTrimUtility.MaximumRailCount];
				var endOffsets = new float[WireRailEndpointTrimUtility.MaximumRailCount];

				WireRailEndpointTrimUtility.Collect(component.SplineContainer.Spline,
					component.Segments, component.Fixtures, startOffsets, endOffsets);

				// The per-rail cutoffs still apply only to the other rails (the attached pair
				// is forced to zero), but the offset trims the two attached rails by 40.
				Assert.That(drop.RailOffsets, Is.EqualTo(new[] { 25f, 0f, 75f, 0f }));
				Assert.That(startOffsets, Is.All.Zero);
				Assert.That(endOffsets.Take(4), Is.EqualTo(new[] { 25f, 40f, 75f, 40f }));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(WireRailEndpoint.Start)]
		[TestCase(WireRailEndpoint.End)]
		public void ShouldExtendTheTwoBottomFacesDownForADrop(
			WireRailEndpoint endpoint)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var baselineVertices = component.ColliderMesh.vertices;
				var baselineIndices = component.ColliderMesh.GetIndices(0);

				const float dropLength = 30f;
				var fixtureIndex = component.AddDropFixture(endpoint);
				component.SetDropFixtureProperties(fixtureIndex, endpoint, 0, 1,
					0f, dropLength, 0f, new[] { 0f, 0f });
				AssertDropExtendsTwoBottomFaces(component, endpoint, dropLength,
					baselineVertices, baselineIndices);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		// A Drop leaves the whole normal channel untouched and appends exactly two vertical
		// faces: the two floor faces the ball rests on, each extended straight down (along
		// -Up) by the drop length, top-aligned with the channel floor.
		private static void AssertDropExtendsTwoBottomFaces(WireRailComponent component,
			WireRailEndpoint endpoint, float dropLength, Vector3[] baselineVertices,
			int[] baselineIndices)
		{
			var collider = component.ColliderMesh;
			var vertices = collider.vertices;
			var indices = collider.GetIndices(0);

			// the channel prefix must be byte-for-byte the no-fixture collider
			Assert.That(vertices.Length, Is.EqualTo(baselineVertices.Length + 8),
				"a Drop must append exactly two four-vertex faces");
			Assert.That(indices.Length, Is.EqualTo(baselineIndices.Length + 24),
				"a Drop must append exactly two two-sided quads");
			for (var i = 0; i < baselineVertices.Length; i++) {
				Assert.That(math.distance((float3)vertices[i],
					(float3)baselineVertices[i]), Is.LessThan(0.0001f),
					"a Drop must not change the existing channel collider");
			}
			for (var i = 0; i < baselineIndices.Length; i++) {
				Assert.That(indices[i], Is.EqualTo(baselineIndices[i]));
			}

			var spline = component.SplineContainer.Spline;
			var endpointDistance = endpoint == WireRailEndpoint.Start
				? 0f : component.SplineLength;
			Assert.That(WireRailSplineGeometry.TryEvaluateDistance(spline, endpointDistance,
				out var frame), Is.True);
			var down = math.normalizesafe(-frame.Up);

			var faceTops = new System.Collections.Generic.List<float3>();
			for (var face = 0; face < 2; face++) {
				var b = baselineVertices.Length + face * 4;
				var top0 = (float3)vertices[b];
				var bottom0 = (float3)vertices[b + 1];
				var top1 = (float3)vertices[b + 2];
				var bottom1 = (float3)vertices[b + 3];
				AssertVerticalDrop(top0, bottom0);
				AssertVerticalDrop(top1, bottom1);
				AssertTopOnChannel(top0);
				AssertTopOnChannel(top1);
				faceTops.Add(top0);
				faceTops.Add(top1);
			}

			// the two faces form a V: they share exactly one top vertex, the resting point
			var shared = 0;
			for (var a = 0; a < 2; a++) {
				for (var c = 2; c < 4; c++) {
					if (math.distance(faceTops[a], faceTops[c]) < 0.001f) {
						shared++;
					}
				}
			}
			Assert.That(shared, Is.EqualTo(1),
				"the two drop faces must meet at the single lowest channel point");

			void AssertVerticalDrop(float3 top, float3 bottom)
			{
				var extrusion = bottom - top;
				Assert.That(math.length(extrusion), Is.EqualTo(dropLength).Within(0.001f),
					"each drop face must be exactly the drop length tall");
				Assert.That(math.dot(math.normalizesafe(extrusion), down),
					Is.GreaterThan(0.9999f), "each drop face must hang straight down");
			}

			void AssertTopOnChannel(float3 top)
				=> Assert.That(baselineVertices.Any(v =>
					math.distance((float3)v, top) < 0.001f), Is.True,
					"each drop face must be top-aligned with the channel floor");
		}

		[TestCase(WireRailEndpoint.Start)]
		[TestCase(WireRailEndpoint.End)]
		public void ShouldExtendOnlyTheTwoBottomFacesForASixRailDrop(
			WireRailEndpoint endpoint)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(6);
				var baselineVertices = component.ColliderMesh.vertices;
				var baselineIndices = component.ColliderMesh.GetIndices(0);

				// A six-rail channel has several up-facing floor faces, but only the two the
				// ball rests on may be extended, and the channel itself must not be trimmed
				// (these railOffsets would trim rails 2 and 3 if the Drop affected the channel).
				const float dropLength = 30f;
				var railOffsets = new[] { 0f, 0f, 50f, 50f, 0f, 0f };
				var fixtureIndex = component.AddDropFixture(endpoint);
				component.SetDropFixtureProperties(fixtureIndex, endpoint, 0, 1,
					0f, dropLength, 0f, railOffsets);
				AssertDropExtendsTwoBottomFaces(component, endpoint, dropLength,
					baselineVertices, baselineIndices);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(WireRailEndpoint.Start)]
		[TestCase(WireRailEndpoint.End)]
		public void ShouldMoveTheDropInwardWithAPositiveOffset(WireRailEndpoint endpoint)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var fixtureIndex = component.AddDropFixture(endpoint);

				component.SetDropFixtureProperties(fixtureIndex, endpoint, 0, 1,
					0f, 30f, 0f, new[] { 0f, 0f });
				var tops0 = DropFaceTops(component);
				var channelExtent0 = ChannelExtentAlongDrop(component, endpoint);

				const float offset = 100f;
				component.SetDropFixtureProperties(fixtureIndex, endpoint, 0, 1,
					offset, 30f, 0f, new[] { 0f, 0f });
				var topsOffset = DropFaceTops(component);
				var channelExtentOffset = ChannelExtentAlongDrop(component, endpoint);

				Assert.That(topsOffset.Length, Is.EqualTo(tops0.Length),
					"the offset must keep exactly the two drop faces");
				var spline = component.SplineContainer.Spline;
				var endpointDistance = endpoint == WireRailEndpoint.Start
					? 0f : component.SplineLength;
				Assert.That(WireRailSplineGeometry.TryEvaluateDistance(spline,
					endpointDistance, out var frame), Is.True);
				var inward = math.normalizesafe((endpoint == WireRailEndpoint.Start
					? 1f : -1f) * frame.Tangent);
				for (var i = 0; i < tops0.Length; i++) {
					var shift = topsOffset[i] - tops0[i];
					Assert.That(math.length(shift), Is.EqualTo(offset).Within(1f),
						"the drop faces must move inward by the offset");
					Assert.That(math.dot(math.normalizesafe(shift), inward),
						Is.GreaterThan(0.99f),
						"the drop faces must move inward along the rails, not sideways");
				}
				Assert.That(channelExtent0 - channelExtentOffset,
					Is.EqualTo(offset).Within(1f),
					"the channel must be scaled to end at the shifted drop point");
			} finally {
				Object.DestroyImmediate(go);
			}

			static float3[] DropFaceTops(WireRailComponent component)
			{
				var v = component.ColliderMesh.vertices;
				return new[] {
					(float3)v[v.Length - 8], (float3)v[v.Length - 6],
					(float3)v[v.Length - 4], (float3)v[v.Length - 2],
				};
			}

			// How far the channel (everything but the eight drop-face vertices) reaches toward
			// the drop endpoint, so a positive offset must reduce it by that offset.
			static float ChannelExtentAlongDrop(WireRailComponent component,
				WireRailEndpoint endpoint)
			{
				var spline = component.SplineContainer.Spline;
				var endpointDistance = endpoint == WireRailEndpoint.Start
					? 0f : component.SplineLength;
				WireRailSplineGeometry.TryEvaluateDistance(spline, endpointDistance,
					out var frame);
				var toward = (endpoint == WireRailEndpoint.Start ? -1f : 1f)
					* frame.Tangent;
				var v = component.ColliderMesh.vertices;
				var extent = float.NegativeInfinity;
				for (var i = 0; i < v.Length - 8; i++) {
					extent = math.max(extent, math.dot((float3)v[i], toward));
				}
				return extent;
			}
		}

		[Test]
		public void ShouldOmitADropWhenAnAttachedRailIsTrimmed()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var trimIndex = component.AddRailTrimFixture(WireRailEndpoint.End);
				component.SetRailTrimFixtureProperties(trimIndex, WireRailEndpoint.End,
					new[] { 30f, 0f });
				var renderVertexCount = component.RenderMesh.vertexCount;
				var colliderVertexCount = component.ColliderMesh.vertexCount;
				var fixtureIndex = component.AddDropFixture(WireRailEndpoint.End);
				var drop = (WireRailDropFixture)component.Fixtures[fixtureIndex];

				Assert.That(component.HasRailTrimConflict(drop.Endpoint,
					drop.FirstRailIndex, drop.SecondRailIndex), Is.True);
				Assert.That(component.RenderMesh.vertexCount, Is.EqualTo(renderVertexCount));
				Assert.That(component.ColliderMesh.vertexCount,
					Is.EqualTo(colliderVertexCount));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDuplicateEveryDropSettingAndResizeItsCutoffs()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(4);
				var sourceIndex = component.AddDropFixture(WireRailEndpoint.Start);
				component.SetDropFixtureProperties(sourceIndex, WireRailEndpoint.Start,
					1, 2, 55f, 95f, -32f, new[] { 20f, 0f, 0f, 65f });
				var duplicateIndex = component.DuplicateDropFixture(sourceIndex);
				component.SetWireDiameter(12f);
				component.SetRailCount(6);
				component.SynchronizeSegments();
				var source = (WireRailDropFixture)component.Fixtures[sourceIndex];
				var duplicate = (WireRailDropFixture)component.Fixtures[duplicateIndex];

				Assert.That(duplicateIndex, Is.EqualTo(sourceIndex + 1));
				Assert.That(duplicate.Endpoint, Is.EqualTo(source.Endpoint));
				Assert.That(duplicate.FirstRailIndex, Is.EqualTo(source.FirstRailIndex));
				Assert.That(duplicate.SecondRailIndex, Is.EqualTo(source.SecondRailIndex));
				Assert.That(duplicate.Offset, Is.EqualTo(55f));
				Assert.That(duplicate.DropLength, Is.EqualTo(95f));
				Assert.That(duplicate.ZAngle, Is.EqualTo(-32f));
				Assert.That(duplicate.RailOffsets,
					Is.EqualTo(new[] { 20f, 0f, 0f, 65f, 0f, 0f }));
				Assert.That(source.Diameter, Is.EqualTo(12f));
				Assert.That(duplicate.Diameter, Is.EqualTo(12f));
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
			const int radialSegments = 10;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var railTriangleCount = component.RenderMesh.triangles.Length / 3;
				var fixtureIndex = component.AddBraceFixture(250f);
				var brace = (WireRailBraceFixture)component.Fixtures[fixtureIndex];
				var touches = new List<WireRailTouch>();
				WireRailSolderMeshGenerator.CollectTouches(component.SplineContainer.Spline,
					component.Segments, component.Fixtures, brace, touches);
				var fullBraceTriangleCount = component.RenderMesh.triangles.Length / 3
					- railTriangleCount;
				Assert.That(fullBraceTriangleCount,
					Is.EqualTo(32 * radialSegments * 2 + touches.Count
						* WireRailSolderMeshGenerator.TrianglesPerBlob));

				component.SetBraceFixtureProperties(fixtureIndex, 250f,
					true, 0f, 90f);
				touches.Clear();
				WireRailSolderMeshGenerator.CollectTouches(component.SplineContainer.Spline,
					component.Segments, component.Fixtures, brace, touches);
				var cutoutBraceTriangleCount = component.RenderMesh.triangles.Length / 3
					- railTriangleCount;
				Assert.That(cutoutBraceTriangleCount,
					Is.EqualTo(24 * radialSegments * 2 + radialSegments * 6
						+ touches.Count * WireRailSolderMeshGenerator.TrianglesPerBlob),
					"a 90-degree cutout should leave a capped three-quarter brace");

			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldUseTheAuthoredBraceRingDensity()
		{
			const int radialSegments = 10;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var railTriangleCount = component.RenderMesh.triangles.Length / 3;
				var fixtureIndex = component.AddBraceFixture(250f);

				component.SetBraceFixtureProperties(fixtureIndex, 250f,
					false, 0f, 0f, ringDensity: 12);

				var brace = (WireRailBraceFixture)component.Fixtures[fixtureIndex];
				var touches = new List<WireRailTouch>();
				WireRailSolderMeshGenerator.CollectTouches(component.SplineContainer.Spline,
					component.Segments, component.Fixtures, brace, touches);
				Assert.That(brace.RingDensity, Is.EqualTo(12));
				Assert.That(component.RenderMesh.triangles.Length / 3 - railTriangleCount,
					Is.EqualTo(12 * radialSegments * 2 + touches.Count
						* WireRailSolderMeshGenerator.TrianglesPerBlob));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldBevelEveryExposedRailAndFixtureCap()
		{
			const int radialSegments = 10;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetWireCapBevelSize(0f);
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
					true, 35f, 145f, true, 215f, 325f, 7f, -11f, 1.35f, 48);
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
					Assert.That(brace.RingDensity, Is.EqualTo(48));
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
					true, 35f, 125f, true, 205f, 315f, 6f, -9f, 1.4f, 24);
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
				Assert.That(duplicate.RingDensity, Is.EqualTo(source.RingDensity));
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
		public void ShouldDuplicateEveryLegAndFootSetting()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var sourceIndex = component.AddLegFixture(175f);
				component.SetLegFixtureProperties(sourceIndex, 175f,
					WireRailLegSide.Left, new Vector3(1f, 2f, -3f), 42f,
					new Vector3(4f, 5f, -60f), new Vector3(15f, 25f, 35f),
					38f, 27f, 19f, 6f, -9f, 14f);
				var duplicateIndex = component.DuplicateLegFixture(sourceIndex);
				var source = (WireRailLegFixture)component.Fixtures[sourceIndex];
				var duplicate = (WireRailLegFixture)component.Fixtures[duplicateIndex];

				Assert.That(duplicateIndex, Is.EqualTo(sourceIndex + 1));
				Assert.That(duplicate, Is.Not.SameAs(source));
				Assert.That(duplicate.Distance, Is.EqualTo(source.Distance));
				Assert.That(duplicate.Diameter, Is.EqualTo(source.Diameter));
				Assert.That(duplicate.LegSide, Is.EqualTo(source.LegSide));
				Assert.That(duplicate.LateralOffset, Is.EqualTo(source.LateralOffset));
				Assert.That(duplicate.VerticalOffset, Is.EqualTo(source.VerticalOffset));
				Assert.That(duplicate.LengthAdjustment,
					Is.EqualTo(source.LengthAdjustment));
				Assert.That(Vector3.Distance(duplicate.StartDirection, source.StartDirection),
					Is.LessThan(0.0001f));
				Assert.That(duplicate.StartLength, Is.EqualTo(source.StartLength));
				Assert.That(duplicate.FootPosition, Is.EqualTo(source.FootPosition));
				Assert.That(duplicate.FootRotation, Is.EqualTo(source.FootRotation));
				Assert.That(duplicate.FootWidth, Is.EqualTo(source.FootWidth));
				Assert.That(duplicate.FootLength, Is.EqualTo(source.FootLength));
				Assert.That(duplicate.FootConnectionLength,
					Is.EqualTo(source.FootConnectionLength));
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
				component.SetRailCount(6);

				component.SetRailsActive(0, new[] { 0, 1, 2, 3 }, false);

				Assert.That(component.RailCount, Is.EqualTo(6));
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
		public void ShouldReorderLayoutNamesWithoutChangingPhysicalGeometry()
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
					Is.EqualTo(new[] { 1, 2, 3 }));
				Assert.That(component.LayoutDisplayOrder, Is.EqualTo(new[] { 2, 0, 1 }));
				Assert.That(component.Segments[0].ConnectionToNext.WireCount, Is.EqualTo(3));
				Assert.That(component.Segments[1].ConnectionToNext.WireCount, Is.EqualTo(3));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldSuggestLayoutPositionsFromPhysicalNeighbors()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				Assert.That(component.GetSuggestedLayoutDistance(),
					Is.EqualTo(component.SplineLength * 0.5f).Within(0.001f));

				component.AddLayout(200f);
				component.AddLayout(400f);
				component.MoveLayout(2, 0);

				Assert.That(component.GetSuggestedLayoutDistance(),
					Is.EqualTo(300f).Within(0.001f));
				Assert.That(component.GetSuggestedLayoutDistance(0),
					Is.EqualTo(100f).Within(0.001f));
				Assert.That(component.GetSuggestedLayoutDistance(1),
					Is.EqualTo(300f).Within(0.001f));
				Assert.That(component.GetSuggestedLayoutDistance(2),
					Is.EqualTo(300f).Within(0.001f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldAddAnUnselectedLayoutLastInDisplayOrder()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(200f);
				component.AddLayout(400f);

				var layoutIndex = component.AddLayout(component.GetSuggestedLayoutDistance());

				Assert.That(layoutIndex, Is.EqualTo(2));
				Assert.That(component.Segments.Select(layout => layout.Distance),
					Is.EqualTo(new[] { 0f, 200f, 300f, 400f }));
				Assert.That(component.LayoutDisplayOrder, Is.EqualTo(new[] { 0, 1, 3, 2 }));
				Assert.That(component.GetLayoutDisplayIndex(layoutIndex), Is.EqualTo(3));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDuplicateASelectedLayoutBetweenPhysicalNeighbors()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(200f);
				component.AddLayout(400f);
				component.SetRailCount(3);
				component.SetRailOffset(1, 0, new Vector2(42f, 17f));
				component.SetRailsActive(1, new[] { 2 }, false);
				component.SetWireTransitionOverride(1, 0, true);
				component.SetWireContinuous(1, 0, false);

				var layoutIndex = component.DuplicateLayout(1,
					component.GetSuggestedLayoutDistance(1));

				Assert.That(layoutIndex, Is.EqualTo(2));
				Assert.That(component.Segments.Select(layout => layout.Distance),
					Is.EqualTo(new[] { 0f, 200f, 300f, 400f }));
				Assert.That(component.Segments[layoutIndex].GetRailOffset(0),
					Is.EqualTo(new Vector2(42f, 17f)));
				Assert.That(component.Segments[layoutIndex].IsRailActive(2), Is.False);
				Assert.That(component.Segments[layoutIndex].ConnectionToNext
					.IsWireOverridden(0), Is.True);
				Assert.That(component.Segments[layoutIndex].ConnectionToNext
					.IsWireContinuous(0), Is.False);
				Assert.That(component.Segments[1].ConnectionToNext
					.IsWireOverridden(0), Is.False);
				Assert.That(component.LayoutDisplayOrder, Is.EqualTo(new[] { 0, 1, 2, 3 }));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldDuplicateTheLastPhysicalLayoutAfterItsDisplayEntry()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(200f);
				component.AddLayout(400f);
				component.SetRailOffset(2, 0, new Vector2(-25f, 35f));
				component.MoveLayout(2, 0);

				var layoutIndex = component.DuplicateLayout(2,
					component.GetSuggestedLayoutDistance(2));

				Assert.That(layoutIndex, Is.EqualTo(2));
				Assert.That(component.Segments.Select(layout => layout.Distance),
					Is.EqualTo(new[] { 0f, 200f, 300f, 400f }));
				Assert.That(component.Segments[layoutIndex].GetRailOffset(0),
					Is.EqualTo(new Vector2(-25f, 35f)));
				Assert.That(component.LayoutDisplayOrder, Is.EqualTo(new[] { 3, 2, 0, 1 }));
				Assert.That(component.GetLayoutDisplayIndex(layoutIndex), Is.EqualTo(1));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldPreserveDisplayOrderWhenRemovingAPhysicalLayout()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(200f);
				component.AddLayout(400f);
				component.MoveLayout(2, 0);

				component.RemoveLayout(1);

				Assert.That(component.Segments.Select(layout => layout.Distance),
					Is.EqualTo(new[] { 0f, 400f }));
				Assert.That(component.LayoutDisplayOrder, Is.EqualTo(new[] { 1, 0 }));
				Assert.That(component.GetLayoutDisplayIndex(1), Is.Zero);
				Assert.That(component.GetLayoutDisplayIndex(0), Is.EqualTo(1));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRepairLegacyAndInvalidLayoutDisplayOrder()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(200f);
				component.AddLayout(400f);
				var flags = BindingFlags.Instance | BindingFlags.NonPublic;
				var displayOrderField = typeof(WireRailComponent)
					.GetField("_layoutDisplayOrder", flags);

				displayOrderField?.SetValue(component, new List<int>());
				Assert.That(component.SynchronizeSegments(), Is.True);
				Assert.That(component.LayoutDisplayOrder, Is.EqualTo(new[] { 0, 1, 2 }));

				displayOrderField?.SetValue(component, new List<int> { 0, 0, 2 });
				Assert.That(component.SynchronizeSegments(), Is.True);
				Assert.That(component.LayoutDisplayOrder, Is.EqualTo(new[] { 0, 1, 2 }));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepSynchronizationIdempotentOnAZeroLengthSpline()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.AddLayout(component.SplineLength * 0.5f);
				var spline = component.SplineContainer.Spline;
				var end = spline[1];
				end.Position = spline[0].Position;
				spline.SetKnot(1, end);
				component.SynchronizeSegments();
				var renderVersion = component.RenderGeometryVersion;
				var colliderVersion = component.ColliderGeometryVersion;

				Assert.That(component.SynchronizeSegments(), Is.False);
				Assert.That(component.RenderGeometryVersion, Is.EqualTo(renderVersion));
				Assert.That(component.ColliderGeometryVersion, Is.EqualTo(colliderVersion));
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
			const int radialSegments = 10;
			const int capVertexCount = radialSegments + 1;
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetWireCapBevelSize(0f);
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
				var rowCount = vertices.Length / rowSize;
				var secondStartRow = -1;
				for (var rowIndex = 1; rowIndex < rowCount; rowIndex++) {
					if (RowsMatch(vertices, (rowIndex - 1) * rowSize,
							rowIndex * rowSize, rowSize)) {
						secondStartRow = rowIndex;
						break;
					}
				}
				Assert.That(secondStartRow, Is.GreaterThan(1));
				var firstStartX = AverageRowX(vertices, 0, rowSize);
				var firstEndX = AverageRowX(vertices,
					(secondStartRow - 1) * rowSize, rowSize);
				var secondStartX = AverageRowX(vertices,
					secondStartRow * rowSize, rowSize);
				var secondEndX = AverageRowX(vertices,
					(rowCount - 1) * rowSize, rowSize);
				var hasAuthoredMiddle = false;
				for (var rowIndex = 1; rowIndex < secondStartRow - 1; rowIndex++) {
					var rowX = AverageRowX(vertices, rowIndex * rowSize, rowSize);
					if (math.abs(rowX - firstStartX - 10f) < 0.01f) {
						hasAuthoredMiddle = true;
						break;
					}
				}

				Assert.That(hasAuthoredMiddle, Is.True);
				Assert.That(firstEndX - firstStartX, Is.EqualTo(40f).Within(0.01f));
				Assert.That(secondStartX, Is.EqualTo(firstEndX).Within(0.01f));
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
					new Vector2(-15f, 0f), new Vector2(15f, 0f));

				component.SetRailCount(3);
				AssertOffsets(component.Segments[0],
					new Vector2(-15f, 0f), new Vector2(15f, 0f),
					new Vector2(30f, 30f));
				component.SetThirdRailSide(0, WireRailThirdRailSide.Left);
				Assert.That(component.Segments[0].GetRailOffset(2),
					Is.EqualTo(new Vector2(-30f, 30f)));

				component.SetRailCount(4);
				AssertOffsets(component.Segments[0],
					new Vector2(-15f, 0f), new Vector2(15f, 0f),
					new Vector2(-30f, 30f), new Vector2(30f, 30f));

				component.SetRailCount(5);
				Assert.That(component.Segments[0].GetRailOffset(4),
					Is.EqualTo(new Vector2(0f, 60f)));
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
			Assert.That(offsets[4], Is.EqualTo(new Vector2(-15f, 60f)));
			Assert.That(offsets[5], Is.EqualTo(new Vector2(0f, 60f)));
			Assert.That(offsets[6], Is.EqualTo(new Vector2(15f, 60f)));
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
				Assert.That(component.Segments[0].GetWireDiameter(0), Is.EqualTo(6.5f));
				Assert.That(component.Segments[0].GetWireDiameter(1), Is.EqualTo(6.5f));
				Assert.That(component.Segments[0].GetWireDiameter(2), Is.EqualTo(6.5f));
				Assert.That(component.RenderMesh.bounds.min.x,
					Is.EqualTo(-25.25f).Within(0.05f));
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
				component.AddLegFixture(250f);

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
				Assert.That(((WireRailLegFixture)component.Fixtures[2]).Diameter,
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
		public void ShouldGenerateVisibleDecagonalWireTubesWithTheAuthoringDefaults()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var mesh = component.RenderMesh;

				Assert.That(mesh, Is.Not.Null);
				Assert.That(mesh.vertexCount, Is.GreaterThan(0));
				Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount));
				Assert.That(component.WireDiameter, Is.EqualTo(6.5f));
				Assert.That(component.WireCapBevelSize, Is.EqualTo(0.5f));
				Assert.That(mesh.bounds.min.x, Is.EqualTo(-33.25f).Within(0.05f));
				Assert.That(mesh.bounds.max.x, Is.EqualTo(33.25f).Within(0.05f));
				Assert.That(mesh.hideFlags & HideFlags.DontSaveInEditor,
					Is.EqualTo(HideFlags.DontSaveInEditor));
				Assert.That(mesh.hideFlags & HideFlags.DontSaveInBuild,
					Is.EqualTo(HideFlags.DontSaveInBuild));
				Assert.That(component.ColliderMesh.hideFlags & HideFlags.DontSaveInEditor,
					Is.EqualTo(HideFlags.DontSaveInEditor));
				Assert.That(component.ColliderMesh.hideFlags & HideFlags.DontSaveInBuild,
					Is.EqualTo(HideFlags.DontSaveInBuild));
				var serializedComponent = new SerializedObject(component);
				Assert.That(serializedComponent.FindProperty("_radialSegments").intValue,
					Is.EqualTo(10));
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
		public void ShouldKeepTheLegacyReferenceDiameterForUninitializedSegments()
		{
			var segment = new WireRailSegment();

			Assert.That(WireRailLayout.DefaultWireDiameter, Is.EqualTo(6.5f));
			Assert.That(WireRailLayout.ReferenceWireDiameter, Is.EqualTo(8f));
			Assert.That(segment.GetWireDiameter(0), Is.EqualTo(8f),
				"a segment without owning-component context must retain the legacy fallback");
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
				Assert.That(component.ColliderMesh, Is.Not.Null, component.GenerationError);
				Assert.That(component.RenderMesh, Is.Not.SameAs(renderMesh));
				Assert.That(component.ColliderMesh, Is.Not.SameAs(colliderMesh));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldGenerateTheColliderOnlyWhenItIsConsumed()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var colliderField = typeof(WireRailComponent).GetField("_colliderMesh",
					BindingFlags.Instance | BindingFlags.NonPublic);
				var dirtyField = typeof(WireRailComponent).GetField("_colliderGeometryDirty",
					BindingFlags.Instance | BindingFlags.NonPublic);

				Assert.That(component.RenderMesh, Is.Not.Null);
				Assert.That(colliderField, Is.Not.Null);
				Assert.That(dirtyField, Is.Not.Null);
				Assert.That(colliderField.GetValue(component), Is.Null);
				Assert.That(dirtyField.GetValue(component), Is.True);

				var collider = component.ColliderMesh;

				Assert.That(collider, Is.Not.Null);
				Assert.That(colliderField.GetValue(component), Is.SameAs(collider));
				Assert.That(dirtyField.GetValue(component), Is.False);

				component.InvalidateColliderGeometry();
				Assert.That(colliderField.GetValue(component), Is.SameAs(collider));
				Assert.That(dirtyField.GetValue(component), Is.True);
				Assert.That(component.ColliderMesh, Is.SameAs(collider));
				Assert.That(dirtyField.GetValue(component), Is.False);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepStressRailMeshesStableAcrossCachedRebuilds()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				component.SetRailCount(6);
				spline.Insert(1, new BezierKnot(new float3(120f, 160f, 35f)),
					TangentMode.AutoSmooth);
				spline.Insert(2, new BezierKnot(new float3(-90f, 330f, 80f)),
					TangentMode.AutoSmooth);
				component.AddLayout(component.SplineLength * 0.33f);
				component.AddLayout(component.SplineLength * 0.66f);
				component.AddBraceFixture(component.SplineLength * 0.25f);
				component.AddCrossWireFixture(component.SplineLength * 0.5f);
				component.AddVBraceFixture(component.SplineLength * 0.75f);

				var renderSignature = ComputeMeshSignature(component.RenderMesh);
				var colliderSignature = ComputeMeshSignature(component.ColliderMesh);
				Assert.That(renderSignature, Is.EqualTo(1557523280122205228UL));
				Assert.That(colliderSignature, Is.EqualTo(5994109088990827143UL));

				component.RebuildGeneratedMeshes();

				Assert.That(ComputeMeshSignature(component.RenderMesh),
					Is.EqualTo(renderSignature));
				Assert.That(ComputeMeshSignature(component.ColliderMesh),
					Is.EqualTo(colliderSignature));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldCoalesceInspectorRebuildRequests()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var initialGenerationCount = component.RenderMeshGenerationCount;
				component.DeferEditorRebuildsForTesting = true;
				component.SetRailOffset(0, 0, new Vector2(-20f, 2f));
				component.SetRailOffset(0, 0, new Vector2(-22f, 3f));
				component.SetRailOffset(0, 0, new Vector2(-24f, 4f));
				component.DeferEditorRebuildsForTesting = false;

				Assert.That(component.RenderMeshGenerationCount,
					Is.EqualTo(initialGenerationCount));
				component.FlushDeferredEditorRebuildForTesting();
				Assert.That(component.RenderMeshGenerationCount,
					Is.EqualTo(initialGenerationCount + 1));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(2, 3, false)]
		[TestCase(3, 5, false)]
		[TestCase(4, 7, false)]
		[TestCase(5, 8, true)]
		[TestCase(6, 8, true)]
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
		public void ShouldOpenTheTopWhenTheUpperRailGapCanPassTheBall()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(6);
			offsets[4] = new Vector2(-55f, 60f);
			offsets[5] = new Vector2(55f, 60f);
			var wireRadii = Enumerable.Repeat(3.25f, offsets.Length).ToArray();

			Assert.That(WireRailChannelProfile.TryCreate(offsets, wireRadii, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(profile.IsClosed, Is.False);
			Assert.That(profile.Spans.Any(span =>
				(span.StartVertex == 0 && span.EndVertex == profile.Vertices.Count - 1)
				|| (span.EndVertex == 0
					&& span.StartVertex == profile.Vertices.Count - 1)), Is.False,
				"the passable upper gap must not receive a roof facet");
			Assert.That(profile.Vertices, Has.Count.EqualTo(offsets.Length),
				"the open channel must have one inward contact vertex per rail");
			Assert.That(profile.Spans, Has.Count.EqualTo(profile.Vertices.Count - 1));
			Assert.That(MatchesRailContact(profile.Vertices[0], 4), Is.True,
				"the left rim must terminate at the left upper rail contact");
			Assert.That(MatchesRailContact(profile.Vertices[^1], 5), Is.True,
				"the right rim must terminate at the right upper rail contact");
			Assert.That(profile.Vertices.Max(vertex => vertex.y), Is.LessThan(60f),
				"open side facets must not extrapolate above the authored upper rails");
			for (var vertexIndex = 0; vertexIndex < profile.Vertices.Count; vertexIndex++) {
				Assert.That(Enumerable.Range(0, offsets.Length)
					.Any(railIndex => MatchesRailContact(profile.Vertices[vertexIndex],
						railIndex)), Is.True,
					$"profile vertex {vertexIndex} must be an actual rail contact, not a chamfer notch");
			}

			bool MatchesRailContact(float2 vertex, int railIndex)
			{
				var railCenter = (float2)offsets[railIndex];
				var normal = math.normalizesafe(profile.RestingBallCenter - railCenter);
				return math.distance(vertex, railCenter + normal * wireRadii[railIndex])
					< 0.01f;
			}
		}

		[Test]
		public void ShouldKeepAuthoredBlockingRailsWhenTestingADecimatedTopOpening()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(9);
			offsets[4] = new Vector2(-45f, 60f);
			offsets[5] = new Vector2(-40f, 60f);
			offsets[6] = new Vector2(0f, 60f);
			offsets[7] = new Vector2(40f, 60f);
			offsets[8] = new Vector2(45f, 60f);

			Assert.That(WireRailChannelProfile.TryCreate(offsets, 4f, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(profile.IsClosed, Is.True,
				"an upper rail omitted from the eight-facet profile still blocks the exit");
		}

		[Test]
		public void ShouldCloseTheTopWhenTheClearGapCannotPassTheBall()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(6);
			offsets[4] = new Vector2(-29f, 60f);
			offsets[5] = new Vector2(29f, 60f);
			var wireRadii = Enumerable.Repeat(4f, offsets.Length).ToArray();

			Assert.That(WireRailChannelProfile.TryCreate(offsets, wireRadii, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(profile.IsClosed, Is.True,
				"a 58-unit center spacing leaves exactly 50 units between 4-unit wires");
		}

		[Test]
		public void ShouldOpenEitherSideOfAnOverheadRailWhenTheBallCanPass()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(5);
			offsets[2] = new Vector2(-90f, 30f);
			offsets[4] = new Vector2(-1f, 60f);

			Assert.That(WireRailChannelProfile.TryCreate(offsets, 4f, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(profile.IsClosed, Is.False,
				"both gaps flanking a rail above the ball must be considered within its angular radius");
			Assert.That(MatchesRailContact(profile.Vertices[0], 2), Is.True,
				"one open rim must terminate at the rail on the passable side");
			Assert.That(MatchesRailContact(profile.Vertices[^1], 4), Is.True,
				"the other open rim must terminate at the overhead rail");

			bool MatchesRailContact(float2 vertex, int railIndex)
			{
				var railCenter = (float2)offsets[railIndex];
				var normal = math.normalizesafe(profile.RestingBallCenter - railCenter);
				return math.distance(vertex, railCenter + normal * 4f) < 0.01f;
			}
		}

		[Test]
		public void ShouldCheckTheOuterFlanksOfEveryRailCoveringTheTopDirection()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(6);
			offsets[2] = new Vector2(-90f, 30f);
			offsets[4] = new Vector2(-4f, 60f);
			offsets[5] = new Vector2(4f, 60f);

			Assert.That(WireRailChannelProfile.TryCreate(offsets, 4f, 25f,
				out var profile, out var error), Is.True, error);
			Assert.That(profile.IsClosed, Is.False);
			Assert.That(profile.TopOpening.FirstRailIndex, Is.EqualTo(4));
			Assert.That(profile.TopOpening.SecondRailIndex, Is.EqualTo(2));
		}

		[Test]
		public void ShouldKeepTheSameOpeningPairWhenTheOppositeFlankBecomesWider()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(5);
				component.SetRailOffset(0, 2, new Vector2(-91f, 30f));
				component.SetRailOffset(0, 3, new Vector2(90f, 30f));
				var nextLayout = component.AddLayout(component.SplineLength);
				component.SetRailOffset(nextLayout, 2, new Vector2(-90f, 30f));
				component.SetRailOffset(nextLayout, 3, new Vector2(91f, 30f));

				Assert.That(component.ColliderMesh, Is.Not.Null, component.GenerationError);
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
				Assert.That(component.GenerationError, Is.Null);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepTheOpeningPairWhenAnUpperRailCrossesDeadCenter()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(5);
				component.SetRailOffset(0, 2, new Vector2(-90f, 30f));
				component.SetRailOffset(0, 3, new Vector2(90f, 30f));
				component.SetRailOffset(0, 4, new Vector2(-1f, 60f));
				var nextLayout = component.AddLayout(component.SplineLength);
				component.SetRailOffset(nextLayout, 4, new Vector2(1f, 60f));

				Assert.That(component.ColliderMesh, Is.Not.Null, component.GenerationError);
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
				Assert.That(component.GenerationError, Is.Null);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepTheTopOpenAcrossAClosingLayoutTransition()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(6);
				component.SetRailOffset(0, 4, new Vector2(-55f, 60f));
				component.SetRailOffset(0, 5, new Vector2(55f, 60f));
				var nextLayout = component.AddLayout(100f);
				component.SetRailOffset(nextLayout, 4, new Vector2(-15f, 60f));
				component.SetRailOffset(nextLayout, 5, new Vector2(15f, 60f));

				Assert.That(component.ColliderMesh, Is.Not.Null);
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
				Assert.That(component.GenerationError, Is.Null);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepTheSameRailPairAtAForcedTopOpening()
		{
			var closedOffsets = WireRailLayout.CreateDefaultOffsets(5);
			var openOffsets = WireRailLayout.CreateDefaultOffsets(5);
			openOffsets[2] = new Vector2(-90f, 30f);
			var wireRadii = Enumerable.Repeat(4f, 5).ToArray();

			Assert.That(WireRailChannelProfile.TryCreate(openOffsets, wireRadii, 25f,
				new Vector2(0f, 30f), out var openProfile, out var openError),
				Is.True, openError);
			Assert.That(openProfile.IsClosed, Is.False);
			Assert.That(openProfile.TopOpening.IsValid, Is.True);
			Assert.That(WireRailChannelProfile.TryCreate(closedOffsets, wireRadii, 25f,
				new Vector2(0f, 30f), openProfile.TopOpening, false,
				out var forcedProfile, out _, out var forcedError), Is.True, forcedError);
			Assert.That(forcedProfile.TopOpening.FirstRailIndex,
				Is.EqualTo(openProfile.TopOpening.FirstRailIndex));
			Assert.That(forcedProfile.TopOpening.SecondRailIndex,
				Is.EqualTo(openProfile.TopOpening.SecondRailIndex));
			Assert.That(MatchesRailContact(forcedProfile.Vertices[0],
				forcedProfile.TopOpening.SecondRailIndex), Is.True);
			Assert.That(MatchesRailContact(forcedProfile.Vertices[^1],
				forcedProfile.TopOpening.FirstRailIndex), Is.True);

			bool MatchesRailContact(float2 vertex, int railIndex)
			{
				var center = (float2)closedOffsets[railIndex];
				var normal = math.normalizesafe(forcedProfile.RestingBallCenter - center);
				return math.distance(vertex, center + normal * wireRadii[railIndex]) < 0.01f;
			}
		}

		[Test]
		public void ShouldUseAClosedProfileWhenARailMovesThroughTheForcedOpening()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(6);
				component.SetRailOffset(0, 4, new Vector2(-55f, 60f));
				component.SetRailOffset(0, 5, new Vector2(55f, 60f));
				var nextLayout = component.AddLayout(component.SplineLength);
				component.SetRailOffset(nextLayout, 3, new Vector2(0f, 75f));
				component.SetRailOffset(nextLayout, 4, new Vector2(-55f, 60f));
				component.SetRailOffset(nextLayout, 5, new Vector2(55f, 60f));

				Assert.That(component.ColliderMesh, Is.Not.Null, component.GenerationError);
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
				Assert.That(component.GenerationError, Is.Null);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldUseAClosedProfileWhenARailCrossesTheOpeningBetweenProbes()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(6);
				component.SetRailOffset(0, 4, new Vector2(-55f, 60f));
				component.SetRailOffset(0, 5, new Vector2(55f, 60f));
				var nextLayout = component.AddLayout(component.SplineLength);
				component.SetRailOffset(nextLayout, 3, new Vector2(-60f, 30f));
				component.SetWireTransitionCurve(0, 3, new AnimationCurve(
					new Keyframe(0f, 0f), new Keyframe(0.125f, 1f / 3f),
					new Keyframe(0.25f, 0f), new Keyframe(0.9f, 0f),
					new Keyframe(1f, 1f)));
				var spline = component.SplineContainer.Spline;
				spline.Insert(1, new BezierKnot(new float3(500f, 250f, 0f)),
					TangentMode.Linear);
				component.SetLayoutDistance(nextLayout, component.SplineLength);

				Assert.That(component.ColliderMesh, Is.Not.Null, component.GenerationError);
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
				Assert.That(component.ColliderTopologyRetryCount, Is.EqualTo(1));
				Assert.That(component.GenerationError, Is.Null);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldReportWhenTheTopOpeningMovesToAnotherRailPair()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(5);
				component.SetRailOffset(0, 2, new Vector2(-90f, 30f));
				var nextLayout = component.AddLayout(component.SplineLength);
				component.SetRailOffset(nextLayout, 2, new Vector2(-30f, 30f));
				component.SetRailOffset(nextLayout, 3, new Vector2(90f, 30f));

				Assert.That(component.ColliderMesh, Is.Null);
				Assert.That(component.GenerationError, Is.Not.Null);
				Assert.That(component.GenerationError.Contains(
					"top opening moves between different rail pairs"), Is.True);
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldRetryOpenWhenAdaptiveSamplingFindsANarrowTransitionGap()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(6);
				component.SetRailOffset(0, 4, new Vector2(-10f, 60f));
				component.SetRailOffset(0, 5, new Vector2(45f, 60f));
				var nextLayout = component.AddLayout(component.SplineLength);
				component.SetRailOffset(nextLayout, 4, new Vector2(-45f, 60f));
				component.SetRailOffset(nextLayout, 5, new Vector2(10f, 60f));
				component.SetWireTransitionCurve(0, 4, new AnimationCurve(
					new Keyframe(0f, 0f), new Keyframe(0.125f, 1f),
					new Keyframe(0.25f, 0f), new Keyframe(0.9f, 0f),
					new Keyframe(1f, 1f)));
				component.SetWireTransitionCurve(0, 5, new AnimationCurve(
					new Keyframe(0f, 0f), new Keyframe(0.9f, 0f),
					new Keyframe(1f, 1f)));
				var spline = component.SplineContainer.Spline;
				spline.Insert(1, new BezierKnot(new float3(500f, 250f, 0f)),
					TangentMode.Linear);
				component.SetLayoutDistance(nextLayout, component.SplineLength);

				Assert.That(component.ColliderMesh, Is.Not.Null, component.GenerationError);
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(0));
				Assert.That(component.ColliderTopologyRetryCount, Is.EqualTo(1),
					$"collider vertices: {component.ColliderMesh.vertexCount}; "
						+ $"layout distance: {component.Segments[nextLayout].Distance}; "
						+ $"spline length: {component.SplineLength}");
				Assert.That(component.GenerationError, Is.Null);
			} finally {
				Object.DestroyImmediate(go);
			}
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
		public void ShouldOpenAChannelDownwardWhenOnlyTheTopRailsAreActive()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(6);
			var topOffsets = new[] { offsets[4], offsets[5] };
			var wireRadii = new[] { 4f, 4f };
			var envelopeCenter = new Vector2(0f, 30f);

			Assert.That(WireRailChannelProfile.TryCreate(topOffsets, wireRadii, 25f,
				envelopeCenter, out var profile, out var error), Is.True, error);
			Assert.That(profile.RestingBallCenter.y, Is.LessThan(topOffsets[0].y));
			Assert.That(profile.IsClosed, Is.False);
		}

		[Test]
		public void ShouldKeepATwoRailChannelAboveReversedSupports()
		{
			var offsets = new[] { new Vector2(15f, 0f), new Vector2(-15f, 0f) };
			var wireRadii = new[] { 4f, 4f };

			Assert.That(WireRailChannelProfile.TryCreate(offsets, wireRadii, 25f,
				Vector2.zero, out var profile, out var error), Is.True, error);
			Assert.That(profile.RestingBallCenter.y, Is.GreaterThan(0f));
		}

		[Test]
		public void ShouldKeepASingleRailChannelHorizontal()
		{
			var offsets = new[] { new Vector2(-15f, 0f) };
			var wireRadii = new[] { 4f };

			Assert.That(WireRailChannelProfile.TryCreate(offsets, wireRadii, 25f,
				new Vector2(0f, 15f), out var profile, out var error), Is.True, error);
			Assert.That(profile.RestingBallCenter.x, Is.EqualTo(-15f).Within(0.001f));
			Assert.That(profile.Vertices[0].y,
				Is.EqualTo(profile.Vertices[1].y).Within(0.001f));
		}

		[Test]
		public void ShouldNotSelfIntersectAChannelBelowThreeUpperRails()
		{
			var allOffsets = WireRailLayout.CreateDefaultOffsets(7);
			var offsets = new[] { allOffsets[4], allOffsets[5], allOffsets[6] };
			var wireRadii = new[] { 4f, 4f, 4f };

			Assert.That(WireRailChannelProfile.TryCreate(offsets, wireRadii, 25f,
				new Vector2(0f, 30f), out var profile, out var error), Is.True, error);
			Assert.That(profile.RestingBallCenter.y, Is.LessThan(offsets[0].y));
			for (var firstSpanIndex = 0; firstSpanIndex < profile.Spans.Count;
				firstSpanIndex++) {
				var firstSpan = profile.Spans[firstSpanIndex];
				for (var secondSpanIndex = firstSpanIndex + 2;
					secondSpanIndex < profile.Spans.Count; secondSpanIndex++) {
					var secondSpan = profile.Spans[secondSpanIndex];
					Assert.That(SegmentsProperlyIntersect(
						profile.Vertices[firstSpan.StartVertex],
						profile.Vertices[firstSpan.EndVertex],
						profile.Vertices[secondSpan.StartVertex],
						profile.Vertices[secondSpan.EndVertex]), Is.False);
				}
			}
		}

		[Test]
		public void ShouldGenerateAChannelColliderInsteadOfPerWireTubes()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();

				Assert.That(component.ColliderMesh, Is.Not.Null);
				Assert.That(component.ColliderMesh.triangles, Has.Length.EqualTo(28 * 3));
				Assert.That(component.ColliderMesh.normals, Is.Empty);
				Assert.That(component.ColliderMesh.uv, Is.Empty);
				Assert.That(component.RenderMesh.vertexCount,
					Is.GreaterThan(component.ColliderMesh.vertexCount));

				component.SetRailCount(2);
				Assert.That(component.ColliderMesh.triangles, Has.Length.EqualTo(12 * 3));
				Assert.That(component, Is.InstanceOf<ICollidableComponent>());
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldLinearlyWidenTheColliderRadiusFromEachEndpoint()
		{
			var widening = new WireRailColliderWidening(true, 2f, 100f,
				true, 3f, 50f);

			Assert.That(widening.EvaluateRadius(25f, 0f, 500f), Is.EqualTo(50f));
			Assert.That(widening.EvaluateRadius(25f, 50f, 500f), Is.EqualTo(37.5f));
			Assert.That(widening.EvaluateRadius(25f, 100f, 500f), Is.EqualTo(25f));
			Assert.That(widening.EvaluateRadius(25f, 475f, 500f), Is.EqualTo(50f));
			Assert.That(widening.EvaluateRadius(25f, 500f, 500f), Is.EqualTo(75f));

			var overlapping = new WireRailColliderWidening(true, 2f, 100f,
				true, 3f, 100f);
			Assert.That(overlapping.EvaluateRadius(25f, 50f, 100f), Is.EqualTo(50f),
				"overlapping tapers must select the larger radius instead of multiplying them");
		}

		[Test]
		public void ShouldKeepWidenedColliderTopologyBasedOnTheReferenceBallDiameter()
		{
			var offsets = WireRailLayout.CreateDefaultOffsets(6);
			offsets[4] = new Vector2(-35f, 60f);
			offsets[5] = new Vector2(35f, 60f);
			var wireRadii = Enumerable.Repeat(4f, 6).ToArray();

			Assert.That(WireRailChannelProfile.TryCreate(offsets, wireRadii, 50f, 50f,
				new Vector2(0f, 30f), null, false, out var profile, out _, out var error),
				Is.True, error);
			Assert.That(profile.IsClosed, Is.False);
			Assert.That(profile.TopOpening.IsValid, Is.True);
		}

		[Test]
		public void ShouldWidenTheColliderStartOverAuthoredRouteLength()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var baselineMesh = component.ColliderMesh;
				var baselineSignature = ComputeMeshSignature(baselineMesh);
				var baselineVertexCount = baselineMesh.vertexCount;
				var rowSize = baselineVertexCount / 2;
				var baselineExtent = CrossSectionExtent(baselineMesh.vertices, 0, rowSize);
				var renderSignature = ComputeMeshSignature(component.RenderMesh);
				component.SetColliderWidening(true, 1f, 100f,
					true, 1f, 100f);
				Assert.That(ComputeMeshSignature(component.ColliderMesh),
					Is.EqualTo(baselineSignature),
					"a size multiplier of 1 must leave geometry and tessellation unchanged");

				component.SetColliderWidening(true, 2f, 100f,
					false, 1f, 100f);

				var widenedMesh = component.ColliderMesh;
				var widenedVertices = widenedMesh.vertices;
				Assert.That(component.WidenStart, Is.True);
				Assert.That(component.WidenStartSize, Is.EqualTo(2f));
				Assert.That(component.WidenStartLength, Is.EqualTo(100f));
				Assert.That(ComputeMeshSignature(widenedMesh),
					Is.Not.EqualTo(baselineSignature));
				Assert.That(ComputeMeshSignature(component.RenderMesh), Is.EqualTo(renderSignature),
					"collider widening must not rebuild or alter the visible wires");
				Assert.That(widenedMesh.vertexCount, Is.GreaterThan(baselineVertexCount));
				Assert.That(CrossSectionExtent(widenedVertices, 0, rowSize),
					Is.GreaterThan(baselineExtent));

				Assert.That(WireRailSplineGeometry.TryEvaluateDistance(
					component.SplineContainer.Spline, 100f, out var taperEndFrame), Is.True);
				var taperEndRow = FindRowAtRouteY(widenedVertices, rowSize,
					taperEndFrame.Position.y);
				Assert.That(taperEndRow, Is.GreaterThanOrEqualTo(0),
					"the authored taper length must create an exact collider row; rows: "
					+ string.Join(", ", Enumerable.Range(0, widenedVertices.Length / rowSize)
						.Select(row => AverageRowY(widenedVertices, row * rowSize, rowSize))));
				Assert.That(CrossSectionExtent(widenedVertices, taperEndRow, rowSize),
					Is.EqualTo(baselineExtent).Within(0.01f));
				Assert.That(CrossSectionExtent(widenedVertices,
					widenedVertices.Length - rowSize, rowSize),
					Is.EqualTo(baselineExtent).Within(0.01f));

				component.SetColliderWidening(false, 1f, 100f,
					true, 1.5f, 75f);
				var widenedExitVertices = component.ColliderMesh.vertices;
				Assert.That(component.WidenStart, Is.False);
				Assert.That(component.WidenExit, Is.True);
				Assert.That(component.WidenExitSize, Is.EqualTo(1.5f));
				Assert.That(component.WidenExitLength, Is.EqualTo(75f));
				Assert.That(CrossSectionExtent(widenedExitVertices, 0, rowSize),
					Is.EqualTo(baselineExtent).Within(0.01f));
				Assert.That(CrossSectionExtent(widenedExitVertices,
					widenedExitVertices.Length - rowSize, rowSize),
					Is.GreaterThan(baselineExtent));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldNotInvalidateColliderGeometryWhenWideningIsUnchanged()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				Assert.That(component.ColliderMesh, Is.Not.Null);
				var geometryVersion = component.ColliderGeometryVersion;

				component.SetColliderWidening(component.WidenStart,
					component.WidenStartSize, component.WidenStartLength,
					component.WidenExit, component.WidenExitSize, component.WidenExitLength);

				Assert.That(component.ColliderGeometryVersion, Is.EqualTo(geometryVersion));
				Assert.That(component.ColliderGeometryDirty, Is.False);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[TestCase(WireRailEndpoint.Start)]
		[TestCase(WireRailEndpoint.End)]
		public void ShouldAlignDropFacesWithAWidenedColliderEndpoint(WireRailEndpoint endpoint)
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				component.SetColliderWidening(endpoint == WireRailEndpoint.Start, 2f, 100f,
					endpoint == WireRailEndpoint.End, 2f, 100f);
				component.AddDropFixture(endpoint);

				var vertices = component.ColliderMesh.vertices;
				const int dropVertexCount = 8;
				var channelVertexCount = vertices.Length - dropVertexCount;
				Assert.That(channelVertexCount, Is.GreaterThan(0));
				for (var dropVertex = channelVertexCount;
					dropVertex < vertices.Length; dropVertex += 2) {
					Assert.That(vertices.Take(channelVertexCount).Any(channelVertex =>
						Vector3.Distance(channelVertex, vertices[dropVertex]) < 0.0001f), Is.True,
						"each widened drop face must start on a vertex of the channel endpoint");
				}
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldPreserveTrimAndWideningBoundariesInTheSameCollider()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SetRailCount(2);
				var baselineMesh = component.ColliderMesh;
				var rowSize = baselineMesh.vertexCount / 2;
				var baselineExtent = CrossSectionExtent(baselineMesh.vertices, 0, rowSize);
				var trimIndex = component.AddRailTrimFixture(WireRailEndpoint.Start);
				component.SetRailTrimFixtureProperties(trimIndex, WireRailEndpoint.Start,
					new[] { 40f, 40f });
				component.SetColliderWidening(true, 2f, 100f,
					false, 1f, 100f);

				var vertices = component.ColliderMesh.vertices;
				Assert.That(WireRailSplineGeometry.TryEvaluateDistance(
					component.SplineContainer.Spline, 40f, out var trimFrame), Is.True);
				Assert.That(WireRailSplineGeometry.TryEvaluateDistance(
					component.SplineContainer.Spline, 100f, out var taperFrame), Is.True);
				var trimRow = FindRowAtRouteY(vertices, rowSize, trimFrame.Position.y);
				var taperRow = FindRowAtRouteY(vertices, rowSize, taperFrame.Position.y);
				Assert.That(trimRow, Is.GreaterThanOrEqualTo(0),
					"the shared rail-trim boundary must remain an exact collider row");
				Assert.That(taperRow, Is.GreaterThanOrEqualTo(0),
					"the widening boundary must remain an exact collider row");
				Assert.That(CrossSectionExtent(vertices, trimRow, rowSize),
					Is.GreaterThan(baselineExtent));
				Assert.That(CrossSectionExtent(vertices, taperRow, rowSize),
					Is.EqualTo(baselineExtent).Within(0.01f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldIgnoreEndpointWideningOnAClosedRoute()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				component.SplineContainer.Spline.Closed = true;
				var baselineSignature = ComputeMeshSignature(component.ColliderMesh);

				component.SetColliderWidening(true, 2f, 100f,
					true, 2f, 100f);

				Assert.That(ComputeMeshSignature(component.ColliderMesh),
					Is.EqualTo(baselineSignature));
			}
			finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldTessellateTheColliderFromCurvatureInsteadOfKnotCount()
		{
			var go = new GameObject("Wire Rail");
			try {
				var component = go.AddComponent<WireRailComponent>();
				var spline = component.SplineContainer.Spline;
				var straightVertexCount = component.ColliderMesh.vertexCount;

				spline.Insert(1, new BezierKnot(new float3(0f, 250f, 0f)) {
					Rotation = spline[0].Rotation,
				},
					TangentMode.Linear);
				Assert.That(component.ColliderMesh.vertexCount, Is.EqualTo(straightVertexCount),
					"a collinear knot must not force extra collider rows");

				var middle = spline[1];
				middle.Position = new float3(150f, 250f, 0f);
				spline.SetKnot(1, middle);
				Assert.That(component.ColliderMesh.vertexCount, Is.GreaterThan(straightVertexCount),
					"a bend must receive adaptive collider rows");
			} finally {
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
				Assert.That(colliders.Count, Is.EqualTo(52));
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

		private static ulong ComputeMeshSignature(Mesh mesh)
		{
			unchecked {
				const ulong offsetBasis = 14695981039346656037UL;
				const ulong prime = 1099511628211UL;
				var hash = offsetBasis;
				Mix((uint)mesh.vertexCount);
				foreach (var vertex in mesh.vertices) {
					MixQuantized(vertex.x);
					MixQuantized(vertex.y);
					MixQuantized(vertex.z);
				}
				var indices = mesh.triangles;
				Mix((uint)indices.Length);
				foreach (var index in indices) {
					Mix((uint)index);
				}
				return hash;

				void Mix(uint value)
				{
					hash ^= value;
					hash *= prime;
				}

				void MixQuantized(float value)
				{
					var quantized = (int)math.round(value * 10000f);
					Mix((uint)quantized);
				}
			}
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

		private static int FindRowAtRouteY(Vector3[] vertices, int rowSize, float routeY)
		{
			for (var rowStart = 0; rowStart <= vertices.Length - rowSize;
				rowStart += rowSize) {
				if (math.abs(AverageRowY(vertices, rowStart, rowSize) - routeY) <= 0.001f) {
					return rowStart;
				}
			}
			return -1;
		}

		private static float AverageRowY(Vector3[] vertices, int start, int count)
		{
			var sum = 0f;
			for (var index = start; index < start + count; index++) {
				sum += vertices[index].y;
			}
			return sum / count;
		}

		private static float CrossSectionExtent(Vector3[] vertices, int start, int count)
		{
			var minimum = new float2(float.PositiveInfinity);
			var maximum = new float2(float.NegativeInfinity);
			for (var index = start; index < start + count; index++) {
				var point = new float2(vertices[index].x, vertices[index].z);
				minimum = math.min(minimum, point);
				maximum = math.max(maximum, point);
			}
			return math.csum(maximum - minimum);
		}

		private static bool RowsMatch(Vector3[] vertices, int firstStart, int secondStart,
			int count)
		{
			for (var index = 0; index < count; index++) {
				if ((vertices[firstStart + index] - vertices[secondStart + index]).sqrMagnitude
					> 1e-6f) {
					return false;
				}
			}
			return true;
		}

		private static bool SegmentsProperlyIntersect(float2 firstStart, float2 firstEnd,
			float2 secondStart, float2 secondEnd)
		{
			var firstDirection = firstEnd - firstStart;
			var secondDirection = secondEnd - secondStart;
			var firstSideA = Cross(firstDirection, secondStart - firstStart);
			var firstSideB = Cross(firstDirection, secondEnd - firstStart);
			var secondSideA = Cross(secondDirection, firstStart - secondStart);
			var secondSideB = Cross(secondDirection, firstEnd - secondStart);
			return firstSideA * firstSideB < -1e-6f
				&& secondSideA * secondSideB < -1e-6f;

			static float Cross(float2 first, float2 second)
				=> first.x * second.y - first.y * second.x;
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
