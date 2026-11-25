// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// https://github.com/freezy/VisualPinball.Engine
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Unity.Editor;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class ShadowPackagingTests
	{
		[UnityTest]
		public IEnumerator AuthoredShadowsSurviveTablePackageRoundTrip()
		{
			if (RenderPipeline.Current.Type != RenderPipelineType.Hdrp) {
				Assert.Ignore("This test requires HDRP to export renderer state and HDRP light settings.");
			}

			var path = Path.Combine(Path.GetTempPath(), $"vpe-shadows-{Guid.NewGuid():N}.vpe");
			var previousResolver = VpeMaterialResolver.Active;
			var source = new GameObject("Table");
			GameObject imported = null;
			Material material = null;
			try {
				source.AddComponent<TableComponent>();
				var lamp = new GameObject("Spotlight");
				lamp.transform.SetParent(source.transform, false);
				var light = lamp.AddComponent<Light>();
				light.type = LightType.Spot;
				var converter = RenderPipeline.Current.LightConverter;
				converter.UpdateLight(light, new VisualPinball.Engine.VPT.Light.LightData(), true);
				light.shadows = LightShadows.Soft;
				light.shapeRadius = 0.125f;

				var shadowSettings = new HdrpLightSourcePackable();
				converter.CaptureShadowSettings(light, ref shadowSettings);
				shadowSettings.ShadowDimmer = 0.625f;
				shadowSettings.NormalBias = 1.25f;
				converter.RestoreShadowSettings(light, shadowSettings);

				var caster = GameObject.CreatePrimitive(PrimitiveType.Quad);
				caster.transform.SetParent(source.transform, false);
				var shader = Shader.Find("HDRP/Lit");
				Assert.That(shader, Is.Not.Null);
				material = new Material(shader) { name = "Caster" };
				var renderer = caster.GetComponent<MeshRenderer>();
				renderer.sharedMaterial = material;
				renderer.staticShadowCaster = true;

				new PackageWriter(source).WritePackageSync(path);

				// The Player registers a resolver at startup. This test only needs the renderer-state pass.
				VpeMaterialResolver.Register(new KeepImportedMaterials());
				var load = new RuntimePackageReader(path).ImportIntoScene();
				while (!load.IsCompleted) {
					yield return null;
				}
				imported = load.GetAwaiter().GetResult();

				var restoredLight = imported.GetComponentInChildren<Light>(true);
				Assert.That(restoredLight, Is.Not.Null);
				Assert.That(restoredLight.shadows, Is.EqualTo(LightShadows.Soft));
				Assert.That(restoredLight.shapeRadius, Is.EqualTo(0.125f).Within(0.0001f));
				var restoredSettings = new HdrpLightSourcePackable();
				converter.CaptureShadowSettings(restoredLight, ref restoredSettings);
				Assert.That(restoredSettings.ShadowDimmer, Is.EqualTo(0.625f).Within(0.0001f));
				Assert.That(restoredSettings.NormalBias, Is.EqualTo(1.25f).Within(0.0001f));

				var restoredRenderer = imported.GetComponentInChildren<MeshRenderer>(true);
				Assert.That(restoredRenderer, Is.Not.Null);
				Assert.That(restoredRenderer.staticShadowCaster, Is.True);
			} finally {
				VpeMaterialResolver.Register(previousResolver);
				if (imported) { Object.DestroyImmediate(imported); }
				Object.DestroyImmediate(source);
				if (material) { Object.DestroyImmediate(material); }
				File.Delete(path);
			}
		}

		private sealed class KeepImportedMaterials : IVpeMaterialResolver
		{
			public bool Supports(string materialType) => false;
			public Material CreateMaterial(VpeMaterialProfile profile, IVpeTextureProvider textures, Material importedMaterial) => null;
		}
	}
}
