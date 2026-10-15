// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// How serious a plugin's log line is. Same meaning as in the host's log:
	/// <list type="bullet">
	/// <item><see cref="Error"/>: a bug in the plugin or an impossible state.</item>
	/// <item><see cref="Warn"/>: something the user can fix, like a missing file or a wrong path.</item>
	/// <item><see cref="Info"/>: what the plugin loaded or decided, once per game.</item>
	/// <item><see cref="Debug"/>: details for chasing a problem, off by default.</item>
	/// </list>
	/// </summary>
	public enum PluginLogLevel
	{
		Debug,
		Info,
		Warn,
		Error,
	}

	/// <summary>
	/// A plugin's log, provided by the host as <see cref="PluginApplicationContext.Logger"/>. Lines end up in the
	/// player's session log and, for errors, in error reports.
	/// </summary>
	public interface IPluginLog
	{
		/// <summary>
		/// Whether lines of this level are written. Check it before building an expensive message.
		/// </summary>
		bool IsEnabled(PluginLogLevel level);

		/// <summary>
		/// Writes a line. Thread-safe.
		/// </summary>
		void Write(PluginLogLevel level, string message, Exception? exception = null);
	}

	public static class PluginLogExtensions
	{
		public static void Debug(this IPluginLog log, string message) => log.Write(PluginLogLevel.Debug, message);

		public static void Info(this IPluginLog log, string message) => log.Write(PluginLogLevel.Info, message);

		public static void Warn(this IPluginLog log, string message, Exception? exception = null) => log.Write(PluginLogLevel.Warn, message, exception);

		public static void Error(this IPluginLog log, Exception? exception, string message) => log.Write(PluginLogLevel.Error, message, exception);
	}

	/// <summary>
	/// Adapts a plain text sink, for hosts that only have one. Every line is prefixed with its level.
	/// </summary>
	public sealed class TextPluginLog : IPluginLog
	{
		private readonly Action<string> _sink;

		public TextPluginLog(Action<string>? sink)
		{
			_sink = sink ?? (_ => { });
		}

		public bool IsEnabled(PluginLogLevel level) => level >= PluginLogLevel.Info;

		public void Write(PluginLogLevel level, string message, Exception? exception = null)
		{
			if (!IsEnabled(level)) {
				return;
			}
			var prefix = level == PluginLogLevel.Info ? "" : level.ToString().ToUpperInvariant() + ": ";
			_sink(prefix + message + (exception != null ? " (" + exception.GetType().Name + ": " + exception.Message + ")" : ""));
		}
	}
}
