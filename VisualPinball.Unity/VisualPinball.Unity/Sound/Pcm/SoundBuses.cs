// Visual Pinball Engine. Copyright (C) 2026 freezy and VPE Team.
// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	/// <summary>Portable routing identities, independent of Unity mixer assets and device channels.</summary>
	public static class SoundBuses
	{
		public const string Transients = "mechanical.transients", Rolling = "mechanical.rolling", Rom = "game.rom",
			Music = "table.music", Callouts = "table.callouts", Synthetic = "table.synthetic", Ui = "player.ui";
		private static readonly string[] BusIds = { Transients, Rolling, Rom, Music, Callouts, Synthetic, Ui };
		public static IReadOnlyList<string> Ids { get; } = Array.AsReadOnly(BusIds);
		public static IReadOnlyList<string> Labels { get; } = Array.AsReadOnly(new[] { "Mechanical transients", "Rolling", "ROM processing trim", "Table music", "Table callouts", "Table effects", "Player UI" });
		public static int Index(string id) => Array.IndexOf(BusIds, id);
		public static string Default(SoundAsset asset) => asset is MusicAsset ? Music : asset is CalloutAsset ? Callouts :
			asset is SoundEffectAsset effect && effect.Type == SoundEffectType.Mechanical ? Transients : Synthetic;
		public static string Resolve(SoundAsset asset, string automaticOverride = null)
			=> !string.IsNullOrEmpty(asset.BusId) && Index(asset.BusId) >= 0 ? asset.BusId : automaticOverride ?? Default(asset);
	}
}
