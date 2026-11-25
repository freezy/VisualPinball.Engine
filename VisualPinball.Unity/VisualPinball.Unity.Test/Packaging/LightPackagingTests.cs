// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class LightPackagingTests
	{
		/// <summary>
		/// A lamp's light sources keep their own settings through a runtime load, although glTFast
		/// puts the spot on rotated helper children that shift the other lights' sibling indices.
		/// </summary>
		[UnityTest]
		public IEnumerator LampLightSourcesKeepTheirSettingsBesideASpot()
		{
			var path = Path.Combine(Path.GetTempPath(), $"vpe-lamp-lights-{Guid.NewGuid():N}.vpe");
			var source = new GameObject("Table");
			GameObject imported = null;
			try {
				source.AddComponent<TableComponent>();
				var lampGo = new GameObject("Lamp");
				lampGo.transform.SetParent(source.transform, false);
				var spot = lampGo.AddComponent<Light>();
				spot.type = LightType.Spot;
				spot.range = 0.33f;
				AddLight(lampGo.transform, "A", LightType.Point, 0.11f);
				AddLight(lampGo.transform, "B", LightType.Spot, 0.22f);
				AddLight(lampGo.transform, "C", LightType.Point, 0.44f);
				// A hidden bulb mesh isn't exported, but the lamp's saved light paths count it. Last,
				// so it can't cancel out the helper the spot prepends to the lamp's children.
				var bulb = new GameObject("Bulb");
				bulb.transform.SetParent(lampGo.transform, false);
				bulb.SetActive(false);
				lampGo.AddComponent<LightComponent>();

				new PackageWriter(source).WritePackageSync(path);
				var load = new RuntimePackageReader(path).ImportIntoScene();
				while (!load.IsCompleted) {
					yield return null;
				}
				imported = load.GetAwaiter().GetResult();

				var lamp = imported.GetComponentInChildren<LightComponent>(true);
				Assert.That(lamp, Is.Not.Null);
				var hierarchy = Dump(lamp.transform);
				AssertLight(lamp.transform, null, LightType.Spot, 0.33f, hierarchy);
				AssertLight(lamp.transform, "A", LightType.Point, 0.11f, hierarchy);
				AssertLight(lamp.transform, "B", LightType.Spot, 0.22f, hierarchy);
				AssertLight(lamp.transform, "C", LightType.Point, 0.44f, hierarchy);
			} finally {
				if (imported) { Object.DestroyImmediate(imported); }
				Object.DestroyImmediate(source);
				File.Delete(path);
			}
		}

		private static void AddLight(Transform parent, string name, LightType type, float range)
		{
			var go = new GameObject(name);
			go.transform.SetParent(parent, false);
			var light = go.AddComponent<Light>();
			light.type = type;
			light.range = range;
		}

		private static void AssertLight(Transform lamp, string name, LightType type, float range, string hierarchy)
		{
			var node = name == null ? lamp : lamp.Find(name);
			Assert.That(node, Is.Not.Null, $"{name} is missing: {hierarchy}");
			var light = VpeLightRestore.FindLight(node);
			Assert.That(light, Is.Not.Null, $"{node.name} has no light: {hierarchy}");
			Assert.That(light.type, Is.EqualTo(type), $"{node.name} took another light's type: {hierarchy}");
			Assert.That(light.range, Is.EqualTo(range).Within(0.0001f), $"{node.name} took another light's settings: {hierarchy}");
		}

		private static string Dump(Transform node)
		{
			var text = new StringBuilder();
			Dump(node, 0, text);
			return text.ToString();
		}

		private static void Dump(Transform node, int depth, StringBuilder text)
		{
			var light = node.GetComponent<Light>();
			text.Append(' ', depth * 2).Append(node.name);
			if (light) {
				text.Append($" [{light.type} range={light.range:0.###}]");
			}
			text.Append(" | ");
			foreach (Transform child in node) {
				Dump(child, depth + 1, text);
			}
		}
	}
}
