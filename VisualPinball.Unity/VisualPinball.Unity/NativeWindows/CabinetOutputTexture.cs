// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using UnityEngine;
using UnityEngine.Rendering;
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
		private readonly RenderBuffer[] _insetColor = new RenderBuffer[1];
		private static readonly RenderBufferLoadAction[] InsetLoad = { RenderBufferLoadAction.Load };
		private static readonly RenderBufferStoreAction[] InsetStore = { RenderBufferStoreAction.Store };
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
			=> Copy(source, colorSpace, alpha, flipY, 0, false, false, new Rect(0, 0, 1, 1));

		/// <summary>Contains transformed content inside a normalized viewport measured from the bottom-left (Unity UV coordinates). Preserves pixels outside; letterbox bars inside are opaque black. Rotation is clockwise quarter-turns.</summary>
		public void Copy(UnityEngine.Texture source, FrameColorSpace colorSpace, FrameAlpha alpha, bool flipY,
			int rotation, bool mirrorX, bool mirrorY, Rect viewport)
		{
			if (_disposed) throw new ObjectDisposedException(nameof(CabinetOutputTexture));
			if (viewport.x < 0 || viewport.y < 0 || viewport.width <= 0 || viewport.height <= 0 || viewport.xMax > 1 || viewport.yMax > 1
				|| float.IsNaN(viewport.x) || float.IsNaN(viewport.y) || float.IsNaN(viewport.width) || float.IsNaN(viewport.height)) throw new ArgumentOutOfRangeException(nameof(viewport));
			if (EnsureCreated()) Clear();
			if (source == null || source.width < 1 || source.height < 1) source = Texture2D.blackTexture;
			rotation = ((rotation % 4) + 4) % 4;
			double aspect = (double)source.width / source.height;
			if ((rotation & 1) != 0) aspect = 1 / aspect;
			double outputAspect = Texture.width * (double)viewport.width / (Texture.height * viewport.height);
			float width = (float)System.Math.Min(1, aspect / outputAspect), height = (float)System.Math.Min(1, outputAspect / aspect);
			_copy.SetVector("_Viewport", new Vector4(viewport.x, viewport.y, viewport.width, viewport.height));
			_copy.SetVector("_Content", new Vector4(viewport.x + (1 - width) * .5f * viewport.width, viewport.y + (1 - height) * .5f * viewport.height, width * viewport.width, height * viewport.height));
			_copy.SetVector("_SourceTexel", new Vector4(1f / source.width, 1f / source.height, source.width, source.height));
			_copy.SetFloat("_Encoded", colorSpace == FrameColorSpace.Srgb ? 1 : 0);
			_copy.SetFloat("_Alpha", alpha == FrameAlpha.Premultiplied ? 2 : alpha == FrameAlpha.Straight ? 1 : 0);
			_copy.SetFloat("_FlipY", flipY ? 1 : 0);
			_copy.SetFloat("_Rotation", rotation);
			_copy.SetVector("_Mirror", new Vector4(mirrorX ? 1 : 0, mirrorY ? 1 : 0, 0, 0));
			var active = RenderTexture.active;
			bool srgb = GL.sRGBWrite;
			try {
				GL.sRGBWrite = false;
				if (viewport == new Rect(0, 0, 1, 1)) Graphics.Blit(source, Texture, _copy);
				else {
					// Explicitly retain the base image; a Blit may choose DontCare on tile-based GPUs.
					_insetColor[0] = Texture.colorBuffer;
					Graphics.SetRenderTarget(new RenderTargetSetup(_insetColor, Texture.depthBuffer, 0, CubemapFace.Unknown, InsetLoad, InsetStore, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare));
					_copy.SetTexture("_MainTex", source);
					GL.PushMatrix();
					try {
						GL.LoadOrtho();
						if (!_copy.SetPass(0)) throw new InvalidOperationException("Cannot draw cabinet inset.");
						GL.Begin(GL.QUADS);
						try {
							GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
							GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
							GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
							GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
						} finally { GL.End(); }
					} finally { GL.PopMatrix(); }
				}
			}
			finally { _copy.SetTexture("_MainTex", null); GL.sRGBWrite = srgb; RenderTexture.active = active; }
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
