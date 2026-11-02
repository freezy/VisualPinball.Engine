// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Player-owned SDR output. Native windows borrow only this texture, never the producer's. Submit copies after
	/// plugin BeforeRender and before NativeWindowHost. Close/detach the native window before disposing this object.
	/// </summary>
	public sealed class CabinetOutputTexture : IDisposable
	{
		private readonly Material _copy;
		private bool _disposed;
		public RenderTexture Texture { get; }
		/// <summary>Changes after allocation/recreation. A borrowing window must resubmit its native pointer.</summary>
		public long Revision { get; private set; }
		public CabinetOutputTexture(int width, int height)
		{
			if (width < 1 || height < 1 || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize || (long)width * height > 33554432) throw new ArgumentOutOfRangeException(nameof(width), "Cabinet outputs support up to 32 megapixels.");
			var shader = Resources.Load<Shader>("CabinetOutput");
			if (shader == null || !shader.isSupported) throw new PlatformNotSupportedException("The cabinet output copy shader is unavailable.");
			_copy = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
			Texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) {
				name = "Cabinet output", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
			};
			try { EnsureCreated(); }
			catch { Dispose(); throw; }
			Clear();
		}

		/// <summary>Restores a lost allocation; callers must clear or copy its pixels before presentation.</summary>
		public bool EnsureCreated()
		{
			if (_disposed) throw new ObjectDisposedException(nameof(CabinetOutputTexture));
			if (Texture.IsCreated()) return false;
			if (!Texture.Create()) throw new InvalidOperationException("Cannot allocate cabinet output texture.");
			Revision++;
			return true;
		}

		/// <summary>Copies immediately; source is not retained. Color space describes shader-sampled values.</summary>
		public void Copy(UnityEngine.Texture source, FrameColorSpace colorSpace, FrameAlpha alpha = FrameAlpha.None, bool flipY = false)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(CabinetOutputTexture));
			EnsureCreated();
			if (source == null || source.width < 1 || source.height < 1) { Clear(); return; }
			double aspect = (double)source.width / source.height;
			double outputAspect = (double)Texture.width / Texture.height;
			float width = (float)System.Math.Min(1, aspect / outputAspect), height = (float)System.Math.Min(1, outputAspect / aspect);
			_copy.SetVector("_Content", new Vector4((1 - width) * .5f, (1 - height) * .5f, width, height));
			_copy.SetFloat("_Encoded", colorSpace == FrameColorSpace.Srgb ? 1 : 0);
			_copy.SetFloat("_Alpha", alpha == FrameAlpha.Premultiplied ? 2 : alpha == FrameAlpha.Straight ? 1 : 0);
			_copy.SetFloat("_FlipY", flipY ? 1 : 0);
			var active = RenderTexture.active;
			bool srgb = GL.sRGBWrite;
			try { GL.sRGBWrite = false; Graphics.Blit(source, Texture, _copy); }
			finally { GL.sRGBWrite = srgb; RenderTexture.active = active; }
		}

		public void Clear()
		{
			if (_disposed) throw new ObjectDisposedException(nameof(CabinetOutputTexture));
			EnsureCreated();
			var active = RenderTexture.active;
			try { RenderTexture.active = Texture; GL.Clear(false, true, Color.black); }
			finally { RenderTexture.active = active; }
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			if (Texture != null) { Texture.Release(); Destroy(Texture); }
			if (_copy != null) Destroy(_copy);
		}
		private static void Destroy(UnityEngine.Object value)
		{
			if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
		}
	}
}
