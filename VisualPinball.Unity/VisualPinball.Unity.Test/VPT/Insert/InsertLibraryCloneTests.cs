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
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class InsertLibraryCloneTests
	{
		private const string Root = "Assets/Clone Library Test";
		private const string Source = Root + "/Inserts/Insert, Source";

		[TestCase("Insert, Square, 1in", "Insert, Big")]
		[TestCase("Insert, Square, 1in, Bake", "Insert, Big, Bake")]
		[TestCase("Insert, Square, 1in Hybrid", "Insert, Big Hybrid")]
		[TestCase("Insert, Square, 1in, Starburst (Red)", "Insert, Big (Red)")]
		[TestCase("Insert, Square, 1in, Starburst, Red", "Insert, Big, Red")]
		[TestCase("Something Else", "Insert, Big, Something Else")]
		public void ShouldRenameAClonesFiles(string fileName, string expected)
		{
			var stems = new[] { "Insert, Square, 1in", "Insert, Square, 1in, Starburst" };
			Assert.That(InsertLibraryClone.Rename(fileName, stems, "Insert, Big"), Is.EqualTo(expected));
		}

		[Test]
		public void ShouldCloneALibraryInsertWithItsOwnBodyAndBake()
		{
			var forget = typeof(InsertBaker).GetMethod("ForgetLibraries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
			AssetDatabase.CreateFolder("Assets", "Clone Library Test");
			AssetDatabase.CreateFolder(Root, "Inserts");
			AssetDatabase.CreateFolder(Root + "/Inserts", "Insert, Source");
			AssetDatabase.CreateFolder(Root, "Database");
			try {
				var library = ScriptableObject.CreateInstance<AssetLibrary>();
				library.LibraryRoot = Root;
				library.DatabaseRoot = Root + "/Database";
				library.ThumbnailRoot = Root + "/Thumbnails";
				AssetDatabase.CreateAsset(library, Root + "/Library.asset");
				AssetDatabase.SaveAssets();
				var category = library.AddCategory("Inserts");
				forget.Invoke(null, null);

				// The source: a body with a color variant, the maps it carries, a bake, a shared cover.
				var texture = new Texture2D(4, 4);
				File.WriteAllBytes(Path.GetFullPath(Source + "/Insert, Source, Normal Map.png"), texture.EncodeToPNG());
				Object.DestroyImmediate(texture);
				AssetDatabase.ImportAsset(Source + "/Insert, Source, Normal Map.png");
				var map = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Insert, Source, Normal Map.png");
				var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
				var body = new Material(shader);
				body.SetTexture("_NormalMap", map);
				AssetDatabase.CreateAsset(body, Source + "/Insert, Source, Starburst.mat");
				var red = new Material(body) { parent = body };
				AssetDatabase.CreateAsset(red, Source + "/Insert, Source, Starburst, Red.mat");
				var cover = new Material(shader);
				AssetDatabase.CreateAsset(cover, Root + "/Inserts/Insert Cover, Red.mat");
				// What it wore before its bake belongs to another insert, as it does when the source is a clone itself.
				AssetDatabase.CreateFolder(Root + "/Inserts", "Insert, Other");
				var other = new Material(shader);
				AssetDatabase.CreateAsset(other, Root + "/Inserts/Insert, Other/Insert, Other.mat");
				var bake = ScriptableObject.CreateInstance<InsertBakeData>();
				bake.NormalMap = map;
				bake.Material = red;
				AssetDatabase.CreateAsset(bake, Source + "/Insert, Source, Bake.asset");

				var go = new GameObject("Insert, Source");
				var insert = go.AddComponent<InsertComponent>();
				var sourceId = insert.EnsureId();
				// Made by another insert, as some of the library's are.
				bake.Owner = "0123456789abcdef0123456789abcdef";
				EditorUtility.SetDirty(bake);
				var serialized = new SerializedObject(insert);
				serialized.FindProperty("_bake").objectReferenceValue = bake;
				serialized.FindProperty("_coverMaterial").objectReferenceValue = cover;
				serialized.FindProperty("_unbakedMaterial").objectReferenceValue = other;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				go.GetComponent<MeshRenderer>().sharedMaterials = new[] { red, cover };
				var sourcePrefab = PrefabUtility.SaveAsPrefabAsset(go, Source + "/Insert, Source.prefab");
				Object.DestroyImmediate(go);
				AssetDatabase.SaveAssets();

				library.AddAsset(sourcePrefab, category);
				var sourceAsset = library.GetAsset(AssetDatabase.AssetPathToGUID(Source + "/Insert, Source.prefab"));
				sourceAsset.Tags = new List<AssetTag> { new("Square") };
				sourceAsset.MaterialVariations = new List<AssetMaterialVariation> {
					new() { Name = "Body", Target = new AssetMaterialTarget { Object = sourcePrefab, Slot = 0 }, Overrides = new List<AssetMaterialOverride> { new() { Name = "Red", Material = red, Id = "body-red" } } },
					new() { Name = "Cover", Target = new AssetMaterialTarget { Object = sourcePrefab, Slot = 1 }, Overrides = new List<AssetMaterialOverride> { new() { Name = "Red", Material = cover, Id = "cover-red" } } },
				};
				sourceAsset.Save();

				Assert.That(InsertLibraryClone.CannotClone(Source + "/Insert, Source.prefab"), Is.Null);
				Assert.That(InsertLibraryClone.CannotName(Source + "/Insert, Source.prefab", "Insert, Source"), Is.Not.Null, "not under the source's name");

				var clone = InsertLibraryClone.Clone(Source + "/Insert, Source.prefab", "Insert, Clone");

				const string folder = Root + "/Inserts/Insert, Clone";
				Assert.That(AssetDatabase.GetAssetPath(clone), Is.EqualTo(folder + "/Insert, Clone.prefab"));
				Assert.That(clone.name, Is.EqualTo("Insert, Clone"));
				var cloned = clone.GetComponent<InsertComponent>();
				Assert.That(cloned.Id, Is.Not.Empty.And.Not.EqualTo(sourceId), "an insert of its own");

				var clonedBake = cloned.Bake;
				Assert.That(AssetDatabase.GetAssetPath(clonedBake), Is.EqualTo(folder + "/Insert, Clone, Bake.asset"));
				Assert.That(clonedBake.name, Is.EqualTo("Insert, Clone, Bake"));
				Assert.That(clonedBake.Owner, Is.EqualTo(cloned.Id), "which owns its bake, so baking it updates it");
				Assert.That(AssetDatabase.GetAssetPath(clonedBake.NormalMap), Is.EqualTo(folder + "/Insert, Clone, Normal Map.png"));

				var clonedRed = clone.GetComponent<MeshRenderer>().sharedMaterials[0];
				Assert.That(AssetDatabase.GetAssetPath(clonedRed), Is.EqualTo(folder + "/Insert, Clone, Red.mat"), "the body's colors are named after the clone");
				Assert.That(clonedRed.name, Is.EqualTo("Insert, Clone, Red"));
				Assert.That(clonedBake.Material, Is.SameAs(clonedRed));
				Assert.That(AssetDatabase.GetAssetPath(clonedRed.parent), Is.EqualTo(folder + "/Insert, Clone.mat"), "and follow the clone's body");
				Assert.That(clonedRed.parent.GetTexture("_NormalMap"), Is.SameAs(clonedBake.NormalMap), "which carries its maps");
				const string coverPath = Root + "/Inserts/Insert Cover, Red.mat";
				Assert.That(AssetDatabase.GetAssetPath(clone.GetComponent<MeshRenderer>().sharedMaterials[1]), Is.EqualTo(coverPath), "the cover stays the library's");
				Assert.That(AssetDatabase.GetAssetPath(cloned.CoverMaterial), Is.EqualTo(coverPath));
				Assert.That(cloned.PreBakeMaterial, Is.Null, "not another insert's material from before the bake");

				var red2 = AssetDatabase.LoadAssetAtPath<Material>(Source + "/Insert, Source, Starburst, Red.mat");
				Assert.That(AssetDatabase.LoadAssetAtPath<InsertBakeData>(Source + "/Insert, Source, Bake.asset").Owner, Is.EqualTo("0123456789abcdef0123456789abcdef"), "the source is left as it was");
				Assert.That(AssetDatabase.GetAssetPath(red2.parent), Is.EqualTo(Source + "/Insert, Source, Starburst.mat"));
				Assert.That(AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<GameObject>(Source + "/Insert, Source.prefab").GetComponent<InsertComponent>().PreBakeMaterial), Is.EqualTo(Root + "/Inserts/Insert, Other/Insert, Other.mat"));

				var asset = library.GetAsset(AssetDatabase.AssetPathToGUID(folder + "/Insert, Clone.prefab"));
				Assert.That(asset, Is.Not.Null, "added to the library");
				Assert.That(asset.IsOfCategory(category), Is.True);
				Assert.That(asset.Tags.Select(tag => tag.TagName), Is.EquivalentTo(new[] { "Square" }));
				Assert.That(asset.MaterialVariations[0].Overrides[0].Material, Is.SameAs(clonedRed), "its body variation is the clone's colors");
				Assert.That(AssetDatabase.GetAssetPath(asset.MaterialVariations[1].Overrides[0].Material), Is.EqualTo(coverPath), "its cover variation the shared covers");
				Assert.That(asset.MaterialVariations[0].Target.Object, Is.SameAs(clone));

				Assert.That(InsertLibraryClone.CannotName(Source + "/Insert, Source.prefab", "Insert, Clone"), Is.Not.Null, "a name that's taken");

				library.IsLocked = true;
				Assert.That(InsertLibraryClone.CannotClone(Source + "/Insert, Source.prefab"), Is.Not.Null, "not from a locked library");
			} finally {
				AssetDatabase.DeleteAsset(Root);
				forget.Invoke(null, null);
			}
		}
	}
}
