// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Helpers for a settings choice that picks one of the game's displays as a plugin's input, such as "DMD source". The
	/// saved value is relative to the game's controller, so the same choice keeps working across sessions and can be kept
	/// while the game is stopped.
	/// </summary>
	/// <remarks>
	/// The DMD, PinUP and B2S plugins each declare a choice field whose provider is <c>sources</c>. Their
	/// <see cref="IPluginSettingsProvider.DiscoverAsync"/> answers that provider with <see cref="Displays"/>, and their
	/// prepare hook turns the saved value into a resource ID with <see cref="Resolve"/>.
	/// <example>
	/// <code>
	/// // settings provider, may run on a worker:
	/// if (provider == "sources") return PluginResourceChoices.Displays(request, DisplayEncoding.Gray2 | DisplayEncoding.Gray4);
	///
	/// // PrepareGameAsync, owner thread:
	/// string source = game.Settings.String("Source", "auto");
	/// if (source != "auto") selection.ResourceId = PluginResourceChoices.Resolve(game.ControllerId, source);
	/// </code>
	/// </example>
	/// </remarks>
	public static class PluginResourceChoices
	{
		/// <summary>
		/// Builds the choice catalog of displays a plugin can read: <c>auto</c> ("Automatic · this game's DMD") followed by every
		/// original DMD of the request's game that offers at least one of <paramref name="formats"/>.
		/// </summary>
		/// <remarks>
		/// Only root resources (no parents) with role <c>dmd</c>, view <see cref="ResourceView.Original"/> and the game's
		/// controller <c>&lt;game id&gt;/controller/0</c> are listed; each choice ID is the resource ID relative to that
		/// controller, such as <c>display/dmd0</c>. Without a game (global settings) only <c>auto</c> is offered. The catalog is
		/// marked incomplete (<see cref="PluginChoiceCatalog.IsComplete"/> false) unless the game's controller is currently
		/// published, so a saved display is not rejected as missing while the game is stopped. Pure function of the request;
		/// safe on any thread.
		/// </remarks>
		/// <param name="request">The settings request; its <see cref="PluginChoiceRequest.Resources"/> snapshot supplies the displays.</param>
		/// <param name="formats">The encodings the plugin can consume; displays offering none of them are left out.</param>
		/// <returns>The catalog; its revision changes whenever the listed IDs change.</returns>
		public static PluginChoiceCatalog Displays(PluginChoiceRequest request, DisplayEncoding formats)
		{
			var automatic = new PluginChoice("auto", "Automatic · this game's DMD");
			if (!request.Game.HasValue) return new PluginChoiceCatalog("global", new[] { automatic });
			string controller = request.Game.Value.Id + "/controller/0";
			var choices = request.Resources.Where(resource => resource.Kind == ResourceKind.Display && resource.View == ResourceView.Original
				&& resource.Parents.Count == 0 && resource.Controller == controller && resource.Role == "dmd" && (resource.Formats & formats) != 0)
				.Select(resource => new PluginChoice(resource.Id.Substring(controller.Length + 1), resource.Label)).Prepend(automatic).ToArray();
			return new PluginChoiceCatalog(string.Join("|", choices.Select(choice => choice.Id)), choices, request.Resources.Any(resource => resource.Kind == ResourceKind.Controller && resource.Controller == controller));
		}
		/// <summary>
		/// Turns a saved relative display choice into the absolute resource ID to put in
		/// <see cref="PluginResourceSelection.ResourceId"/>. Do not pass <c>auto</c>; leave the selection's ID empty instead.
		/// </summary>
		/// <param name="controller">The running game's controller ID, <see cref="PluginGameContext.ControllerId"/>.</param>
		/// <param name="relative">The saved choice, such as <c>display/dmd0</c>.</param>
		/// <returns><paramref name="controller"/> and <paramref name="relative"/> joined by a slash.</returns>
		/// <exception cref="ArgumentException">
		/// <paramref name="relative"/> does not start with <c>display/</c> or contains <c>..</c>, <c>:</c> or a backslash, so a
		/// tampered setting cannot point at another game's or another kind of resource.
		/// </exception>
		public static string Resolve(string controller, string relative)
		{
			if (!relative.StartsWith("display/", StringComparison.Ordinal) || relative.Contains("..") || relative.Contains(":") || relative.Contains("\\"))
				throw new ArgumentException("Select a display belonging to this game's controller.");
			return controller + "/" + relative;
		}
	}
}
