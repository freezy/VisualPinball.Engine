// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace VisualPinball.Unity.Test
{
	public class RuntimeMaterialTextureOwnershipTests
	{
		[UnityTest] public IEnumerator NoMatchingRendererReleasesPreparedTextures() => CheckOwnership(false, true);
		[UnityTest] public IEnumerator UnsupportedProfileReleasesPreparedTextures() => CheckOwnership(true, false);
		[UnityTest] public IEnumerator UsedTextureAndAliasesSurviveUnusedTextureCleanup() => CheckOwnership(true, true);

		private static IEnumerator CheckOwnership(bool rendererPresent, bool supported)
		{
			var previousResolver = VpeMaterialResolver.Active;
			var root = new GameObject("texture-ownership-test");
			var requested = new Texture2D(2, 2);
			var unused = new Texture2D(2, 2);
			var shader = Shader.Find("Unlit/Texture");
			Assert.That(shader, Is.Not.Null);
			var imported = new Material(shader) { name = "matched" };
			var resolver = new Resolver(supported);
			try {
				VpeMaterialResolver.Register(resolver);
				var renderer = rendererPresent ? root.AddComponent<MeshRenderer>() : null;
				if (renderer) renderer.sharedMaterial = imported;
				var prepared = new Dictionary<string, Texture2D> {
					["used"] = requested, ["used-alias"] = requested,
					["unused"] = unused, ["unused-alias"] = unused
				};
				var payload = new VpeMaterialsPayload {
					Profiles = new[] {
						new VpeMaterialProfile { Id = "matched", Name = "matched", Type = VpeMaterialTypes.Lit,
							Lit = new VpeLitProfile { MaskMap = new VpeTextureRef { TextureId = "used" } } },
						new VpeMaterialProfile { Id = "orphan", Name = "orphan", Type = VpeMaterialTypes.Lit,
							Lit = new VpeLitProfile { MaskMap = new VpeTextureRef { TextureId = "unused" } } }
					}
				};
				var task = VpeMaterialReader.TryApplyAsync(payload, null, null, root.transform, _ => null, "ownership-test", prepared);
				while (!task.IsCompleted) yield return null;
				if (task.IsFaulted) throw task.Exception.GetBaseException();
				Assert.That(task.Result, Is.True, "Skipped profiles still report successful application.");
				yield return null; // Runtime Destroy is deferred until the end of the frame.
				Assert.That(!unused, Is.True, "Unrequested textures must be destroyed.");
				Assert.That(prepared.ContainsKey("unused"), Is.False);
				Assert.That(prepared.ContainsKey("unused-alias"), Is.False);
				if (rendererPresent && supported) {
					Assert.That((bool)requested, Is.True);
					Assert.That(prepared.Count, Is.EqualTo(2), "Aliases of a requested object stay owned by the table.");
					Assert.That(renderer.sharedMaterial.mainTexture, Is.SameAs(requested));
				} else {
					Assert.That(!requested, Is.True);
					Assert.That(prepared, Is.Empty);
				}
			} finally {
				VpeMaterialResolver.Register(previousResolver);
				Object.DestroyImmediate(root);
				if (resolver.Created) Object.DestroyImmediate(resolver.Created);
				Object.DestroyImmediate(imported);
				if (requested) Object.DestroyImmediate(requested);
				if (unused) Object.DestroyImmediate(unused);
			}
		}

		private sealed class Resolver : IVpeMaterialResolver
		{
			private readonly bool _supported;
			public Material Created;
			public Resolver(bool supported) { _supported = supported; }
			public bool Supports(string materialType) => _supported;
			public Material CreateMaterial(VpeMaterialProfile profile, IVpeTextureProvider textures, Material importedMaterial)
			{
				Created = new Material(importedMaterial) { mainTexture = textures.Get(profile.Lit.MaskMap.TextureId) };
				return Created;
			}
		}
	}
}
