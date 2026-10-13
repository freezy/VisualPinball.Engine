// SPDX-License-Identifier: MIT
using UnityEngine;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>Player applications claim integration ownership before automatic native bridges can start.</summary>
	public static class PlayerIntegrationMode
	{
		public static bool HostManaged { get; private set; }
		public static void Claim() => HostManaged = true;
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Reset()
		{
			HostManaged = false;
			PlayerPluginRegistry.Reset();
			PackagedGameIdentityReaders.Reset();
			PluginSettingsViews.Reset();
		}
	}
}
