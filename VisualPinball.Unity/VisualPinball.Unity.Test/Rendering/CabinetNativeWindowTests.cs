// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using VisualPinball.Plugins;

namespace VisualPinball.Unity.Test
{
	public sealed class CabinetNativeWindowTests
	{
		private static readonly System.Collections.Generic.List<CabinetOutputTexture> Quarantine = new();
		[UnityTest]
		public IEnumerator TwoWindowsPresentOwnedCopiesAndDrainAfterReplacement()
		{
			if (!NativeWindowSurface.IsSupported) Assert.Ignore("Native windows are unavailable on this graphics backend.");
			// EditMode: drive the same event pair as NativeWindowHost, without starting a table or plugin.
			if (Application.isPlaying) Assert.Ignore("Run this probe in EditMode; NativeWindowHost owns events during play.");
			var first = new CabinetOutputTexture(64, 32);
			var second = new CabinetOutputTexture(32, 64);
			System.Exception failure = null;
			var source = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
			source.SetPixel(0, 0, Color.green); source.Apply();
			first.Copy(source, FrameColorSpace.Linear); second.Copy(source, FrameColorSpace.Linear);
			Object.DestroyImmediate(source);
			NativeWindowSurface a = null, b = null;
			try {
				Capture(() => {
					a = new NativeWindowSurface(20, 20, 64, 32);
					b = new NativeWindowSurface(90, 20, 32, 64);
					a.SetTexture(first.Texture.GetNativeTexturePtr()); b.SetTexture(second.Texture.GetNativeTexturePtr());
					a.Configure(20, 20, 64, 32, true, false); b.Configure(90, 20, 32, 64, true, false);
				}, ref failure);
				bool ready = false;
				for (int i = 0; i < 30 && !ready && failure == null; i++) {
					Capture(() => { Events(); ready = a.Diagnostics.PresentedFrames > 0 && b.Diagnostics.PresentedFrames > 0; }, ref failure);
					yield return null;
				}
				if (failure == null) Capture(() => {
					Assert.That(ready, Is.True, "Both windows must present.");
					Assert.That(a.Diagnostics.SourceWidth, Is.EqualTo(64));
					Assert.That(b.Diagnostics.SourceWidth, Is.EqualTo(32));
					a.SetTexture(second.Texture.GetNativeTexturePtr());
				}, ref failure);
				ulong frames = 0;
				if (failure == null) Capture(() => frames = a.Diagnostics.PresentedFrames, ref failure);
				ready = false;
				for (int i = 0; i < 30 && !ready && failure == null; i++) {
					Capture(() => { Events(); var state = a.Diagnostics; ready = state.SourceWidth == 32 && state.PresentedFrames > frames; }, ref failure);
					yield return null;
				}
				if (failure == null) Capture(() => Assert.That(ready, Is.True, "The replacement pointer must present new frames."), ref failure);
			} finally {
				Capture(() => a?.Dispose(), ref failure);
				Capture(() => b?.Dispose(), ref failure);
				// Also safe if the test is cancelled: retain the outputs in an editor callback until native work drains.
				ReleaseAfterDrain(first, second);
			}
			for (int i = 0; i < 60 && NativeWindowSurface.PendingResourceCount > 0; i++) { Events(); yield return null; }
			Assert.That(NativeWindowSurface.PendingResourceCount, Is.Zero, "Native GPU resources must drain before releasing the owned outputs.");
			if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
		}

		private static void Capture(System.Action action, ref System.Exception failure)
		{
			try { action(); } catch (System.Exception error) { failure ??= error; }
		}

		private static void ReleaseAfterDrain(CabinetOutputTexture first, CabinetOutputTexture second)
		{
			int updates = 0;
			EditorApplication.CallbackFunction drain = null;
			drain = () => {
				if (NativeWindowSurface.PendingResourceCount > 0 && updates++ < 600) { Events(); return; }
				EditorApplication.update -= drain;
				if (NativeWindowSurface.PendingResourceCount > 0) {
					Quarantine.Add(first); Quarantine.Add(second);
					Debug.LogWarning("Cabinet native probe retained its outputs because GPU retirement did not drain.");
					return;
				}
				first.Dispose(); second.Dispose();
			};
			EditorApplication.update += drain;
		}

		private static void Events()
		{
			NativeWindowSurface.Pump();
			GL.IssuePluginEvent(NativeWindowSurface.RenderEventFunction, NativeWindowSurface.DrawEventId);
			GL.IssuePluginEvent(NativeWindowSurface.RenderEventFunction, NativeWindowSurface.PresentEventId);
			EditorApplication.QueuePlayerLoopUpdate();
		}
	}
}
