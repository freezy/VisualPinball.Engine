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

// ReSharper disable InconsistentNaming

using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RollingSoundPackagingTests
	{
		[UnityTest]
		public IEnumerator RuntimeRestoresSharedGraphUnspawnedBallRowsAndMixedTroughTags()
		{
			var folder = "Assets/__RollingTest_" + Guid.NewGuid().ToString("N");
			var package = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".vpe");
			var source = new GameObject("Rolling table"); var steelPrefab = new GameObject("Steel ball"); var ceramicPrefab = new GameObject("Ceramic ball");
			GameObject restored = null;
			var steel = ScriptableObject.CreateInstance<BallSoundMaterialAsset>(); steel.name = "Steel";
			var ceramic = ScriptableObject.CreateInstance<BallSoundMaterialAsset>(); ceramic.name = "Ceramic";
			var surface = ScriptableObject.CreateInstance<RollingSoundMaterialAsset>(); surface.name = "Wood";
			var physical = ScriptableObject.CreateInstance<PhysicsMaterialAsset>(); physical.name = "Physical wood"; physical.Elasticity = .52f; physical.Friction = .13f; physical.ElasticityOverVelocity = new AnimationCurve(); physical.FrictionOverVelocity = new AnimationCurve();
			try {
				Directory.CreateDirectory(folder);
				var wavPath = folder + "/loop.wav";
				using (var writer = new BinaryWriter(File.Create(wavPath))) {
					writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + 882); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200); writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(882); for (var i = 0; i < 441; i++) writer.Write((short)(i % 30 * 100));
				}
				AssetDatabase.ImportAsset(wavPath);
				var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(wavPath);
				var banks = new SoundEffectAsset[3];
				for (var i = 0; i < banks.Length; i++) {
					banks[i] = ScriptableObject.CreateInstance<SoundEffectAsset>(); banks[i].Clips = new[] { clip };
					var so = new SerializedObject(banks[i]); so.FindProperty("_loop").boolValue = i < 2; so.ApplyModifiedPropertiesWithoutUndo();
					AssetDatabase.CreateAsset(banks[i], folder + "/bank" + i + ".asset");
				}
				surface.Responses = new[] { new RollingSoundResponse { BallMaterial = steel, Loop = banks[0], Enter = banks[2] }, new RollingSoundResponse { BallMaterial = ceramic, Loop = banks[1], Exit = banks[2] } };
				physical.RollingSoundMaterial = surface;
				source.AddComponent<TableComponent>();
				var controller = source.AddComponent<RollingSoundComponent>(); controller.DefaultBallMaterial = steel; controller.Volume = .8f;
				var child = new GameObject("Support"); child.transform.SetParent(source.transform, false);
				var sound = child.AddComponent<RollingSoundSurfaceComponent>(); sound.Mode = RollingSurfaceMode.Override; sound.Material = surface;
				child.AddComponent<PrimitiveComponent>(); var collider = child.AddComponent<PrimitiveColliderComponent>(); collider.PhysicsMaterial = physical; collider.OverwritePhysics = true;
				steelPrefab.AddComponent<BallComponent>().SoundMaterial = steel; ceramicPrefab.AddComponent<BallComponent>().SoundMaterial = ceramic;
				var troughObject = new GameObject("Trough"); troughObject.transform.SetParent(source.transform, false); var trough = troughObject.AddComponent<TroughComponent>(); trough.BallPrefabs = new[] { steelPrefab, ceramicPrefab, steelPrefab };
				new PackageWriter(source).WritePackageSync(package);
				using (var archive = PackageApi.StorageManager.OpenStorage(package)) {
					Assert.That(archive.GetFolder("table").TryGetFolder("rolling", out _), Is.True);
					var assets = archive.GetFolder("table").GetFolder("assets");
					Assert.That(assets.TryGetFolder("BallSoundMaterial", out _), Is.False, "semantic assets must be invisible to older generic asset readers");
				}
				// Break every authoring reference before import: the graph has to stand on its own.
				sound.Material = null; controller.DefaultBallMaterial = null; physical.RollingSoundMaterial = null; surface.Responses = Array.Empty<RollingSoundResponse>();
				var task = new RuntimePackageReader(package).ImportIntoScene(); while (!task.IsCompleted) yield return null;
				if (task.IsFaulted) throw task.Exception.GetBaseException(); restored = task.Result;
				var restoredController = restored.GetComponentInChildren<RollingSoundComponent>(true);
				var restoredSurface = restored.GetComponentInChildren<RollingSoundSurfaceComponent>(true).Material;
				Assert.That(restoredSurface, Is.Not.SameAs(surface)); Assert.That(restoredSurface.Responses, Has.Length.EqualTo(2));
				Assert.That(restoredSurface.Responses[0].BallMaterial, Is.SameAs(restoredController.DefaultBallMaterial));
				Assert.That(restoredSurface.Responses[0].Loop, Is.Not.SameAs(restoredSurface.Responses[1].Loop));
				Assert.That(restoredSurface.Responses[0].Enter, Is.SameAs(restoredSurface.Responses[1].Exit));
				Assert.That(restoredSurface.Responses.All(row => row.Loop && row.Loop.Loop && row.Loop.Clips.Length == 1 && row.Loop.Clips[0]), Is.True);
				var restoredPhysical = restored.GetComponentInChildren<PrimitiveColliderComponent>(true).PhysicsMaterial;
				Assert.That(restoredPhysical.RollingSoundMaterial, Is.SameAs(restoredSurface)); Assert.That(restoredPhysical.Elasticity, Is.EqualTo(.52f)); Assert.That(restoredPhysical.Friction, Is.EqualTo(.13f));
				var templates = restored.GetComponentInChildren<TroughComponent>(true).BallPrefabs;
				Assert.That(templates[0].GetComponent<BallComponent>().SoundMaterial, Is.SameAs(restoredController.DefaultBallMaterial));
				Assert.That(templates[1].GetComponent<BallComponent>().SoundMaterial, Is.SameAs(restoredSurface.Responses[1].BallMaterial));
				Assert.That(templates[0], Is.SameAs(templates[2]));
				var editorFolder = folder + "/EditorGraph"; Directory.CreateDirectory(editorFolder);
				using (var storage = PackageApi.StorageManager.OpenStorage(package)) {
					var editorFiles = new PackagedFiles(storage.GetFolder("table"), new PackagedRefs(source.transform));
					editorFiles.UnpackSounds(editorFolder, VpeIntegrity.ReadManifest(storage)); editorFiles.UnpackAssets(editorFolder); editorFiles.Rolling.Read(editorFolder);
				}
				var physicalPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:PhysicsMaterialAsset", new[] { editorFolder }).Single());
				Assert.That(File.ReadAllText(physicalPath), Does.Contain("RollingSoundMaterial: {fileID: 11400000, guid:"));
				var editorPhysical = AssetDatabase.LoadAssetAtPath<PhysicsMaterialAsset>(physicalPath); Resources.UnloadAsset(editorPhysical); AssetDatabase.ImportAsset(physicalPath, ImportAssetOptions.ForceUpdate);
				var reloaded = AssetDatabase.LoadAssetAtPath<PhysicsMaterialAsset>(physicalPath);
				Assert.That(reloaded.RollingSoundMaterial, Is.Not.Null); Assert.That(reloaded.RollingSoundMaterial.Responses, Has.Length.EqualTo(2));
				Assert.That(reloaded.RollingSoundMaterial.Responses[1].BallMaterial.name, Is.EqualTo("Ceramic"));
			} finally {
				if (restored) Object.DestroyImmediate(restored); Object.DestroyImmediate(source); Object.DestroyImmediate(steelPrefab); Object.DestroyImmediate(ceramicPrefab);
				Object.DestroyImmediate(steel); Object.DestroyImmediate(ceramic); Object.DestroyImmediate(surface); Object.DestroyImmediate(physical); AssetDatabase.DeleteAsset(folder); File.Delete(package);
			}
		}
	}
}
