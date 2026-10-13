// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	public interface IUnityGameScene
	{
		GameObject Table { get; }
		IGamelogicEngine Gamelogic { get; }
	}
	public sealed class UnityGameScene : IUnityGameScene
	{
		public GameObject Table { get; }
		public IGamelogicEngine Gamelogic { get; }
		public UnityGameScene(GameObject table, IGamelogicEngine gamelogic) { Table = table; Gamelogic = gamelogic; }
	}
	/// <summary>Native display negotiation may apply to the entire provider, even when the API accepts a display ID.</summary>
	public interface IDisplayResourceMetadata
	{
		DisplayEncoding DisplayFormats { get; }
		DisplayEncoding DefaultDisplayFormat { get; }
		bool FormatAppliesToAllDisplays { get; }
	}
	public interface IGamelogicAudioResourceProvider { IPluginAudioSource AudioSource { get; } }
	public static class UnityPluginCapability
	{
		public const string GameScene = "vpe.unity.game-scene.v1";
		public const string Events = "vpe.events.v1";
		public const string Outputs = "vpe.outputs.v1";
	}
}
