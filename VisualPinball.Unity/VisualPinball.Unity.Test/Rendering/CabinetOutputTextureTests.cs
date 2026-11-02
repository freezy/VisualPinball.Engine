// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: GPL-3.0-or-later

using NUnit.Framework;
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity.Test
{
	public sealed class CabinetOutputTextureTests
	{
		[Test]
		public void CopyOwnsItsPixelsAfterProducerDestructionAndLetterboxes()
		{
			var source = Solid(16, 8, new Color(.25f, .25f, .25f, 1));
			using var output = new CabinetOutputTexture(16, 16);
			output.Copy(source, FrameColorSpace.Srgb);
			Object.DestroyImmediate(source);
			var pixels = Read(output.Texture);
			try {
				Assert.That(pixels.GetPixel(8, 8).r, Is.EqualTo(.25f).Within(.015));
				Assert.That(pixels.GetPixel(8, 1).r, Is.LessThan(.005));
			} finally { Object.DestroyImmediate(pixels); }
		}

		[Test]
		public void RepeatedCopyUpdatesPixelsWithoutReplacementEventsAndEncodesLinearLight()
		{
			using var output = new CabinetOutputTexture(8, 8);
			var pointer = output.Texture.GetNativeTexturePtr();
			var source = Solid(8, 8, Color.red);
			try {
				output.Copy(source, FrameColorSpace.Linear);
				Assert.That(output.Texture.GetNativeTexturePtr(), Is.EqualTo(pointer));
				var pixels = Read(output.Texture);
				Assert.That(pixels.GetPixel(4, 4).r, Is.GreaterThan(.98));
				Object.DestroyImmediate(pixels);
				Fill(source, new Color(.25f, .25f, .25f, 1));
				output.Copy(source, FrameColorSpace.Linear);
				pixels = Read(output.Texture);
				try { Assert.That(pixels.GetPixel(4, 4).r, Is.EqualTo(.537f).Within(.015)); }
				finally { Object.DestroyImmediate(pixels); }
			} finally { Object.DestroyImmediate(source); }
		}

		[TestCase(8f / 255, FrameColorSpace.Srgb, 8f / 255)]
		[TestCase(.002f, FrameColorSpace.Linear, .02584f)]
		public void ConversionPreservesShadowDetail(float input, FrameColorSpace space, float expected)
		{
			var source = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
			source.SetPixel(0, 0, new Color(input, input, input, 1)); source.Apply();
			using var output = new CabinetOutputTexture(8, 8);
			try {
				output.Copy(source, space);
				var pixels = Read(output.Texture);
				try { Assert.That(pixels.GetPixel(4, 4).r, Is.EqualTo(expected).Within(1.1f / 255)); }
				finally { Object.DestroyImmediate(pixels); }
			} finally { Object.DestroyImmediate(source); }
		}

		[Test]
		public void EncodedPremultipliedInputAndLostOutputAllocationAreHandled()
		{
			var source = Solid(8, 8, new Color(.25f, 0, 0, .5f));
			using var output = new CabinetOutputTexture(8, 8);
			long revision = output.Revision;
			output.Texture.Release();
			try {
				output.Copy(source, FrameColorSpace.Srgb, FrameAlpha.Premultiplied);
				Assert.That(output.Revision, Is.GreaterThan(revision));
				var pixels = Read(output.Texture);
				// Decode .5, compose at .5 alpha over black, encode: about .361.
				try { Assert.That(pixels.GetPixel(4, 4).r, Is.EqualTo(.361f).Within(2f / 255)); }
				finally { Object.DestroyImmediate(pixels); }
			} finally { Object.DestroyImmediate(source); }
		}

		[Test]
		public void CopyRestoresGlobalRenderStateAndComposesPremultipliedInputOverBlack()
		{
			var source = Solid(8, 8, new Color(.25f, 0, 0, .5f));
			using var output = new CabinetOutputTexture(8, 8);
			var previous = RenderTexture.active;
			bool srgb = GL.sRGBWrite;
			try {
				output.Copy(source, FrameColorSpace.Linear, FrameAlpha.Premultiplied);
				Assert.That(RenderTexture.active, Is.SameAs(previous));
				Assert.That(GL.sRGBWrite, Is.EqualTo(srgb));
				var pixels = Read(output.Texture);
				try { Assert.That(pixels.GetPixel(4, 4).r, Is.EqualTo(.537f).Within(.015)); Assert.That(pixels.GetPixel(4, 4).a, Is.GreaterThan(.99)); }
				finally { Object.DestroyImmediate(pixels); }
			} finally { Object.DestroyImmediate(source); }
		}

		private static Texture2D Solid(int width, int height, Color color)
		{
			var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
			Fill(texture, color); return texture;
		}
		private static void Fill(Texture2D texture, Color color)
		{
			var colors = new Color[texture.width * texture.height];
			for (int i = 0; i < colors.Length; i++) colors[i] = color;
			texture.SetPixels(colors); texture.Apply();
		}
		private static Texture2D Read(RenderTexture source)
		{
			var previous = RenderTexture.active;
			var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
			try { RenderTexture.active = source; texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); texture.Apply(); return texture; }
			finally { RenderTexture.active = previous; }
		}
	}
}
