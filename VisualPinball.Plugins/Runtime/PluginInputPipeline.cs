// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualPinball.Plugins
{
	/// <summary>Ordered filters transform one input value; observers receive the resulting immutable event.</summary>
	public sealed class PluginInputPipeline
	{
		private sealed class Stage
		{
			internal string Id = "";
			internal int Order;
			internal IPluginInputFilter Filter = null!;
		}
		private readonly List<Stage> _stages = new List<Stage>();
		private Stage[] _dispatch = Array.Empty<Stage>();
		public IDisposable Register(string pluginId, int order, IPluginInputFilter filter)
		{
			if (_stages.Any(stage => stage.Id == pluginId)) throw new InvalidOperationException("Input filter already registered: " + pluginId);
			var entry = new Stage { Id = pluginId, Order = order, Filter = filter }; _stages.Add(entry); Rebuild();
			return new Subscription(() => { _stages.Remove(entry); Rebuild(); });
		}
		private void Rebuild() => _dispatch = _stages.OrderBy(stage => stage.Order).ThenBy(stage => stage.Id, StringComparer.Ordinal).ToArray();
		public PluginInputEvent Apply(in PluginInputEvent input)
		{
			var result = input;
			foreach (var stage in _dispatch) result = stage.Filter.Filter(result);
			return result;
		}
	}
}
