// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace VisualPinball.Unity.Test
{
	public class BakedOcclusionTests
	{
		private static VpeTexture Texture(string id, int width = 1024, int height = 2048) => new() { Id = id, FileName = id + ".png", ColorSpace = VpeColorSpaces.Linear, Width = width, Height = height };

		private static VpeMaterialProfile Profile(string id, string mask, string occlusion, float metallic = 0.25f, float smoothness = 0.75f) => new() {
			Id = id,
			Type = VpeMaterialTypes.Lit,
			Lit = new VpeLitProfile {
				Metallic = metallic,
				Smoothness = smoothness,
				MaskMap = mask == null ? null : new VpeTextureRef { TextureId = mask },
				SmoothnessRemap = new Vector2(0.4f, 1f),
				OcclusionMap = occlusion == null ? null : new VpeTextureRef { TextureId = occlusion },
			},
		};

		[Test] public void ProfilesSharingAMaskAndAnOcclusionShareOneMergedMask()
		{
			var payload = new VpeMaterialsPayload {
				Textures = new[] { Texture("mask"), Texture("ao") },
				Profiles = new[] { Profile("wood", "mask", "ao"), Profile("decals", "mask", "ao") },
			};

			var merges = VpeBakedOcclusion.Prepare(payload);

			Assert.That(merges.Count, Is.EqualTo(1));
			var merge = merges.Values.Single();
			Assert.That(merge.Mask.Id, Is.EqualTo("mask"));
			Assert.That(merge.Occlusion.Id, Is.EqualTo("ao"));
			Assert.That(merge.Target.FileName, Is.EqualTo("mask.png"), "the merged mask cooks from the authored mask's bytes");
			Assert.That(merge.CooksFromOcclusion, Is.False);
			foreach (var lit in payload.Profiles.Select(p => p.Lit)) {
				Assert.That(lit.MaskMap.TextureId, Is.EqualTo(merge.Target.Id));
				Assert.That(lit.OcclusionMap, Is.Null);
				Assert.That(lit.SmoothnessRemap, Is.EqualTo(new Vector2(0.4f, 1f)), "an authored mask keeps its remaps");
			}
			Assert.That(payload.Textures.Select(t => t.Id), Is.EquivalentTo(new[] { "mask", "ao", merge.Target.Id }));
		}

		[Test] public void AProfileWithoutAMaskTakesTheOcclusionAsMaskWithItsScalarsPinned()
		{
			var payload = new VpeMaterialsPayload {
				Textures = new[] { Texture("ao") },
				Profiles = new[] { Profile("wood", null, "ao", metallic: 0.1f, smoothness: 0.6f) },
			};

			var merge = VpeBakedOcclusion.Prepare(payload).Values.Single();

			var lit = payload.Profiles[0].Lit;
			Assert.That(merge.Mask, Is.Null);
			Assert.That(merge.Target.FileName, Is.EqualTo("ao.png"));
			Assert.That(merge.CooksFromOcclusion, Is.True);
			Assert.That(lit.MaskMap.TextureId, Is.EqualTo(merge.Target.Id));
			Assert.That(lit.MaskPacking, Is.EqualTo(VpeMaskPackings.HdrpMaskMap));
			Assert.That(lit.MetallicRemap, Is.EqualTo(new Vector2(0.1f, 0.1f)));
			Assert.That(lit.SmoothnessRemap, Is.EqualTo(new Vector2(0.6f, 0.6f)));
			Assert.That(lit.AoRemap, Is.EqualTo(new Vector2(0f, 1f)));
		}

		[Test] public void AMaskWithFewerTexelsCooksAtTheOcclusionsResolution()
		{
			var payload = new VpeMaterialsPayload {
				Textures = new[] { Texture("mask", 4, 4), Texture("ao") },
				Profiles = new[] { Profile("wood", "mask", "ao") },
			};

			var merge = VpeBakedOcclusion.Prepare(payload).Values.Single();

			Assert.That(merge.CooksFromOcclusion, Is.True);
			Assert.That(merge.Mask.Id, Is.EqualTo("mask"));
			Assert.That(merge.Target.FileName, Is.EqualTo("ao.png"));
			Assert.That((merge.Target.Width, merge.Target.Height), Is.EqualTo((1024, 2048)));
			Assert.That(payload.Profiles[0].Lit.SmoothnessRemap, Is.EqualTo(new Vector2(0.4f, 1f)), "the authored mask's remaps stay");
		}

		[Test] public void TheAuthoredOcclusionRemapMovesIntoTheMerge()
		{
			var payload = new VpeMaterialsPayload {
				Textures = new[] { Texture("mask", 2048, 4096), Texture("ao") },
				Profiles = new[] { Profile("wood", "mask", "ao"), Profile("decals", "mask", "ao") },
			};
			payload.Profiles[0].Lit.AoRemap = new Vector2(0.8f, 1f);

			var merges = VpeBakedOcclusion.Prepare(payload);

			Assert.That(merges.Count, Is.EqualTo(2), "profiles with different remaps can't share a merged mask");
			var remapped = merges.Values.Single(m => m.AoRemap != new Vector2(0f, 1f));
			Assert.That(remapped.AoRemap, Is.EqualTo(new Vector2(0.8f, 1f)));
			Assert.That(payload.Profiles[0].Lit.MaskMap.TextureId, Is.EqualTo(remapped.Target.Id));
			Assert.That(payload.Profiles.Select(p => p.Lit.AoRemap), Is.All.EqualTo(new Vector2(0f, 1f)));
		}

		[Test] public void AFailedMergeGivesTheProfilesTheirRemapBack()
		{
			var payload = new VpeMaterialsPayload {
				Textures = new[] { Texture("mask", 2048, 4096), Texture("ao") },
				Profiles = new[] { Profile("wood", "mask", "ao") },
			};
			payload.Profiles[0].Lit.AoRemap = new Vector2(1f, 1f);

			var merge = VpeBakedOcclusion.Prepare(payload).Values.Single();
			VpeBakedOcclusion.RestoreRemap(payload, merge);

			Assert.That(payload.Profiles[0].Lit.AoRemap, Is.EqualTo(new Vector2(1f, 1f)));
		}

		[Test] public void ComposeSamplesARepeatingMaskWrappedAndAPointFilteredOneUnblended()
		{
			// A 2x1 mask [0, 255] under a 4x1 occlusion: the first texel's left neighbor is the mask's last texel
			// when it repeats, the edge texel when it clamps.
			static byte[] Composed(bool repeat, bool point)
			{
				var rgba = Enumerable.Repeat((byte)255, 16).ToArray();
				VpeBakedOcclusion.ComposeInto(rgba, 4, 1, new byte[] { 0, 0, 0, 0, 255, 255, 255, 255 }, 2, 1, new Vector2(0f, 1f), repeat, point);
				return rgba;
			}
			Assert.That(Composed(false, false)[0], Is.EqualTo(0));
			Assert.That(Composed(true, false)[0], Is.EqualTo(64));
			Assert.That(Composed(false, true)[4], Is.EqualTo(0));
			Assert.That(Composed(false, false)[4], Is.EqualTo(64));
		}

		[Test] public void DanglingOcclusionRefsAreDropped()
		{
			var payload = new VpeMaterialsPayload {
				Textures = new[] { Texture("mask") },
				Profiles = new[] { Profile("wood", "mask", "missing") },
			};

			Assert.That(VpeBakedOcclusion.Prepare(payload), Is.Empty);
			Assert.That(payload.Profiles[0].Lit.MaskMap.TextureId, Is.EqualTo("mask"));
			Assert.That(payload.Profiles[0].Lit.OcclusionMap, Is.Null);
		}

		[Test] public void StripRemovesTheOcclusionAndTheTexturesOnlyItUsed()
		{
			var payload = new VpeMaterialsPayload {
				Textures = new[] { Texture("mask"), Texture("ao"), Texture("shared") },
				Profiles = new[] { Profile("wood", "mask", "ao"), Profile("plastic", "shared", "shared") },
			};

			VpeBakedOcclusion.Strip(payload);

			Assert.That(payload.Profiles.All(p => p.Lit.OcclusionMap == null));
			Assert.That(payload.Textures.Select(t => t.Id), Is.EquivalentTo(new[] { "mask", "shared" }));
			Assert.That(payload.Profiles[0].Lit.MaskMap.TextureId, Is.EqualTo("mask"));
		}

		[Test] public void MultiplyScalesOnlyTheOcclusionChannel()
		{
			// 2x1 mask, 1x1 occlusion of half: every texel's green halves, the other channels stay.
			var rgba = new byte[] { 10, 200, 30, 40, 50, 100, 70, 80 };
			VpeBakedOcclusion.MultiplyInto(rgba, 2, 1, new byte[] { 128 }, 1, 1, new Vector2(0f, 1f));
			Assert.That(rgba, Is.EqualTo(new byte[] { 10, 100, 30, 40, 50, 50, 70, 80 }));
		}

		[Test] public void MultiplyAppliesTheAuthoredRemapFirst()
		{
			// A remap of (1, 1) turns the authored occlusion off: only the baked half is left.
			var rgba = new byte[] { 0, 0, 0, 255 };
			VpeBakedOcclusion.MultiplyInto(rgba, 1, 1, new byte[] { 128 }, 1, 1, new Vector2(1f, 1f));
			Assert.That(rgba[1], Is.EqualTo(128));
		}

		[Test] public void ComposeSamplesTheMaskIntoTheOcclusion()
		{
			// 2x1 occlusion (decoded as RGBA, occlusion in red) over a 1x1 mask.
			var rgba = new byte[] { 255, 255, 255, 255, 64, 64, 64, 255 };
			VpeBakedOcclusion.ComposeInto(rgba, 2, 1, new byte[] { 10, 200, 30, 40 }, 1, 1, new Vector2(0f, 1f));
			Assert.That(rgba, Is.EqualTo(new byte[] { 10, 200, 30, 40, 10, 50, 30, 40 }));
		}

		[Test] public void MultiplySamplesALargerOcclusionBilinearly()
		{
			// 1x2 mask over a 1x4 occlusion (bottom row first): each mask texel lands between two occlusion rows.
			var rgba = new byte[] { 0, 255, 0, 0, 0, 255, 0, 0 };
			VpeBakedOcclusion.MultiplyInto(rgba, 1, 2, new byte[] { 0, 255, 255, 255 }, 1, 4, new Vector2(0f, 1f));
			Assert.That(rgba[1], Is.EqualTo(128));
			Assert.That(rgba[5], Is.EqualTo(255));
		}

		[Test] public void DecodeReturnsTheBottomRowFirst()
		{
			var texture = new Texture2D(1, 2, TextureFormat.RGBA32, false, true);
			try {
				texture.SetPixels32(new[] { new Color32(10, 10, 10, 255), new Color32(200, 200, 200, 255) });
				texture.Apply();
				var pixels = VpeBakedOcclusion.DecodeOcclusion(texture.EncodeToPNG(), out var width, out var height);
				Assert.That((width, height), Is.EqualTo((1, 2)));
				Assert.That(pixels[0], Is.EqualTo(10).Within(1));
				Assert.That(pixels[1], Is.EqualTo(200).Within(1));
				var mask = VpeBakedOcclusion.DecodeMask(texture.EncodeToPNG(), out width, out height);
				Assert.That(mask.Length, Is.EqualTo(8));
				Assert.That(mask[0], Is.EqualTo(10).Within(1));
				Assert.That(mask[4], Is.EqualTo(200).Within(1));
			} finally {
				Object.DestroyImmediate(texture);
			}
		}
	}
}
