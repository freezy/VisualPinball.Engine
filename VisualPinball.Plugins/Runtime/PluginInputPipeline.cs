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
	/// <summary>
	/// A chain of plugin input filters: each <see cref="PluginInputEvent"/> passes through every registered
	/// <see cref="IPluginInputFilter"/> in a deterministic order, and the owner of the pipeline forwards the final value.
	/// </summary>
	/// <remarks>
	/// Filters let plugins adjust input before the game sees it, for example to scale or dead-zone an analog nudge axis. The
	/// order is <c>order</c> ascending, then plugin ID, so the result does not depend on registration order. Each plugin can
	/// register one filter per pipeline. The pipeline itself does no threading, so register, unregister and apply on the
	/// same thread. The Player does not create a pipeline yet; the type is a building block for an input owner.
	/// <example>
	/// <code>
	/// sealed class NudgeScale : IPluginInputFilter
	/// {
	///     public PluginInputEvent Filter(in PluginInputEvent input) =&gt; input.Action == "nudge-x"
	///         ? new PluginInputEvent(input.Action, input.Value * 2, input.TimestampUs) : input;
	/// }
	///
	/// using var stage = pipeline.Register("my.nudge", 10, new NudgeScale());
	/// var filtered = pipeline.Apply(new PluginInputEvent("nudge-x", 0.25f, timestampUs));
	/// </code>
	/// </example>
	/// </remarks>
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
		/// <summary>Adds a plugin's filter to the chain until the returned handle is disposed.</summary>
		/// <param name="pluginId">The registering plugin's ID; also the tie-breaker between equal orders.</param>
		/// <param name="order">Position in the chain; lower values run first.</param>
		/// <param name="filter">The filter to run on every event.</param>
		/// <returns>A handle that removes the filter; dispose it with the plugin's game or application scope.</returns>
		/// <exception cref="InvalidOperationException">A filter is already registered for <paramref name="pluginId"/>.</exception>
		public IDisposable Register(string pluginId, int order, IPluginInputFilter filter)
		{
			if (_stages.Any(stage => stage.Id == pluginId)) throw new InvalidOperationException("Input filter already registered: " + pluginId);
			var entry = new Stage { Id = pluginId, Order = order, Filter = filter }; _stages.Add(entry); Rebuild();
			return new Subscription(() => { _stages.Remove(entry); Rebuild(); });
		}
		private void Rebuild() => _dispatch = _stages.OrderBy(stage => stage.Order).ThenBy(stage => stage.Id, StringComparer.Ordinal).ToArray();
		/// <summary>
		/// Runs an event through every filter in order, each receiving the previous filter's output, and returns the result.
		/// With no filters registered the input is returned unchanged. A filter's exception propagates to the caller.
		/// </summary>
		/// <param name="input">The raw input event.</param>
		/// <returns>The filtered event to deliver to the game.</returns>
		public PluginInputEvent Apply(in PluginInputEvent input)
		{
			var result = input;
			foreach (var stage in _dispatch) result = stage.Filter.Filter(result);
			return result;
		}
	}
}
