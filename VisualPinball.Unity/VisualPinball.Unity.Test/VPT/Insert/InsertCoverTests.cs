// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
