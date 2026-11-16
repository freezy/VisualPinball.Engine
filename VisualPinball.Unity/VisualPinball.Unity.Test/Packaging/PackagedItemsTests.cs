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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class PackagedItemsTests
	{
		private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);
		private static string Text(byte[] bytes) => bytes == null ? null : Encoding.UTF8.GetString(bytes);

		[Test]
		public void WritesOneNodePerLineAndLeavesOutWhatIsEmpty()
		{
			var nodes = new List<PackagedItems.Node> {
				new() { Id = "n\"1", Item = Bytes("{\"name\":\"x\"}") },
				new() { Id = "n2" },
			};
			nodes[0].Components.Add(new PackagedItems.Entry { Type = "A", Data = Bytes("{\"a\":1}"), Refs = Bytes("{\"r\":[]}") });
			nodes[0].Components.Add(new PackagedItems.Entry { Type = "A" });
			nodes[0].Components.Add(new PackagedItems.Entry { Type = "B", Data = Array.Empty<byte>(), Refs = Bytes("[]") });

			var written = PackagedItems.Write(nodes);

			Assert.That(Text(written), Is.EqualTo(
				"{\"nodes\":[\n" +
				"{\"id\":\"n\\\"1\",\"item\":{\"name\":\"x\"},\"components\":[{\"type\":\"A\",\"data\":{\"a\":1},\"refs\":{\"r\":[]}},{\"type\":\"A\"},{\"type\":\"B\",\"refs\":[]}]},\n" +
				"{\"id\":\"n2\"}\n" +
				"]}"));
			// what a reader with a parser of its own sees
			Assert.That(JObject.Parse(Text(written))["nodes"]!.Count(), Is.EqualTo(2));

			var read = PackagedItems.Read(written);
			Assert.That(read.Select(node => node.Id), Is.EqualTo(new[] { "n\"1", "n2" }));
			Assert.That(Text(read[0].Item), Is.EqualTo("{\"name\":\"x\"}"));
			Assert.That(read[0].Components.Select(entry => entry.Type), Is.EqualTo(new[] { "A", "A", "B" }));
			Assert.That(Text(read[0].Components[0].Data), Is.EqualTo("{\"a\":1}"));
			Assert.That(Text(read[0].Components[0].Refs), Is.EqualTo("{\"r\":[]}"));
			Assert.That(read[0].Components[1].Data, Is.Null);
			Assert.That(read[0].Components[1].Refs, Is.Null);
			Assert.That(read[0].Components[2].Data, Is.Null);
			Assert.That(Text(read[0].Components[2].Refs), Is.EqualTo("[]"));
			Assert.That(read[1].Item, Is.Null);
			Assert.That(read[1].Components, Is.Empty);
			// and written again, the same bytes
			Assert.That(PackagedItems.Write(read), Is.EqualTo(written));

			Assert.That(PackagedItems.Read(PackagedItems.Write(new List<PackagedItems.Node>())), Is.Empty);
			Assert.Throws<InvalidOperationException>(() => PackagedItems.Write(new List<PackagedItems.Node> { new() { Id = "a" }, new() { Id = "a" } }));
			Assert.Throws<InvalidOperationException>(() => PackagedItems.Write(new List<PackagedItems.Node> { new() }));
		}

		[Test]
		public void ReadsWhatAnotherWriterMayProduce()
		{
			// white space, a byte order mark, keys in any order, keys this build doesn't know, escapes
			var read = PackagedItems.Read(Bytes(
				"\uFEFF { \"future\" : [1,{\"a\":\"]}\\\"\"}] ,\r\n\t\"nodes\" : [ { \"x\" : null , \"components\" : [ " +
				"{ \"refs\" : \"s\" , \"later\" : {\"deep\":[[]]} , \"data\" : 7 , \"type\" : \"T\\\\x\" } , { \"type\":\"U\", \"data\":null, \"refs\":[1,2,{\"k\":\"}]\"}] } ] , " +
				"\"id\" : \"a\\u0041\\n\" , \"item\" : null } , {\"id\":\"2026-01-02T03:04:05Z\"} ] , \"more\" : -1.5e+3 } \n"));

			Assert.That(read.Select(node => node.Id), Is.EqualTo(new[] { "aA\n", "2026-01-02T03:04:05Z" }));
			Assert.That(read[0].Item, Is.Null);
			Assert.That(read[0].Components.Select(entry => entry.Type), Is.EqualTo(new[] { "T\\x", "U" }));
			Assert.That(Text(read[0].Components[0].Data), Is.EqualTo("7"));
			Assert.That(Text(read[0].Components[0].Refs), Is.EqualTo("\"s\""));
			Assert.That(read[0].Components[1].Data, Is.Null);
			Assert.That(Text(read[0].Components[1].Refs), Is.EqualTo("[1,2,{\"k\":\"}]\"}]"));

			Assert.That(PackagedItems.Read(Bytes("{}")), Is.Empty);
			var deep = new string('[', 100) + new string(']', 100);
			Assert.That(Text(PackagedItems.Read(Bytes("{\"nodes\":[{\"id\":\"a\",\"item\":" + deep + "}]}"))[0].Item), Is.EqualTo(deep));
		}

		[TestCase("", TestName = "Refuses an empty file")]
		[TestCase("[]", TestName = "Refuses an array at the root")]
		[TestCase("{\"nodes\":{}}", TestName = "Refuses nodes that are no array")]
		[TestCase("{\"nodes\":[],\"nodes\":[]}", TestName = "Refuses nodes given twice")]
		[TestCase("{\"nodes\":[{}]}", TestName = "Refuses a node without id")]
		[TestCase("{\"nodes\":[{\"id\":\"\"}]}", TestName = "Refuses an empty id")]
		[TestCase("{\"nodes\":[{\"id\":5}]}", TestName = "Refuses an id that is no string")]
		[TestCase("{\"nodes\":[{\"id\":\"a\"},{\"id\":\"a\"}]}", TestName = "Refuses a node listed twice")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"id\":\"b\"}]}", TestName = "Refuses an id given twice")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"item\":{},\"item\":{}}]}", TestName = "Refuses an item given twice")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"components\":[],\"components\":[]}]}", TestName = "Refuses components given twice")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"components\":[{}]}]}", TestName = "Refuses a component without type")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"components\":[{\"type\":\"T\",\"data\":{},\"data\":{}}]}]}", TestName = "Refuses data given twice")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"components\":[{\"type\":\"T\",\"refs\":{},\"refs\":{}}]}]}", TestName = "Refuses refs given twice")]
		[TestCase("{\"nodes\":[{\"id\":\"a\"}]} x", TestName = "Refuses what follows the file's value")]
		[TestCase("{\"nodes\":[{\"id\":\"a\"},]}", TestName = "Refuses a trailing comma")]
		[TestCase("{\"nodes\":[{\"id\":\"a\" \"item\":{}}]}", TestName = "Refuses a missing comma")]
		[TestCase("{\"nodes\":[{\"id\" \"a\"}]}", TestName = "Refuses a missing colon")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"item\":{]}]}", TestName = "Refuses a bracket closing another kind")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"item\":}]}", TestName = "Refuses a missing value")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"x\":@}]}", TestName = "Refuses a value that is none")]
		[TestCase("{\"nodes\":[{\"id\":\"a\tb\"}]}", TestName = "Refuses a control character in a string")]
		[TestCase("{\"nodes\":[{\"id\":\"a\\q\"}]}", TestName = "Refuses an unknown escape")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"item\":{\"name\":\"x\"}", TestName = "Refuses a file cut short")]
		[TestCase("{\"nodes\":[{\"id\":\"a\",\"item\":{\"name\":\"x", TestName = "Refuses a file cut short in a string")]
		public void RefusesABrokenFile(string json)
		{
			Assert.Throws<InvalidDataException>(() => PackagedItems.Read(Bytes(json)));
		}

		[Test]
		public void RefusesAValueNestedDeeperThanItFollows()
		{
			var deep = new string('[', 129) + new string(']', 129);
			Assert.Throws<InvalidDataException>(() => PackagedItems.Read(Bytes("{\"nodes\":[{\"id\":\"a\",\"item\":" + deep + "}]}")));
		}

		[Test]
		public void AcceptsOnlyPayloadsThatCannotBreakTheFile()
		{
			foreach (var payload in new[] {
				"{}", "[]", "1", "\"s\"", "null", "true", " {\"a\":[1,2,{\"b\":\"}\"}]} ", "{\"\":{}}",
				"[0,-0,-0.5,1E-05,1.5e+10,12.25E-3,true,false,null]", "\"\\\"\\\\\\/\\b\\f\\n\\r\\t\\u00e9 ü€𝄞 // not a comment\"",
				new string('[', 64) + new string(']', 64),
			}) {
				Assert.DoesNotThrow(() => PackagedItems.ValidatePayload(Bytes(payload)), payload);
			}
			foreach (var payload in new[] { "", "{", "{}x", "{} {}", "{\"a\":}", "\uFEFF{}", "{\"a\":[}", "{\"a\":\"b}", new string('[', 65) + new string(']', 65),
				// what a lenient parser takes and JSON doesn't: each could end a value somewhere else for another reader
				"{\"x\":1//}", "{\"x\":1}//", "/**/{}", "{\"x\":/*}*/1}", "{'a':1}", "{\"a\":'}'}", "{a:1}", "[1,]", "{\"a\":1,}", "[,1]",
				"01", "1.", ".5", "+1", "1e", "0x10", "NaN", "Infinity", "-", "undefined", "True", "nul", "\"\\x\"", "\"\\u12\"", "\"a\tb\"", "new Date(1)",
			}) {
				Assert.Throws<InvalidDataException>(() => PackagedItems.ValidatePayload(Bytes(payload)), payload);
			}
			Assert.Throws<InvalidDataException>(() => PackagedItems.ValidatePayload(new byte[] { (byte)'"', 0xFF, (byte)'"' }), "not UTF-8");
		}

		[Test]
		public void FindsTheComponentOfExactlyTheTypeAnEntryNames()
		{
			var go = new GameObject("Sounds");
			go.SetActive(false);
			try {
				// derived from SoundComponent, and found by GetComponents(typeof(SoundComponent)) as well
				var derived = go.AddComponent<InputActionSoundComponent>();
				var first = go.AddComponent<SoundComponent>();
				var second = go.AddComponent<SoundComponent>();

				Assert.That(PackagedItems.FindComponent(go, typeof(SoundComponent), 0), Is.SameAs(first));
				Assert.That(PackagedItems.FindComponent(go, typeof(SoundComponent), 1), Is.SameAs(second));
				Assert.That(PackagedItems.FindComponent(go, typeof(SoundComponent), 2), Is.Null);
				Assert.That(PackagedItems.FindComponent(go, typeof(InputActionSoundComponent), 0), Is.SameAs(derived));
				Assert.That(PackagedItems.FindComponent(go, typeof(InputActionSoundComponent), 1), Is.Null);
			} finally {
				Object.DestroyImmediate(go);
			}
		}

		[UnityTest]
		public IEnumerator ReferencesGoToTheComponentTheyWereWrittenFor()
		{
			var name = "PackagedItemsTest-" + Guid.NewGuid().ToString("N");
			var assetFolder = "Assets/__" + name;
			var package = Path.Combine(Path.GetTempPath(), name + ".vpe");
			var source = new GameObject("Table");
			GameObject runtime = null;
			GameObject imported = null;
			try {
				Directory.CreateDirectory(assetFolder);
				var wav = assetFolder + "/click.wav";
				using (var writer = new BinaryWriter(File.Create(wav))) {
					writer.Write(Encoding.ASCII.GetBytes("RIFF"));
					writer.Write(38);
					writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
					writer.Write(16);
					writer.Write((short)1);
					writer.Write((short)1);
					writer.Write(44100);
					writer.Write(88200);
					writer.Write((short)2);
					writer.Write((short)16);
					writer.Write(Encoding.ASCII.GetBytes("data"));
					writer.Write(2);
					writer.Write((short)1000);
				}
				AssetDatabase.ImportAsset(wav);
				var bank = ScriptableObject.CreateInstance<SoundEffectAsset>();
				bank.Clips = new[] { AssetDatabase.LoadAssetAtPath<AudioClip>(wav) };
				bank.BusId = SoundBuses.Transients;
				AssetDatabase.CreateAsset(bank, assetFolder + "/bank.asset");

				var table = source.AddComponent<TableComponent>();
				// The GLB asset's file name differs from the table name, which a descendant also uses.
				var child = new GameObject(source.name);
				child.transform.SetParent(source.transform, false);
				// Two components of one type. Only the second one has a reference, which a count of the references
				// written would hand to the first.
				var silent = child.AddComponent<InputActionSoundComponent>();
				silent.ActionName = "First";
				var sounding = child.AddComponent<InputActionSoundComponent>();
				sounding.ActionName = "Second";
				sounding.SoundAsset = bank;

				new PackageWriter(source).WritePackageSync(package);

				// the package: one file for all nodes, and no entry that is only a folder
				using (var zip = new ZipArchive(File.OpenRead(package), ZipArchiveMode.Read)) {
					var names = zip.Entries.Select(entry => entry.FullName).ToList();
					Assert.That(names, Has.Member("table/items.json"));
					Assert.That(names.Where(entry => entry.EndsWith("/")), Is.Empty);
					Assert.That(names.Where(entry => entry.StartsWith("table/items/") || entry.StartsWith("table/refs/")), Is.Empty);
				}
				using (var storage = PackageApi.StorageManager.OpenStorage(package)) {
					var nodes = PackagedItems.Read(storage.GetFolder(PackageApi.TableFolder));
					var entries = nodes.Single(node => node.Components.Any(entry => entry.Type == "InputActionSound")).Components;
					Assert.That(entries.Select(entry => entry.Type), Is.EqualTo(new[] { "InputActionSound", "InputActionSound" }));
					Assert.That(entries[0].Refs, Is.Null);
					Assert.That(entries[1].Refs, Is.Not.Null);
					// the table root comes before its children
					Assert.That(nodes[0].Components.Select(entry => entry.Type), Has.Member("Table"));
				}

				var load = new RuntimePackageReader(package).ImportIntoScene();
				while (!load.IsCompleted) yield return null;
				runtime = load.GetAwaiter().GetResult();
				AssertRestored(runtime, "runtime");

				var import = new PackageReader(package).ImportIntoScene(name);
				while (!import.IsCompleted) yield return null;
				import.GetAwaiter().GetResult();
				imported = Object.FindObjectsByType<TableComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
					.Single(t => t != table && t.gameObject != runtime).gameObject;
				Assert.That(imported.name, Is.EqualTo(source.name));
				AssertRestored(imported, "editor");
			} finally {
				if (runtime) Object.DestroyImmediate(runtime);
				if (imported) Object.DestroyImmediate(imported);
				Object.DestroyImmediate(source);
				AssetDatabase.DeleteAsset(assetFolder);
				AssetDatabase.DeleteAsset("Assets/Resources/" + name);
				if (File.Exists(package)) File.Delete(package);
			}
		}

		[UnityTest]
		public IEnumerator PrefabInstancesAreLinkedToTheirPrefabAgain()
		{
			var name = "PackagedItemsTest-" + Guid.NewGuid().ToString("N");
			var assetFolder = "Assets/__" + name;
			var package = Path.Combine(Path.GetTempPath(), name + ".vpe");
			var exportedAgain = Path.Combine(Path.GetTempPath(), name + "-again.vpe");
			var source = new GameObject(name);
			var prefabRoot = new GameObject("Assembly");
			GameObject imported = null;
			try {
				AssetDatabase.CreateFolder("Assets", "__" + name);
				// two children of the same name, which the glTF import makes unique, and one the instance renames
				AddChild(prefabRoot, "Screw");
				AddChild(prefabRoot, "Screw");
				// a light, which glTF exports and imports on a helper child
				AddChild(AddChild(prefabRoot, "Lamp"), "Source").AddComponent<Light>();
				// an inactive child, which the export leaves out
				AddChild(prefabRoot, "Decal").SetActive(false);
				var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetFolder + "/Assembly.prefab");

				source.AddComponent<TableComponent>();
				var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, source.transform);
				instance.name = "Bumper 1";
				var secondScrew = instance.transform.GetChild(1);
				secondScrew.localPosition = new Vector3(0.1f, 0, 0);
				var lamp = instance.transform.GetChild(2);
				lamp.name = "Bumper 1 Lamp";
				lamp.gameObject.AddComponent<InputActionSoundComponent>().ActionName = "Lamp";
				// a light the instance adds, which the prefab doesn't have
				instance.transform.GetChild(0).gameObject.AddComponent<Light>();
				// an added game object with the name of the prefab's renamed one
				AddChild(instance, "Lamp").AddComponent<InputActionSoundComponent>().ActionName = "Added";
				// and an added instance of the same prefab, whose game objects have the same file ids as the outer's
				var nested = (GameObject)PrefabUtility.InstantiatePrefab(prefab, instance.transform);
				nested.name = "Nested";
				nested.transform.GetChild(1).localPosition = new Vector3(0.2f, 0, 0);
				PrefabUtility.RecordPrefabInstancePropertyModifications(nested);
				PrefabUtility.RecordPrefabInstancePropertyModifications(nested.transform.GetChild(1));
				PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
				PrefabUtility.RecordPrefabInstancePropertyModifications(secondScrew);
				PrefabUtility.RecordPrefabInstancePropertyModifications(lamp.gameObject);

				new PackageWriter(source).WritePackageSync(package);

				// only the root of the instance names the prefab, the game objects below say which of its game objects they are
				using (var storage = PackageApi.StorageManager.OpenStorage(package)) {
					var items = PackagedItems.Read(storage.GetFolder(PackageApi.TableFolder))
						.Where(node => node.Item != null)
						.Select(node => ItemPackable.Unpack(node.Item))
						.ToList();
					Assert.That(items.Where(item => !string.IsNullOrEmpty(item.PrefabGuid)).Select(item => item.Name), Is.EqualTo(new[] { "Bumper 1", "Nested" }));
					Assert.That(items.Where(item => item.PrefabObject != 0).Select(item => item.Name),
						Is.EquivalentTo(new[] { "Screw", "Screw", "Bumper 1 Lamp", "Source", "Screw", "Screw", "Lamp", "Source" }));
				}

				var import = new PackageReader(package).ImportIntoScene(name);
				while (!import.IsCompleted) yield return null;
				import.GetAwaiter().GetResult();
				imported = Object.FindObjectsByType<TableComponent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
					.Single(table => table.gameObject != source).gameObject;

				var bumper = imported.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Bumper 1").gameObject;
				Assert.That(PrefabUtility.IsOutermostPrefabInstanceRoot(bumper), Is.True, "the instance is linked to its prefab");
				Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(bumper), Is.SameAs(prefab));
				Assert.That(PrefabUtility.GetAddedGameObjects(bumper).Select(added => added.instanceGameObject.name).Where(name => !name.EndsWith("_Orientation")),
					Is.EquivalentTo(new[] { "Lamp", "Nested" }),
					"the game objects of the prefab were matched with the prefab's, the added ones stay added");
				// the instances of the prefab's game objects, in its order (Unity keeps the added ones apart)
				var linked = bumper.transform.Cast<Transform>().Where(t => !PrefabUtility.IsAddedGameObjectOverride(t.gameObject)).ToList();
				Assert.That(linked.Select(t => t.name), Is.EqualTo(new[] { "Screw", "Screw", "Bumper 1 Lamp", "Decal" }));
				Assert.That(linked[3].gameObject.activeSelf, Is.False);
				Assert.That(linked[0].GetComponentsInChildren<Light>(true), Has.Length.EqualTo(1), "the light the instance added");
				Assert.That(linked[1].localPosition.x, Is.EqualTo(0.1f).Within(1e-5f));
				var importedLamp = linked[2];
				Assert.That(importedLamp.GetComponent<InputActionSoundComponent>()?.ActionName, Is.EqualTo("Lamp"));
				Assert.That(importedLamp.Cast<Transform>().Select(t => t.name), Is.EqualTo(new[] { "Source" }));
				Assert.That(importedLamp.GetComponentsInChildren<Light>(true), Has.Length.EqualTo(1), "the prefab's light, and not the glTF import's too");
				var addedLamp = bumper.transform.Cast<Transform>().Single(t => t.name == "Lamp");
				Assert.That(addedLamp.GetComponent<InputActionSoundComponent>()?.ActionName, Is.EqualTo("Added"));

				var importedNested = bumper.transform.Cast<Transform>().Single(t => t.name == "Nested").gameObject;
				Assert.That(PrefabUtility.IsAnyPrefabInstanceRoot(importedNested), Is.True, "the added instance is linked to its prefab");
				Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(importedNested), Is.SameAs(prefab));
				Assert.That(PrefabUtility.GetAddedGameObjects(importedNested), Is.Empty);
				Assert.That(PrefabUtility.GetRemovedGameObjects(importedNested), Is.Empty);
				Assert.That(importedNested.transform.Cast<Transform>().Select(t => t.name), Is.EqualTo(new[] { "Screw", "Screw", "Lamp", "Decal" }));
				Assert.That(importedNested.transform.GetChild(1).localPosition.x, Is.EqualTo(0.2f).Within(1e-5f));
				Assert.That(linked[1].localPosition.x, Is.EqualTo(0.1f).Within(1e-5f), "the nested instance's moved screw is its own");

				// and the imported table exports again
				new PackageWriter(imported).WritePackageSync(exportedAgain);
			} finally {
				if (imported) Object.DestroyImmediate(imported);
				Object.DestroyImmediate(source);
				Object.DestroyImmediate(prefabRoot);
				AssetDatabase.DeleteAsset(assetFolder);
				AssetDatabase.DeleteAsset("Assets/Resources/" + name);
				if (File.Exists(package)) File.Delete(package);
				if (File.Exists(exportedAgain)) File.Delete(exportedAgain);
			}
		}

		private static GameObject AddChild(GameObject parent, string name)
		{
			var child = new GameObject(name);
			child.transform.SetParent(parent.transform, false);
			return child;
		}

		private static void AssertRestored(GameObject table, string reader)
		{
			var sounds = table.GetComponentsInChildren<InputActionSoundComponent>(true);
			Assert.That(sounds.Select(sound => sound.ActionName), Is.EqualTo(new[] { "First", "Second" }), reader);
			Assert.That(sounds[0].gameObject, Is.SameAs(sounds[1].gameObject), reader);
			Assert.That(sounds[0].SoundAsset, Is.Null, reader + ": the component without a reference");
			Assert.That(sounds[1].SoundAsset, Is.Not.Null, reader + ": the component with the reference");
		}
	}
}
