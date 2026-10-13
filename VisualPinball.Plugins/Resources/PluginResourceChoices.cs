// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// See LICENSE for the terms of use and distribution.

using System;
using System.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>Saved resource choices are relative to a game's controller and can be kept while that game is stopped.</summary>
	public static class PluginResourceChoices
	{
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
		public static string Resolve(string controller, string relative)
		{
			if (!relative.StartsWith("display/", StringComparison.Ordinal) || relative.Contains("..") || relative.Contains(":") || relative.Contains("\\"))
				throw new ArgumentException("Select a display belonging to this game's controller.");
			return controller + "/" + relative;
		}
	}
}
