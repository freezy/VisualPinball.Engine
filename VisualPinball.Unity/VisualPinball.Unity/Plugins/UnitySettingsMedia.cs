// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>Native output transport. The Player owns placement controls and test-pattern rendering.</summary>
	public interface IUnitySettingsWindow : IDisposable
	{
		RectInt Bounds { get; }
		void SetBounds(RectInt bounds);
		void SetTexture(Texture texture);
		void Activate();
	}
	public interface IUnitySettingsWindowProvider
	{
		bool Supported { get; }
		IUnitySettingsWindow OpenWindow(RectInt bounds);
	}
	/// <summary>Texture and normalized placement data; no UI objects or styling cross this boundary.</summary>
	public sealed class UnitySettingsPreviewLayer
	{
		public string Id { get; }
		public Texture Texture { get; set; }
		public Rect Rectangle { get; set; }
		public bool Visible { get; set; }
		public UnitySettingsPreviewLayer(string id) { Id = id; }
	}
	public interface IUnitySettingsPreview : IDisposable
	{
		Vector2 CanvasSize { get; }
		IReadOnlyList<UnitySettingsPreviewLayer> Layers { get; }
		void Update();
	}
	public interface IUnitySettingsPreviewProvider
	{
		/// <summary>Called on Unity's owning thread. Return an owned preview; cancellation must release partial startup.</summary>
		Task<IUnitySettingsPreview> OpenPreviewAsync(string provider, PluginChoiceRequest request, CancellationToken cancellation);
	}
}
