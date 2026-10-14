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

	/// <summary>Receives every controller output in the controller's own numbering, on Unity's main thread.</summary>
	public interface INativeOutputListener
	{
		void NativeOutputChanged(ControllerEventKind kind, int number, double value, ControllerValueSemantics semantics, double maximum);
		/// <summary>The controller finished one poll.</summary>
		void NativeOutputPollEnd();
	}

	/// <summary>
	/// A gamelogic engine that reports all lamps, solenoids, GI strings and mechs by native number, independent of the
	/// table's mapping. Backglass-only outputs (match, tilt, ball in play) are usually not mapped to anything on the
	/// playfield, so the mapped events alone miss them.
	/// </summary>
	public interface IGamelogicNativeOutputs
	{
		/// <summary>Sets the single listener, or null. Changes are delivered from the engine's main-thread poll.</summary>
		void SetNativeOutputListener(INativeOutputListener listener);
	}

	/// <summary>Layout and technology of a segment display, as published in <see cref="PluginSegmentFrame"/>.</summary>
	public interface ISegmentDisplayMetadata
	{
		/// <summary>Returns the layout (<c>seg7</c>, <c>seg9</c>, <c>seg14</c>, <c>seg16</c>) and hardware hint of a display.</summary>
		bool TryGetSegmentLayout(string displayId, out string layout, out string hardware);
	}
	public static class UnityPluginCapability
	{
		public const string GameScene = "vpe.unity.game-scene.v1";
		public const string Events = "vpe.events.v1";
		public const string Outputs = "vpe.outputs.v1";
	}
}
