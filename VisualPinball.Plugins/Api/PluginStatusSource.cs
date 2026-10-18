// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System.Collections.Generic;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// A running plugin that tells the player what it does in the current game, in a few short lines the
	/// Player shows in its pause menu: the DMD plugin which colorization it loaded, PinUP which pack, B2S
	/// which backglass.
	/// </summary>
	/// <remarks>
	/// Unlike <see cref="IDiagnosticsSource"/>, which is for developers, this is for the player at the table:
	/// plain words, file names rather than paths, nothing about internals. A plugin that writes nothing has
	/// nothing to do in this game (PinMAME on a table without a ROM), and the pause menu leaves it out; a
	/// running plugin that doesn't implement this is listed with its name only.
	/// </remarks>
	public interface IPluginStatusSource
	{
		/// <summary>
		/// Writes the lines, such as <c>Colorization</c> → <c>Serum, t2_l8.cRZ</c>. Called on the owner
		/// thread when the pause menu opens, so keep it cheap: no file reads.
		/// </summary>
		void DescribeStatus(PluginStatusWriter writer);
	}

	/// <summary>Collects the label/value lines of a plugin's status, in order.</summary>
	public sealed class PluginStatusWriter
	{
		private readonly List<KeyValuePair<string, string>> _lines = new();

		/// <summary>The lines written so far.</summary>
		public IReadOnlyList<KeyValuePair<string, string>> Lines => _lines;

		public PluginStatusWriter Add(string label, string value)
		{
			_lines.Add(new KeyValuePair<string, string>(label ?? "", value ?? ""));
			return this;
		}

		public PluginStatusWriter Add(string label, bool on) => Add(label, on ? "on" : "off");
	}

	/// <summary>One plugin as the pause menu shows it: what it is, its state, and its status lines.</summary>
	public sealed class PluginStatusReport
	{
		public PluginDescriptor Descriptor { get; }
		public PluginState State { get; }
		/// <summary>Why the plugin isn't running, for a blocked or faulted one; empty otherwise.</summary>
		public string Message { get; }
		public IReadOnlyList<KeyValuePair<string, string>> Lines { get; }

		public PluginStatusReport(PluginDescriptor descriptor, PluginState state, string message, IReadOnlyList<KeyValuePair<string, string>> lines)
		{
			Descriptor = descriptor;
			State = state;
			Message = message ?? "";
			Lines = lines;
		}
	}
}
