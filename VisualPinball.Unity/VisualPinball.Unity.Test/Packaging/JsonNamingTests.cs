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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class JsonNamingTests
	{
		private static readonly Regex CamelCase = new("^[a-z][a-zA-Z0-9]*$");

		[Test]
		public void EveryKeyOfEveryModelIsCamelCase()
		{
			var defaultResolver = new DefaultContractResolver();
			var wrong = new List<string>();
			var checkedTypes = 0;
			foreach (var type in ModelTypes()) {
				try {
					defaultResolver.ResolveContract(type);
				} catch (Exception) {
					continue; // not something Json.NET can write in the first place
				}
				JsonContract contract;
				try {
					contract = VpeJson.Resolver.ResolveContract(type);
				} catch (Exception e) {
					wrong.Add($"{type.FullName}: {e.Message}");
					continue;
				}
				checkedTypes++;
				if (contract is not JsonObjectContract objectContract) {
					continue;
				}
				foreach (var property in objectContract.Properties.Where(p => !p.Ignored)) {
					if (!CamelCase.IsMatch(property.PropertyName)) {
						wrong.Add($"{type.FullName}.{property.UnderlyingName} is written as \"{property.PropertyName}\"");
					}
				}
			}
			Assert.That(checkedTypes, Is.GreaterThan(100));
			Assert.That(wrong, Is.Empty, string.Join("\n", wrong));
		}

		[Test]
		public void APrivateFieldIsWrittenWithoutItsUnderscoreAndReadBack()
		{
			var asset = ScriptableObject.CreateInstance<SoundEffectAsset>();
			var restored = ScriptableObject.CreateInstance<SoundEffectAsset>();
			try {
				Set(asset, "_loop", true);
				Set(asset, "_cooldown", 0.05f);
				Set(asset, "_volumeRange", new Vector2(0.5f, 0.8f));

				var bytes = PackageApi.Packer.Pack(asset);
				var json = JObject.Parse(Encoding.UTF8.GetString(bytes));
				Assert.That(json["loop"]?.Value<bool>(), Is.True);
				Assert.That(json["cooldown"]?.Value<float>(), Is.EqualTo(0.05f));
				Assert.That(json["volumeRange"]?["x"]?.Value<float>(), Is.EqualTo(0.5f));
				Assert.That(json.Properties().Select(p => p.Name), Has.None.StartsWith("_"));
				// what a vector or the asset computes from its fields isn't written
				Assert.That(json["volumeRange"]?["magnitude"], Is.Null);

				PackageApi.Packer.Unpack(bytes, restored);
				Assert.That(restored.Loop, Is.True);
				Assert.That(Get<float>(restored, "_cooldown"), Is.EqualTo(0.05f));
				Assert.That(Get<Vector2>(restored, "_volumeRange"), Is.EqualTo(new Vector2(0.5f, 0.8f)));
			} finally {
				UnityEngine.Object.DestroyImmediate(asset);
				UnityEngine.Object.DestroyImmediate(restored);
			}
		}

		[Test]
		public void TheManifestIsCamelCaseAndKeepsItsSchemaNames()
		{
			var manifest = new VpePackageManifest { WrittenBy = "test", RootNodeId = "root" };
			manifest.Schemas[VpePackageSchemas.Items] = VpePackageSchemas.ItemsVersion;
			manifest.Schemas[VpePackageSchemas.ItemsReader] = VpePackageSchemas.ItemsReaderVersion;
			var json = JObject.Parse(Encoding.UTF8.GetString(PackageApi.Packer.Pack(manifest)));

			Assert.That(json.Properties().Select(p => p.Name), Is.SupersetOf(new[] { "formatVersion", "writtenBy", "rootNodeId", "schemas", "componentTypes" }));
			Assert.That(json["schemas"]?["itemsReader"]?.Value<int>(), Is.EqualTo(VpePackageSchemas.ItemsReaderVersion));
			Assert.That(PackageApi.Packer.Unpack<VpePackageManifest>(PackageApi.Packer.Pack(manifest)).Schemas, Is.EquivalentTo(manifest.Schemas));
		}

		[Test]
		public void TheIntegrityManifestIsCamelCaseAndStrict()
		{
			var manifest = new VpeIntegrity.Manifest { Entries = { new VpeIntegrity.Entry { Path = "a.json", Length = 1, Sha256 = new string('0', 64) } } };
			var text = Encoding.UTF8.GetString(VpeIntegrity.Json(manifest));
			Assert.That(text, Is.EqualTo("{\"version\":1,\"entries\":[{\"path\":\"a.json\",\"length\":1,\"sha256\":\"" + new string('0', 64) + "\"}]}"));
			Assert.That(VpeIntegrity.Parse<VpeIntegrity.Manifest>(Encoding.UTF8.GetBytes(text)).Entries.Single().Path, Is.EqualTo("a.json"));
			Assert.Throws<JsonSerializationException>(() => VpeIntegrity.Parse<VpeIntegrity.Manifest>(Encoding.UTF8.GetBytes("{\"version\":1,\"entries\":[],\"extra\":1}")));
		}

		private static IEnumerable<Type> ModelTypes()
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies()
				.Where(a => a.GetName().Name.StartsWith("VisualPinball.", StringComparison.Ordinal)
					&& !a.GetName().Name.Contains(".Test") && !a.GetName().Name.Contains(".Editor"));
			foreach (var assembly in assemblies) {
				Type[] types;
				try {
					types = assembly.GetTypes();
				} catch (ReflectionTypeLoadException e) {
					types = e.Types.Where(t => t != null).ToArray();
				}
				foreach (var type in types) {
					if (type.IsGenericTypeDefinition || type.IsInterface || type.IsEnum || type.IsPointer
						|| type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false)
						|| type.Name.Contains("<") || typeof(Delegate).IsAssignableFrom(type) || type.IsByRefLike
						// components are written through their packables, never as they are
						|| typeof(Component).IsAssignableFrom(type)
						|| typeof(UnityEditor.Editor).IsAssignableFrom(type) || typeof(UnityEditor.EditorWindow).IsAssignableFrom(type)) {
						continue;
					}
					yield return type;
				}
			}
		}

		private static void Set(object target, string field, object value)
			=> target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

		private static T Get<T>(object target, string field)
			=> (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);
	}
}
