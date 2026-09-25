// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Mathematics;

namespace VisualPinball.Unity.Test
{
	public class InsertCoverTests
	{
		private static Vector2 PlayfieldUv(Vector3 p) => new(0.07f - 0.9f * p.x, 0.95f + 0.6f * p.z);

		private static Mesh PlayfieldMesh()
		{
			// Wall vertices duplicate the top edge but have an unrelated UV island.
			var points = new[] {
				new Vector3(-0.3f, 0f, -1.1f), new Vector3(0.5f, 0f, -1.1f),
				new Vector3(0.5f, 0f, 0.1f), new Vector3(-0.3f, 0f, 0.1f),
				new Vector3(-0.3f, 0f, -1.1f), new Vector3(0.5f, 0f, -1.1f),
				new Vector3(0.5f, -0.02f, -1.1f),
			};
			var uv = points.Select(PlayfieldUv).ToArray();
			uv[4] = Vector2.zero;
			uv[5] = Vector2.right;
			uv[6] = Vector2.one;
			return new Mesh {
				vertices = points, uv = uv,
				triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6 },
			};
		}

		[TestCase(0f, 1f)]
		[TestCase(37f, 1f)]
		[TestCase(90f, -1f)]
		public void ShouldAlignUvsAndNormalsAcrossTransforms(float angle, float mirror)
		{
			var playfield = PlayfieldMesh();
			var mesh = new Mesh {
				vertices = new[] { Vector3.down, new Vector3(-0.02f, 0f, -0.03f), new Vector3(0.02f, 0f, 0.03f) },
				uv = new[] { new Vector2(4f, 7f), Vector2.zero, Vector2.zero },
				tangents = new[] { new Vector4(0f, 0f, 1f, -1f), Vector4.zero, Vector4.zero },
			};
			try {
				Assert.That(InsertCoverUv.TryCreate(playfield, out var projection), Is.True);
				var sourceTransform = Matrix4x4.TRS(new Vector3(0.1f, -0.02f, -0.6f), Quaternion.Euler(0f, 14f, 0f), new Vector3(1.2f, 1f, 0.8f));
				var insertTransform = Matrix4x4.TRS(new Vector3(0.3f, 0.02f, -0.4f), Quaternion.Euler(0f, angle, 0f), new Vector3(mirror * 1.7f, 1f, 0.65f));
				var relative = sourceTransform.inverse * insertTransform;
				var scale = new Vector2(-2f, 1.3f);
				var offset = new Vector2(0.13f, -0.27f);
				projection.Apply(mesh, 1, relative, scale, offset);
				Assert.That(mesh.uv[0], Is.EqualTo(new Vector2(4f, 7f)), "body atlas must stay untouched");
				Assert.That(mesh.tangents[0], Is.EqualTo(new Vector4(0f, 0f, 1f, -1f)));
				for (var i = 1; i < mesh.vertexCount; i++) {
					var expected = Vector2.Scale(PlayfieldUv(relative.MultiplyPoint3x4(mesh.vertices[i])), scale) + offset;
					Assert.That(Vector2.Distance(mesh.uv[i], expected), Is.LessThan(1e-5f));
					var tangent = mesh.tangents[i];
					var worldTangent = insertTransform.MultiplyVector(new Vector3(tangent.x, tangent.y, tangent.z)).normalized;
					// Negative material tiling must not flip the mesh's normal-map basis.
					Assert.That(Vector3.Dot(worldTangent, sourceTransform.MultiplyVector(Vector3.left).normalized), Is.GreaterThan(0.9999f));
					var worldBitangent = Vector3.Cross(Vector3.up, worldTangent) * tangent.w * mirror;
					Assert.That(Vector3.Dot(worldBitangent, sourceTransform.MultiplyVector(Vector3.forward).normalized), Is.GreaterThan(0.9999f));
				}
			} finally {
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(playfield);
			}
		}

		[Test]
		public void ShouldRejectNonPlanarOrCollapsedTopUvs()
		{
			var mesh = PlayfieldMesh();
			try {
				var uv = mesh.uv;
				uv[3] += Vector2.one * 0.1f;
				mesh.uv = uv;
				Assert.That(InsertCoverUv.TryCreate(mesh, out _), Is.False);
				mesh.uv = new Vector2[mesh.vertexCount];
				Assert.That(InsertCoverUv.TryCreate(mesh, out _), Is.False);
			} finally {
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldFollowPlayfieldMaterialAndTransformChangesWithoutRebuildingBody()
		{
			var shader = Shader.Find("HDRP/Lit");
			if (shader == null) { Assert.Ignore("Requires HDRP to verify the playfield material binding."); }
			var board = new GameObject("Cover test playfield", typeof(MeshFilter), typeof(MeshRenderer));
			var go = new GameObject("Cover test insert");
			var sourceMesh = PlayfieldMesh();
			var source = new Material(shader);
			var coverShader = Shader.Find("Visual Pinball/Insert Cover");
			Assert.That(coverShader, Is.Not.Null);
			var cover = new Material(coverShader);
			Mesh mesh = null;
			try {
				board.GetComponent<MeshFilter>().sharedMesh = sourceMesh;
				board.GetComponent<MeshRenderer>().sharedMaterial = source;
				source.SetTexture("_NormalMap", Texture2D.whiteTexture);
				source.SetFloat("_NormalScale", 0.48f);
				var insert = go.AddComponent<InsertComponent>();
				typeof(InsertComponent).GetField("_coverPlayfield", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(insert, board.GetComponent<MeshRenderer>());
				mesh = go.GetComponent<MeshFilter>().sharedMesh;
				var bodyUvs = mesh.uv;
				var bodyTangents = mesh.tangents;
				insert.SetCoverMaterial(cover);
				var localUvs = mesh.uv;
				var localTangents = mesh.tangents;
				insert.UsePlayfieldNormals = true;
				Assert.That(insert.CoverWarning, Is.Null);
				go.transform.SetPositionAndRotation(new Vector3(0.2f, 0f, -0.5f), Quaternion.Euler(0f, 32f, 0f));
				go.transform.localScale = new Vector3(1.7f, 1f, 0.8f);
				board.transform.position = new Vector3(0.05f, 0f, -0.1f);
				var tiling = new Vector2(2f, 0.7f);
				var offset = new Vector2(0.2f, -0.1f);
				source.SetTextureScale("_BaseColorMap", tiling);
				source.SetTextureOffset("_BaseColorMap", offset);
				insert.SendMessage("LateUpdate");
				Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
				Assert.That(mesh.uv.Take(bodyUvs.Length), Is.EqualTo(bodyUvs));
				Assert.That(mesh.tangents.Take(bodyTangents.Length), Is.EqualTo(bodyTangents));
				for (var i = bodyUvs.Length; i < mesh.vertexCount; i++) {
					var position = board.transform.InverseTransformPoint(go.transform.TransformPoint(mesh.vertices[i]));
					Assert.That(Vector2.Distance(mesh.uv[i], Vector2.Scale(PlayfieldUv(position), tiling) + offset), Is.LessThan(1e-5f));
				}
				var properties = new MaterialPropertyBlock();
				var renderer = go.GetComponent<MeshRenderer>();
				renderer.GetPropertyBlock(properties, 1);
				Assert.That(properties.GetTexture("_CoverNormalMap"), Is.SameAs(Texture2D.whiteTexture));
				Assert.That(properties.GetFloat("_PlayfieldNormalStrength"), Is.EqualTo(0.48f));
				Assert.That(properties.GetVector("_CoverNormalMap_ST"), Is.EqualTo(new Vector4(1f, 1f, 0f, 0f)));
				renderer.GetPropertyBlock(properties);
				Assert.That(properties.GetTexture("_CoverNormalMap"), Is.Null, "cover map must not override body properties");
				// Removing the source normal map restores local UVs without a body rebuild.
				source.SetTexture("_NormalMap", null);
				insert.SendMessage("LateUpdate");
				Assert.That(mesh.uv, Is.EqualTo(localUvs));
				Assert.That(mesh.tangents, Is.EqualTo(localTangents));
				renderer.GetPropertyBlock(properties, 1);
				Assert.That(properties.isEmpty, Is.True);
				// Reassignment must project again even though transforms did not change.
				source.SetTexture("_NormalMap", Texture2D.whiteTexture);
				insert.SendMessage("LateUpdate");
				Assert.That(mesh.uv, Is.Not.EqualTo(localUvs));
				insert.UsePlayfieldNormals = false;
				Assert.That(mesh.uv, Is.EqualTo(localUvs));
				Assert.That(mesh.tangents, Is.EqualTo(localTangents));
				Assert.That(insert.CoverWarning, Is.Null);
				renderer.GetPropertyBlock(properties, 1);
				Assert.That(properties.isEmpty, Is.True);
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(board);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(sourceMesh);
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(cover);
			}
		}

		[Test]
		public void ShouldReuseProjectionAndRefreshWhenSourceMeshChanges()
		{
			var mesh = PlayfieldMesh();
			try {
				Assert.That(InsertCoverUv.TryCreate(mesh, out var before, out var version), Is.True);
				Assert.That(InsertCoverUv.TryCreate(mesh, out _, out var sameVersion), Is.True);
				Assert.That(sameVersion, Is.EqualTo(version));
				var positions = mesh.vertices;
				for (var i = 0; i < positions.Length; i++) { positions[i].x *= 2f; }
				mesh.vertices = positions;
				mesh.RecalculateBounds();
				Assert.That(InsertCoverUv.TryCreate(mesh, out var resized, out var resizedVersion), Is.True);
				Assert.That(resizedVersion, Is.Not.EqualTo(version));
				Assert.That(Vector2.Distance(resized.Project(Vector3.right * 0.4f), before.Project(Vector3.right * 0.2f)), Is.LessThan(1e-5f));
				var uvs = mesh.uv;
				for (var i = 0; i < uvs.Length; i++) { uvs[i] += Vector2.one * 0.1f; }
				mesh.uv = uvs;
				InsertCoverUv.Invalidate(mesh);
				Assert.That(InsertCoverUv.TryCreate(mesh, out var edited, out var editedVersion), Is.True);
				Assert.That(editedVersion, Is.Not.EqualTo(resizedVersion));
				Assert.That(Vector2.Distance(edited.Project(Vector3.zero), resized.Project(Vector3.zero) + Vector2.one * 0.1f), Is.LessThan(1e-5f));
			} finally { Object.DestroyImmediate(mesh); }
		}

		[Test]
		public void ShouldUseAnyCustomMaterialWithoutAPlayfield()
		{
			var go = new GameObject("Custom cover test");
			var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
			Mesh mesh = null;
			try {
				var insert = go.AddComponent<InsertComponent>();
				insert.SetCoverMaterial(material);
				mesh = go.GetComponent<MeshFilter>().sharedMesh;
				var renderer = go.GetComponent<MeshRenderer>();
				var uv = mesh.uv;
				var tangents = mesh.tangents;
				var outline = insert.Outline;
				go.SetActive(false);
				go.SetActive(true);
				Assert.That(insert.Outline, Is.SameAs(outline), "reactivating an initialized insert must not regenerate its geometry");
				Assert.That(insert.UsePlayfieldNormals, Is.False);
				Assert.That(renderer.sharedMaterials[1], Is.SameAs(material));
				go.transform.SetPositionAndRotation(Vector3.one, Quaternion.Euler(0f, 49f, 0f));
				insert.SendMessage("LateUpdate");
				Assert.That(mesh.uv, Is.EqualTo(uv));
				Assert.That(mesh.tangents, Is.EqualTo(tangents));
				Assert.That(insert.CoverWarning, Is.Null);
				var properties = new MaterialPropertyBlock();
				renderer.GetPropertyBlock(properties, 1);
				Assert.That(properties.isEmpty, Is.True);
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(material);
			}
		}

		[Test]
		public void ShouldErodeAFlashIntoItsEngravedLine()
		{
			var warnings = new System.Collections.Generic.List<string>();
			var corners = InsertShapes.FlashCorners(50f, 125f, 10f, 16f, -8f,
				new InsertCornerSettings(5.2f, 4), new InsertCornerSettings(5f, 4), new InsertCornerSettings(3f, 3), warnings);
			var outline = InsertOutlineBuilder.Build(corners);
			var eroded = InsertShapes.Erode(outline, 6f, 1f, warnings);
			Assert.That(eroded, Is.Not.Null);
			Assert.That(eroded.Length, Is.EqualTo(6), "each flat end closes up into one point");
			Assert.That(eroded.Count(c => c.Tag.Contains("+")), Is.EqualTo(2), "the merged ends carry both corners' names");
			Assert.That(eroded.Where(c => c.Tag.StartsWith("Notch")).Select(c => c.Radius), Is.All.EqualTo(9f).Within(1e-4f), "concave corners widen by the distance");
			Assert.That(eroded.Where(c => c.Tag.StartsWith("Spike")).Select(c => c.Radius), Is.All.EqualTo(1f).Within(1e-4f), "convex corners shrink, never below the minimum");
			var line = InsertOutlineBuilder.Build(eroded, 1, center: false);
			var polygon = outline.Points.Select(p => p.Position).ToList();
			foreach (var point in line.Points) {
				Assert.That(Inside(polygon, point.Position), Is.True, "the line stays inside the flash");
				Assert.That(DistanceToEdges(polygon, point.Position), Is.GreaterThan(5.5f), "and about the inset away from its edge");
			}
			Assert.That(InsertShapes.Erode(outline, 60f, 1f, warnings), Is.Null, "an erosion past the shape leaves nothing");

			// Arms whose notches nearly touch cross each other under the erosion.
			var pinched = InsertOutlineBuilder.Build(InsertShapes.FlashCorners(100f, 125f, 10f, 45f, 0f,
				new InsertCornerSettings(0f, 1), new InsertCornerSettings(0f, 1), new InsertCornerSettings(0f, 4), warnings));
			Assert.That(InsertShapes.Erode(pinched, 6f, 0f, warnings), Is.Null, "an erosion that crosses itself leaves nothing in one piece");
		}

		[Test]
		public void ShouldEngraveTheLineIntoAFlashsMiddleInsteadOfTheStarburst()
		{
			var go = new GameObject("Engraved flash");
			Mesh mesh = null;
			try {
				var insert = go.AddComponent<InsertComponent>();
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_shape").enumValueIndex = (int)InsertShape.Flash;
				// Without a channel the line is eroded from the rim by its inset
				// alone; a channel adds its width, and the default flash's arms
				// would cross under that.
				settings.FindProperty("_channel").boolValue = false;
				settings.FindProperty("_starburst").boolValue = true;
				settings.FindProperty("_flashGroove").boolValue = true;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				mesh = go.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(insert.Groove, Is.Not.Null);
				Assert.That(insert.Body.StarburstBursts, Is.Zero, "the line takes the starburst's place");
				Assert.That(insert.Warnings, Has.Some.Contains("starburst"));
				var surface = InsertMeshGenerator.ToLocal(float2.zero, -insert.Body.Depth + insert.Body.ChannelDepth).y;
				var trough = InsertMeshGenerator.ToLocal(float2.zero, -insert.Body.Depth + insert.Body.ChannelDepth - 0.8f).y;
				var vertices = mesh.vertices;
				var triangles = mesh.GetTriangles(0);
				Assert.That(vertices.Any(v => math.abs(v.y - trough) < 1e-5f), Is.True, "the trough dips below the middle by its depth");
				Assert.That(vertices.Count(v => math.abs(v.y - trough) < 1e-6f), Is.GreaterThanOrEqualTo(3), "the trough's floor runs along the line");
				for (var t = 0; t < triangles.Length; t += 3) {
					var a = vertices[triangles[t]];
					var b = vertices[triangles[t + 1]];
					var c = vertices[triangles[t + 2]];
					if (a.y >= surface - 1e-6f && b.y >= surface - 1e-6f && c.y >= surface - 1e-6f || a.y < trough - 1e-6f || b.y < trough - 1e-6f || c.y < trough - 1e-6f) {
						continue;
					}
					var normal = Vector3.Cross(b - a, c - a);
					if (normal.magnitude < 1e-9f) {
						continue;
					}
					Assert.That(normal.y / normal.magnitude, Is.GreaterThan(-1e-3f), "the trough faces up");
				}
				// The trough's edges are creases: its own corners there face
				// across it, the flat top's corners keep facing up.
				var normals = mesh.normals;
				int across = 0, upOnTrough = 0, tiltedOnFlat = 0;
				for (var t = 0; t < triangles.Length; t += 3) {
					var ids = new[] { triangles[t], triangles[t + 1], triangles[t + 2] };
					// A trough triangle reaches below the surface; a flat one lies on
					// it; a wall's, from the rim down, is neither.
					var inTrough = ids.Any(i => vertices[i].y < surface - 1e-6f && vertices[i].y > trough - 1e-6f);
					var onFlat = ids.All(i => math.abs(vertices[i].y - surface) < 1e-6f);
					foreach (var i in ids) {
						if (math.abs(vertices[i].y - surface) > 1e-6f) {
							continue;
						}
						if (inTrough) {
							if (normals[i].y < 0.05f) { across++; } else { upOnTrough++; }
						} else if (onFlat && normals[i].y < 0.999f) {
							tiltedOnFlat++;
						}
					}
				}
				Assert.That(across, Is.GreaterThan(0));
				Assert.That(upOnTrough, Is.Zero, "a trough corner on the edge faces across the trough");
				Assert.That(tiltedOnFlat, Is.Zero, "a flat corner on the edge faces up");
				settings.FindProperty("_flashGroove").boolValue = false;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				Assert.That(insert.Groove, Is.Null);
				Assert.That(insert.Body.StarburstBursts, Is.GreaterThan(0), "the starburst is back once the line is off");
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldKeepTheTroughFacingUpOnATightLine()
		{
			var go = new GameObject("Tight engraved flash");
			Mesh mesh = null;
			try {
				var insert = go.AddComponent<InsertComponent>();
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_shape").enumValueIndex = (int)InsertShape.Flash;
				settings.FindProperty("_channel").boolValue = false;
				settings.FindProperty("_flashTip").floatValue = 5f;
				settings.FindProperty("_flashNotch").floatValue = 5f;
				settings.FindProperty("_flashStep").floatValue = 0f;
				foreach (var corner in new[] { "_flashTipCorner", "_flashSpikeCorner", "_flashNotchCorner" }) {
					settings.FindProperty(corner).FindPropertyRelative("Radius").floatValue = 0f;
				}
				settings.FindProperty("_flashGroove").boolValue = true;
				settings.FindProperty("_flashGrooveInset").floatValue = 2.1f;
				settings.FindProperty("_flashGrooveWidth").floatValue = 4f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				mesh = go.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(insert.Groove, Is.Not.Null, string.Join(" | ", insert.Warnings));
				var vertices = mesh.vertices;
				var triangles = mesh.GetTriangles(0);
				var surface = InsertMeshGenerator.ToLocal(float2.zero, -insert.Body.Depth + insert.Body.ChannelDepth).y;
				var trough = InsertMeshGenerator.ToLocal(float2.zero, -insert.Body.Depth + insert.Body.ChannelDepth - 0.8f).y;
				for (var t = 0; t < triangles.Length; t += 3) {
					var a = vertices[triangles[t]];
					var b = vertices[triangles[t + 1]];
					var c = vertices[triangles[t + 2]];
					if (!(new[] { a, b, c }.Any(v => v.y < surface - 1e-6f && v.y > trough - 1e-6f))) {
						continue;
					}
					var normal = Vector3.Cross(b - a, c - a);
					if (normal.magnitude < 1e-9f) {
						continue;
					}
					Assert.That(normal.y / normal.magnitude, Is.GreaterThan(-1e-3f), "every trough triangle faces up, collapsed sides included");
				}
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldRefuseALineThatReachesPastTheMiddle()
		{
			var go = new GameObject("Overreaching engraved flash");
			Mesh mesh = null;
			try {
				var insert = go.AddComponent<InsertComponent>();
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_shape").enumValueIndex = (int)InsertShape.Flash;
				settings.FindProperty("_channel").boolValue = false;
				settings.FindProperty("_flashWidth").floatValue = 20f;
				settings.FindProperty("_flashHeight").floatValue = 60f;
				settings.FindProperty("_flashTip").floatValue = 18f;
				settings.FindProperty("_flashNotch").floatValue = 9.8f;
				settings.FindProperty("_flashStep").floatValue = 27f;
				foreach (var corner in new[] { "_flashTipCorner", "_flashSpikeCorner", "_flashNotchCorner" }) {
					settings.FindProperty(corner).FindPropertyRelative("Radius").floatValue = 1f;
				}
				settings.FindProperty("_flashGroove").boolValue = true;
				settings.FindProperty("_flashGrooveInset").floatValue = 1f;
				settings.FindProperty("_flashGrooveWidth").floatValue = 1f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				mesh = go.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(insert.Groove, Is.Null, "a band whose outer edge leaves the middle is not built");
				Assert.That(insert.Warnings, Has.Some.Contains("past the middle"));
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(mesh);
			}
		}

		private static bool Inside(System.Collections.Generic.List<float2> polygon, float2 point)
		{
			var inside = false;
			for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++) {
				var a = polygon[i];
				var b = polygon[j];
				if (a.y > point.y != b.y > point.y && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) {
					inside = !inside;
				}
			}
			return inside;
		}

		private static float DistanceToEdges(System.Collections.Generic.List<float2> polygon, float2 point)
		{
			var nearest = float.MaxValue;
			for (var i = 0; i < polygon.Count; i++) {
				var a = polygon[i];
				var b = polygon[(i + 1) % polygon.Count];
				var side = b - a;
				var t = math.saturate(math.dot(point - a, side) / math.max(math.lengthsq(side), 1e-9f));
				nearest = math.min(nearest, math.distance(point, a + t * side));
			}
			return nearest;
		}

		[UnityTest]
		public IEnumerator ShouldApplyPendingGeometryEditsWhenReenabled()
		{
			var go = new GameObject("Pending insert edit");
			var insert = go.AddComponent<InsertComponent>();
			var mesh = go.GetComponent<MeshFilter>().sharedMesh;
			try {
				var width = mesh.bounds.size.x;
				go.SetActive(false);
				var settings = new UnityEditor.SerializedObject(insert);
				settings.FindProperty("_width").floatValue *= 1.5f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				go.SetActive(true);
				yield return null;
				Assert.That(mesh.bounds.size.x, Is.GreaterThan(width * 1.4f));
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void ShouldFindTheBoardInItsOwnPlayfieldAndLeaveMaterialAssetsUntouched()
		{
			var shader = Shader.Find("HDRP/Lit");
			if (shader == null) { Assert.Ignore("Requires HDRP to verify the playfield material binding."); }
			var root = new GameObject("Automatic cover test", typeof(PlayfieldComponent));
			var board = new GameObject("Imported board", typeof(MeshFilter), typeof(MeshRenderer));
			var go = new GameObject("Auto cover insert");
			var sourceMesh = PlayfieldMesh();
			var source = new Material(shader);
			var cover = new Material(Shader.Find("Visual Pinball/Insert Cover"));
			Mesh mesh = null;
			try {
				board.transform.SetParent(root.transform);
				go.transform.SetParent(root.transform);
				board.GetComponent<MeshFilter>().sharedMesh = sourceMesh;
				board.GetComponent<MeshRenderer>().sharedMaterial = source;
				source.SetTexture("_NormalMap", Texture2D.whiteTexture);
				cover.SetTexture("_CoverNormalMap", Texture2D.blackTexture);
				var disabled = new GameObject("Disabled surface", typeof(MeshFilter), typeof(MeshRenderer));
				disabled.transform.SetParent(root.transform);
				disabled.transform.localScale = Vector3.one * 3f;
				disabled.GetComponent<MeshFilter>().sharedMesh = sourceMesh;
				disabled.GetComponent<MeshRenderer>().sharedMaterial = source;
				disabled.GetComponent<MeshRenderer>().enabled = false;
				var noMesh = new GameObject("No mesh", typeof(MeshRenderer));
				noMesh.transform.SetParent(root.transform);
				noMesh.GetComponent<MeshRenderer>().sharedMaterial = source;
				cover.SetTextureScale("_CoverNormalMap", new Vector2(3f, 4f));
				var insert = go.AddComponent<InsertComponent>();
				insert.SetCoverMaterial(cover);
				insert.UsePlayfieldNormals = true;
				mesh = go.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(insert.CoverPlayfield, Is.SameAs(board.GetComponent<MeshRenderer>()));
				Assert.That(insert.CoverWarning, Is.Null);
				var properties = new MaterialPropertyBlock();
				go.GetComponent<MeshRenderer>().GetPropertyBlock(properties, 1);
				Assert.That(properties.GetTexture("_CoverNormalMap"), Is.SameAs(Texture2D.whiteTexture));
				Assert.That(cover.GetTexture("_CoverNormalMap"), Is.SameAs(Texture2D.blackTexture));
				Assert.That(cover.GetTextureScale("_CoverNormalMap"), Is.EqualTo(new Vector2(3f, 4f)));
				typeof(InsertComponent).GetField("_coverPlayfield", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(insert, noMesh.GetComponent<MeshRenderer>());
				Assert.DoesNotThrow(insert.RebuildMesh);
				Assert.That(insert.CoverWarning, Is.Not.Null);
				typeof(InsertComponent).GetField("_coverPlayfield", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(insert, null);
				// An unrelated insert must never discover a board in another hierarchy.
				go.transform.SetParent(null);
				insert.RebuildMesh();
				Assert.That(insert.CoverPlayfield, Is.Null);
				go.GetComponent<MeshRenderer>().GetPropertyBlock(properties, 1);
				Assert.That(properties.isEmpty, Is.True);
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(sourceMesh);
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(cover);
			}
		}
	}
}
