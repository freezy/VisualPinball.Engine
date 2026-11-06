// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Threading;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Optional on an <see cref="IPlayerPluginFactory"/> (or its <see cref="IPlayerPluginFactory.SettingsProvider"/>): what the
	/// plugin finds on this computer and uses while its own settings are empty, such as another application's folders or
	/// configuration files. The Player shows the findings when it starts for the first time, so the user sees where their
	/// defaults come from and can replace them with the player's own values.
	/// </summary>
	public interface IPluginSystemDefaults
	{
		/// <summary>
		/// Looks for the plugin's defaults on this computer. Called on a worker thread, also while the plugin is switched off.
		/// Only reads: never writes another application's files or settings.
		/// </summary>
		/// <param name="settings">The plugin's effective global settings.</param>
		/// <param name="cancellation">Cancelled when the Player no longer needs the result.</param>
		IReadOnlyList<PluginSystemDefault> FindSystemDefaults(PluginSettingsSnapshot settings, CancellationToken cancellation);
	}

	/// <summary>One thing a plugin found on this computer, or looked for and didn't find.</summary>
	public sealed class PluginSystemDefault
	{
		/// <param name="title">What it is, such as <c>AltSound</c>.</param>
		/// <param name="source">Where it comes from, such as the application that set it up: <c>VPinMAME</c>.</param>
		public PluginSystemDefault(string title, string source)
		{
			Title = title ?? throw new ArgumentNullException(nameof(title));
			Source = source ?? "";
		}

		/// <summary>What it is, such as <c>AltSound</c>.</summary>
		public string Title { get; }
		/// <summary>Where it comes from, such as the application that set it up: <c>VPinMAME</c>.</summary>
		public string Source { get; }
		/// <summary>The folder or file found, or empty when nothing was found.</summary>
		public string Path { get; set; } = "";
		/// <summary>Whether something was found. Without it, <see cref="Description"/> says what was looked for.</summary>
		public bool Found => Path.Length > 0;
		/// <summary>One line on what was found and what it is for, such as <c>12 replacement sound packs</c>.</summary>
		public string Description { get; set; } = "";
		/// <summary>
		/// The plugin's global setting that replaces this default when set (a <see cref="SettingKind.Folder"/> or
		/// <see cref="SettingKind.File"/> field), or null when the default can only be shown.
		/// </summary>
		public string? FieldId { get; set; }
		/// <summary>
		/// Whether the plugin uses this default by itself while <see cref="FieldId"/> is empty (the default). When false, the Player
		/// saves the found path into <see cref="FieldId"/> once the user accepts the findings.
		/// </summary>
		public bool UsedWhenEmpty { get; set; } = true;
		/// <summary>Where on the desktop the found configuration puts the plugin's cabinet outputs.</summary>
		public IReadOnlyList<PluginSuggestedOutput> Outputs { get; set; } = Array.Empty<PluginSuggestedOutput>();
	}

	/// <summary>A cabinet output's placement in another application's configuration, in desktop pixels.</summary>
	public readonly struct PluginSuggestedOutput
	{
		public PluginSuggestedOutput(CabinetOutputRole role, int x, int y, int width, int height)
		{
			Role = role; X = x; Y = y; Width = width; Height = height;
		}

		public CabinetOutputRole Role { get; }
		/// <summary>Left edge, in virtual desktop pixels (negative left of the primary monitor).</summary>
		public int X { get; }
		/// <summary>Top edge, in virtual desktop pixels.</summary>
		public int Y { get; }
		public int Width { get; }
		public int Height { get; }
	}
}
