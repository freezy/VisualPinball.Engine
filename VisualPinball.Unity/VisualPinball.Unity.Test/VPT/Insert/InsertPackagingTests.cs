// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertPackagingTests
	{
		[Test]
		public void ImportedInsertKeepsItsGlbMeshWhenEnabled()
		{
			var go = new GameObject("Imported Insert");
			var mesh = new Mesh { name = "GLB insert mesh" };
			try {
				go.AddComponent<MeshFilter>().sharedMesh = mesh;
				go.AddComponent<MeshRenderer>();
				InsertComponent insert;
				using (InsertComponent.DeferGenerationForPackageImport()) {
					insert = go.AddComponent<InsertComponent>();
					Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
					insert.Unpack(insert.Pack());
				}

				go.SetActive(false);
				go.SetActive(true);
				insert.RebuildMesh();

				Assert.That(go.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
				Assert.That(mesh.name, Is.EqualTo("GLB insert mesh"));
			} finally {
				Object.DestroyImmediate(go);
				Object.DestroyImmediate(mesh);
			}
		}

		[Test]
		public void LampMappingSelectsInsertLightOnSharedNode()
		{
			var go = new GameObject("Insert with Light");
			go.SetActive(false);
			try {
				go.AddComponent<LightComponent>();
				var insertLight = go.AddComponent<InsertLightComponent>();
				var refs = new PackagedRefs(go.transform);
				refs.SetNodeIdsForWrite(new Dictionary<Transform, string> { [go.transform] = "insert-node" });
				refs.SetNodeIdsForRead(new Dictionary<string, Transform> { ["insert-node"] = go.transform });
				var mapping = new LampMapping { Device = insertLight };

				mapping.SaveReference(refs);
				var restored = PackageApi.Packer.Unpack<LampMapping>(PackageApi.Packer.Pack(mapping));
				restored.RestoreReference(refs);

				Assert.That(restored.Device, Is.SameAs(insertLight));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void InsertLightStateAndSpillReferenceRoundTrip()
		{
			var go = new GameObject("Insert Lamp");
			go.SetActive(false);
			var spillGo = new GameObject("Spill");
			spillGo.transform.SetParent(go.transform);
			try {
				var insertLight = go.AddComponent<InsertLightComponent>();
				var spill = spillGo.AddComponent<Light>();
				insertLight.Intensity = 0.35f;
				insertLight.Color = Color.cyan;
				insertLight.ExtraBulbs[0].Intensity = 0.7f;
				insertLight.BulbNames[1] = "Middle";
				insertLight.TintWithLamp = true;
				insertLight.SpillLumens = 3.5f;
				insertLight.SpillLight = spill;

				var refs = new PackagedRefs(go.transform);
				refs.SetNodeIdsForWrite(new Dictionary<Transform, string> {
					[go.transform] = "insert-node",
					[spillGo.transform] = "spill-node",
				});
				refs.SetNodeIdsForRead(new Dictionary<string, Transform> {
					["insert-node"] = go.transform,
					["spill-node"] = spillGo.transform,
				});
				var data = insertLight.Pack();
				var referenceData = insertLight.PackReferences(go.transform, refs, null);
				insertLight.Intensity = 0f;
				insertLight.Color = Color.white;
				insertLight.ExtraBulbs[0].Intensity = 0f;
				insertLight.BulbNames[1] = null;
				insertLight.TintWithLamp = false;
				insertLight.SpillLumens = 0f;
				insertLight.SpillLight = null;

				insertLight.Unpack(data);
				insertLight.UnpackReferences(referenceData, go.transform, refs, null);

				Assert.That(insertLight.Intensity, Is.EqualTo(0.35f));
				Assert.That(insertLight.Color, Is.EqualTo(Color.cyan));
				Assert.That(insertLight.ExtraBulbs[0].Intensity, Is.EqualTo(0.7f));
				Assert.That(insertLight.BulbNames[1], Is.EqualTo("Middle"));
				Assert.That(insertLight.TintWithLamp, Is.True);
				Assert.That(insertLight.SpillLumens, Is.EqualTo(3.5f));
				Assert.That(insertLight.SpillLight, Is.SameAs(spill));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void UnavailableAuthoringDependenciesDoNotAbortSidecarCapture()
		{
			var go = new GameObject("Insert with unavailable dependencies");
			InsertBakeData bake = null;
			Material material = null;
			Texture2D texture = null;
			try {
				var insert = go.AddComponent<InsertComponent>();
				material = new Material(Shader.Find("HDRP/Lit")) { name = "Unsupported pre-bake material" };
				go.GetComponent<MeshRenderer>().sharedMaterial = material;
				texture = new Texture2D(2, 2) { name = "Unavailable bake map" };
				bake = ScriptableObject.CreateInstance<InsertBakeData>();
				bake.NormalMap = texture;
				insert.SetBake(bake);
				LogAssert.Expect(LogType.Warning, new Regex("Insert authoring material.*could not be packaged"));
				LogAssert.Expect(LogType.Warning, new Regex("Insert bake texture.*could not be packaged"));
				var sidecar = InsertAuthoringPayload.Capture(go.transform,
					new Dictionary<Transform, string> { [go.transform] = "insert-node" },
					new Dictionary<Material, string>(), new Dictionary<Texture2D, string>());
				Assert.That(sidecar.Inserts[0].Bake.NormalMapId, Is.Null);
				Assert.That(sidecar.Inserts[0].PreBakeMaterialId, Is.Null);
			} finally {
				Object.DestroyImmediate(go);
				if (bake) { Object.DestroyImmediate(bake); }
				if (material) { Object.DestroyImmediate(material); }
				if (texture) { Object.DestroyImmediate(texture); }
			}
		}

		[Test]
		public void MissingAuthoringReferencesDoNotAbortSidecarRestore()
		{
			var folderName = "InsertPackagingTest-" + Guid.NewGuid().ToString("N");
			var assetFolder = "Assets/" + folderName;
			AssetDatabase.CreateFolder("Assets", folderName);
			var go = new GameObject("Restored Insert");
			try {
				var insert = go.AddComponent<InsertComponent>();
				var sidecar = new InsertAuthoringPayload {
					Inserts = new[] { new InsertAuthoringRecord {
						NodeId = "insert-node",
						GeneratorJson = JsonUtility.ToJson(insert),
						PreBakeMaterialId = "missing-material",
						Bake = new InsertBakeAuthoringRecord { NormalMapId = "missing-texture" },
					} },
				};
				LogAssert.Expect(LogType.Warning, new Regex("Insert bake texture 'missing-texture' could not be imported"));
				LogAssert.Expect(LogType.Warning, new Regex("Insert authoring material profile 'missing-material' could not be rebuilt"));
				sidecar.Restore(id => id == "insert-node" ? go.transform : null,
					new Dictionary<string, Material>(), new Dictionary<string, Texture2D>(),
					Path.Combine(Application.dataPath, folderName));
				Assert.That(insert.Bake, Is.Not.Null);
				Assert.That(insert.Bake.NormalMap, Is.Null);
				Assert.That(insert.PreBakeMaterial, Is.Null);
			} finally {
				Object.DestroyImmediate(go);
				AssetDatabase.DeleteAsset(assetFolder);
			}
		}

		[Test]
		public void EditorSidecarRestoresGeneratorAndBakeAssets()
		{
			var folderName = "InsertPackagingTest-" + Guid.NewGuid().ToString("N");
			var assetFolder = "Assets/" + folderName;
			AssetDatabase.CreateFolder("Assets", folderName);
			var sourceGo = new GameObject("Source Insert");
			var targetGo = new GameObject("Restored Insert");
			InsertBakeData sourceBake = null;
			try {
				var shader = Shader.Find("HDRP/Lit");
				Assert.That(shader, Is.Not.Null);
				var preBake = new Material(shader) { name = "Before Bake" };
				var baked = new Material(shader) { name = "Baked" };
				var normal = new Texture2D(2, 2) { name = "Baked Normal" };
				var optical = new Texture2D(2, 2) { name = "Optical Normal" };
				var response = new Texture2D(2, 2) { name = "Optical Response" };
				AssetDatabase.CreateAsset(preBake, assetFolder + "/Before Bake.mat");
				AssetDatabase.CreateAsset(baked, assetFolder + "/Baked.mat");
				AssetDatabase.CreateAsset(normal, assetFolder + "/Normal.asset");
				AssetDatabase.CreateAsset(optical, assetFolder + "/Optical.asset");
				AssetDatabase.CreateAsset(response, assetFolder + "/Response.asset");

				var source = sourceGo.AddComponent<InsertComponent>();
				source.GetComponent<MeshRenderer>().sharedMaterial = preBake;
				var serialized = new SerializedObject(source);
				serialized.FindProperty("_width").floatValue = 37f;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				source.RebuildMesh();
				sourceBake = ScriptableObject.CreateInstance<InsertBakeData>();
				sourceBake.Hash = "saved-hash";
				sourceBake.Owner = source.EnsureId();
				sourceBake.Uvs = new[] { new Vector2(0.25f, 0.75f) };
				sourceBake.Tangents = new[] { new Vector4(1f, 0f, 0f, 1f) };
				sourceBake.NormalMap = normal;
				sourceBake.OpticalNormalMap = optical;
				sourceBake.OpticalResponseMap = response;
				sourceBake.Material = baked;
				sourceBake.Packer = "test-packer";
				sourceBake.UvFill = 0.42f;
				source.SetBake(sourceBake);

				var sidecar = InsertAuthoringPayload.Capture(sourceGo.transform,
					new Dictionary<Transform, string> { [sourceGo.transform] = "insert-node" },
					new Dictionary<Material, string> { [preBake] = "before", [baked] = "baked" },
					new Dictionary<Texture2D, string> { [normal] = "normal", [optical] = "optical", [response] = "response" });
				var serializedSidecar = PackageApi.Packer.Pack(sidecar);
				var restoredSidecar = PackageApi.Packer.Unpack<InsertAuthoringPayload>(serializedSidecar);
				var target = targetGo.AddComponent<InsertComponent>();
				target.Unpack(source.Pack());
				target.GetComponent<MeshRenderer>().sharedMaterial = baked;
				restoredSidecar.Restore(id => id == "insert-node" ? targetGo.transform : null,
					new Dictionary<string, Material> { ["before"] = preBake, ["baked"] = baked },
					new Dictionary<string, Texture2D> { ["normal"] = normal, ["optical"] = optical, ["response"] = response },
					Path.Combine(Application.dataPath, folderName));

				Assert.That(new SerializedObject(target).FindProperty("_width").floatValue, Is.EqualTo(37f));
				Assert.That(target.Bake, Is.Not.Null);
				Assert.That(target.Bake.Hash, Is.EqualTo("saved-hash"));
				Assert.That(target.Bake.Uvs[0], Is.EqualTo(new Vector2(0.25f, 0.75f)));
				Assert.That(target.Bake.NormalMap, Is.SameAs(normal));
				Assert.That(target.Bake.OpticalNormalMap, Is.SameAs(optical));
				Assert.That(target.Bake.OpticalResponseMap, Is.SameAs(response));
				Assert.That(target.Bake.Material, Is.SameAs(baked));
				Assert.That(target.PreBakeMaterial, Is.SameAs(preBake));
				Assert.That(target.GetComponent<MeshFilter>().sharedMesh.name, Does.StartWith("Insert (generated)"));
			} finally {
				Object.DestroyImmediate(sourceGo);
				Object.DestroyImmediate(targetGo);
				if (sourceBake) { Object.DestroyImmediate(sourceBake); }
				AssetDatabase.DeleteAsset(assetFolder);
			}
		}
	}
}
