// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertPackagingTests
	{
		[Test]
		public void ShouldKeepTheDiameterOfABananaSavedBeforeItHadItsOwn()
		{
			var go = new GameObject("Old banana");
			try {
				var insert = go.AddComponent<InsertComponent>();
				// Data from before the split carries no version (read as zero), and the banana's diameter in the circle's.
				JsonUtility.FromJsonOverwrite("{\"_shape\":3,\"_diameter\":200.0,\"_version\":0}", insert);
				Assert.That(new SerializedObject(insert).FindProperty("_bananaDiameter").floatValue, Is.EqualTo(200f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ShouldKeepTheBananaAndCircleDiametersApart()
		{
			var go = new GameObject("Banana");
			try {
				var insert = go.AddComponent<InsertComponent>();
				var settings = new SerializedObject(insert);
				Assert.That(settings.FindProperty("_bananaDiameter").floatValue, Is.EqualTo(510.3f));
				settings.FindProperty("_diameter").floatValue = 200f;
				settings.ApplyModifiedPropertiesWithoutUndo();
				JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(insert), insert);
				Assert.That(new SerializedObject(insert).FindProperty("_bananaDiameter").floatValue, Is.EqualTo(510.3f));
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[UnityTest]
		public IEnumerator EditorPackageRoundTripRestoresGeneratorAndBake()
		{
			var tableName = "InsertPackagingTest-" + Guid.NewGuid().ToString("N");
			var packagePath = Path.Combine(Path.GetTempPath(), tableName + ".vpe");
			GameObject source = null;
			GameObject imported = null;
			InsertBakeData sourceBake = null;
			Material body = null;
			Material cover = null;
			try {
				source = new GameObject(tableName);
				source.AddComponent<TableComponent>();
				var insertGo = new GameObject("Editable Insert");
				insertGo.transform.SetParent(source.transform, false);
				var insert = insertGo.AddComponent<InsertComponent>();
				var generator = new SerializedObject(insert);
				generator.FindProperty("_shape").enumValueIndex = (int)InsertShape.Circle;
				generator.FindProperty("_circleVertices").intValue = 19;
				generator.FindProperty("_diameter").floatValue = 200f;
				generator.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				body = new Material(Shader.Find("Pinball/Insert Hybrid")) { name = "Editor Round Trip Body" };
				cover = new Material(Shader.Find("Pinball/Insert Cover")) { name = "Editor Round Trip Cover" };
				insertGo.GetComponent<MeshRenderer>().sharedMaterial = body;
				insert.SetCoverMaterial(cover);
				sourceBake = ScriptableObject.CreateInstance<InsertBakeData>();
				var bakeMesh = new Mesh();
				var bakeLayout = new InsertMeshLayout();
				insert.Generate(bakeMesh, bakeLayout);
				sourceBake.Hash = bakeLayout.Hash.ToString();
				sourceBake.Owner = insert.EnsureId();
				var triangleCorners = bakeMesh.triangles.Length;
				sourceBake.Uvs = new Vector2[triangleCorners];
				sourceBake.Tangents = new Vector4[triangleCorners];
				for (var i = 0; i < triangleCorners; i++) {
					sourceBake.Uvs[i] = new Vector2(0.25f, 0.75f);
					sourceBake.Tangents[i] = new Vector4(1f, 0f, 0f, 1f);
				}
				Object.DestroyImmediate(bakeMesh);
				insert.SetBake(sourceBake);
				Assert.That(insert.IsBakeFresh, Is.True);
				Assert.That(insertGo.GetComponent<MeshRenderer>().enabled, Is.True);
				Assert.That(insertGo.GetComponent<MeshFilter>().sharedMesh.subMeshCount, Is.EqualTo(2));
				Assert.That(insertGo.GetComponent<MeshRenderer>().sharedMaterials, Has.Length.EqualTo(2));

				new PackageWriter(source).WritePackageSync(packagePath);
				var importTask = new PackageReader(packagePath).ImportIntoScene(tableName);
				while (!importTask.IsCompleted) { yield return null; }
				if (importTask.IsFaulted) { throw importTask.Exception!.GetBaseException(); }
				foreach (var table in Object.FindObjectsByType<TableComponent>(FindObjectsSortMode.None)) {
					if (table.gameObject != source && table.name == tableName) { imported = table.gameObject; break; }
				}
				Assert.That(imported, Is.Not.Null);
				var restored = imported.GetComponentInChildren<InsertComponent>(true);
				Assert.That(restored, Is.Not.Null);
				var restoredGenerator = new SerializedObject(restored);
				Assert.That(restoredGenerator.FindProperty("_shape").enumValueIndex, Is.EqualTo((int)InsertShape.Circle));
				Assert.That(restoredGenerator.FindProperty("_circleVertices").intValue, Is.EqualTo(19));
				Assert.That(restoredGenerator.FindProperty("_diameter").floatValue, Is.EqualTo(200f));
				Assert.That(restored.Bake, Is.Not.Null);
				Assert.That(restored.Bake.Hash, Is.EqualTo(sourceBake.Hash));
				Assert.That(restored.Bake.Uvs[0], Is.EqualTo(new Vector2(0.25f, 0.75f)));
				Assert.That(AssetDatabase.Contains(restored.Bake), Is.True);
				Assert.That(restored.PreBakeMaterial, Is.Not.Null, "the pre-bake material must restore from its profile id");
				var renderers = restored.GetComponentsInChildren<MeshRenderer>(true);
				var rendererLayout = string.Join("; ", Array.ConvertAll(renderers, renderer =>
					$"{renderer.name}: slots={renderer.sharedMaterials.Length}, mesh={renderer.GetComponent<MeshFilter>()?.sharedMesh?.subMeshCount}"));
				Assert.That(restored.GetComponent<MeshRenderer>().sharedMaterials, Has.Length.EqualTo(2),
					"the cover renderer slot must survive the GLB/editor material import: " + rendererLayout);
				Assert.That(restored.CoverMaterial, Is.Not.Null, "the cover material must restore from renderer slot 1");
				Assert.That(restored.PreBakeMaterial.shader.name, Is.EqualTo("Pinball/Insert Hybrid"));
				Assert.That(restored.CoverMaterial.shader.name, Is.EqualTo("Pinball/Insert Cover"));
				Assert.That(restored.GetComponent<MeshFilter>().sharedMesh, Is.Not.Null);
			} finally {
				if (source) { Object.DestroyImmediate(source); }
				if (imported) { Object.DestroyImmediate(imported); }
				if (sourceBake) { Object.DestroyImmediate(sourceBake); }
				if (body) { Object.DestroyImmediate(body); }
				if (cover) { Object.DestroyImmediate(cover); }
				AssetDatabase.DeleteAsset("Assets/Resources/" + tableName);
				File.Delete(packagePath);
			}
		}

		[UnityTest]
		public IEnumerator PackagedInsertUsesGlbMeshAndRestoresLampInRuntimeReader()
		{
			var packagePath = Path.Combine(Path.GetTempPath(), $"vpe-insert-package-{Guid.NewGuid():N}.vpe");
			GameObject source = null;
			GameObject imported = null;
			Material body = null;
			Material cover = null;
			InsertBakeData staleBake = null;
			Mesh customSource = null;
			try {
				source = new GameObject("Table");
				source.AddComponent<TableComponent>();
				var insertGo = new GameObject("Insert");
				insertGo.transform.SetParent(source.transform, false);
				var insert = insertGo.AddComponent<InsertComponent>();
				body = new Material(Shader.Find("Pinball/Insert Hybrid")) { name = "Test Insert Body" };
				cover = new Material(Shader.Find("Pinball/Insert Cover")) { name = "Test Insert Cover" };
				insertGo.GetComponent<MeshRenderer>().sharedMaterial = body;
				insert.SetCoverMaterial(cover);
				var defaultBoundsMagnitude = insertGo.GetComponent<MeshFilter>().sharedMesh.bounds.size.magnitude;
				var generator = new SerializedObject(insert);
				customSource = new Mesh { name = "Custom package outline" };
				customSource.vertices = new[] { new Vector3(-0.06f, 0, -0.12f), new Vector3(0.06f, 0, -0.12f),
					new Vector3(0.06f, 0, 0.12f), new Vector3(0, 0, 0.07f), new Vector3(-0.06f, 0, 0.12f) };
				customSource.SetIndices(new[] { 0, 1, 1, 2, 2, 3, 3, 4, 4, 0 }, MeshTopology.Lines, 0);
				generator.FindProperty("_shape").enumValueIndex = (int)InsertShape.Custom;
				generator.FindProperty("_customMesh").objectReferenceValue = customSource;
				generator.ApplyModifiedPropertiesWithoutUndo();
				insert.RebuildMesh();
				var light = insertGo.AddComponent<InsertLightComponent>();
				light.Intensity = 0.6f;
				light.ExtraBulbs[0].Intensity = 0.4f;
				staleBake = ScriptableObject.CreateInstance<InsertBakeData>();
				staleBake.Hash = "stale-bake";
				insert.SetBake(staleBake);
				Assert.That(insertGo.GetComponent<MeshRenderer>().enabled, Is.False);
				var sourceMesh = insertGo.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(sourceMesh, Is.Not.Null);

				new PackageWriter(source).WritePackageSync(packagePath);
				Assert.That(insertGo.GetComponent<MeshRenderer>().enabled, Is.False,
					"export must restore the author's hidden stale-bake preview");

				var importTask = new RuntimePackageReader(packagePath).ImportIntoScene();
				while (!importTask.IsCompleted) { yield return null; }
				if (importTask.IsFaulted) { throw importTask.Exception!.GetBaseException(); }
				imported = importTask.Result;
				var restoredInsert = imported.GetComponentInChildren<InsertComponent>(true);
				Assert.That(restoredInsert, Is.Not.Null);
				Assert.That(restoredInsert.CustomMesh, Is.Null, "the runtime reader must not load editor source meshes");
				var restoredMesh = restoredInsert.GetComponent<MeshFilter>().sharedMesh;
				Assert.That(restoredMesh, Is.Not.Null);
				Assert.That(restoredMesh.subMeshCount, Is.EqualTo(2), "the disabled insert's cover must be present in the GLB");
				Assert.That(restoredMesh.bounds.size.magnitude, Is.GreaterThan(defaultBoundsMagnitude * 2f),
					"the packaged custom insert must not be replaced by the default generated triangle");
				Assert.That(new SerializedObject(restoredInsert).FindProperty("_usesPackagedMesh").boolValue, Is.True);
				restoredInsert.RebuildMesh();
				Assert.That(restoredInsert.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(restoredMesh));
				var restoredLight = restoredInsert.GetComponent<InsertLightComponent>();
				Assert.That(restoredLight.Intensity, Is.EqualTo(0.6f));
				Assert.That(restoredLight.ExtraBulbs[0].Intensity, Is.EqualTo(0.4f));
			} finally {
				if (source) { Object.DestroyImmediate(source); }
				if (imported) { Object.DestroyImmediate(imported); }
				if (body) { Object.DestroyImmediate(body); }
				if (cover) { Object.DestroyImmediate(cover); }
				if (staleBake) { Object.DestroyImmediate(staleBake); }
				if (customSource) { Object.DestroyImmediate(customSource); }
				File.Delete(packagePath);
			}
		}

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
				Object.DestroyImmediate(go);
				Assert.That(mesh != null, Is.True, "the imported mesh is not owned by the insert");
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
		public void TableInsertBrightnessRoundTrips()
		{
			var source = new GameObject("Table").AddComponent<TableComponent>();
			var target = new GameObject("Restored").AddComponent<TableComponent>();
			var legacy = new GameObject("Legacy").AddComponent<TableComponent>();
			try {
				source.InsertBrightness = 0.6f;

				target.Unpack(source.Pack());
				legacy.Unpack(System.Text.Encoding.UTF8.GetBytes("{\"GlobalDifficulty\":0.3}"));

				Assert.That(target.InsertBrightness, Is.EqualTo(0.6f));
				Assert.That(legacy.InsertBrightness, Is.EqualTo(1f), "a package from before the setting keeps the default");
				Assert.That(legacy.GlobalDifficulty, Is.EqualTo(0.3f));
			} finally {
				Object.DestroyImmediate(source.gameObject);
				Object.DestroyImmediate(target.gameObject);
				Object.DestroyImmediate(legacy.gameObject);
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
				insertLight.State = LampStatus.Blinking;
				insertLight.BlinkPattern = "1100";
				insertLight.BlinkInterval = 250;
				insertLight.FadeSpeedUp = 0.2f;
				insertLight.FadeSpeedDown = 0.4f;

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
				insertLight.State = LampStatus.Off;
				insertLight.BlinkPattern = "";
				insertLight.BlinkInterval = 0;
				insertLight.FadeSpeedUp = 0f;
				insertLight.FadeSpeedDown = 0f;

				insertLight.Unpack(data);
				insertLight.UnpackReferences(referenceData, go.transform, refs, null);

				Assert.That(insertLight.Intensity, Is.EqualTo(0.35f));
				Assert.That(insertLight.Color, Is.EqualTo(Color.cyan));
				Assert.That(insertLight.ExtraBulbs[0].Intensity, Is.EqualTo(0.7f));
				Assert.That(insertLight.BulbNames[1], Is.EqualTo("Middle"));
				Assert.That(insertLight.TintWithLamp, Is.True);
				Assert.That(insertLight.SpillLumens, Is.EqualTo(3.5f));
				Assert.That(insertLight.SpillLight, Is.SameAs(spill));
				Assert.That(insertLight.State, Is.EqualTo(LampStatus.Blinking));
				Assert.That(insertLight.BlinkPattern, Is.EqualTo("1100"));
				Assert.That(insertLight.BlinkInterval, Is.EqualTo(250));
				Assert.That(insertLight.FadeSpeedUp, Is.EqualTo(0.2f));
				Assert.That(insertLight.FadeSpeedDown, Is.EqualTo(0.4f));
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
				sourceBake.PlainStipple = true;
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
				Assert.That(target.Bake.PlainStipple, Is.True);
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
