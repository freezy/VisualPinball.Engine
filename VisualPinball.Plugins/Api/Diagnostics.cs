// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// SPDX-License-Identifier: MIT
// Licensed under MIT. See https://opensource.org/license/mit.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace VisualPinball.Plugins
{
	/// <summary>
	/// Something that can describe what it loaded and how it's set up, for the session log and
	/// error reports. Gamelogic engines and plugins implement it so a developer can reproduce a
	/// user's problem: which ROM, which version, which files, which settings.
	/// </summary>
	public interface IDiagnosticsSource
	{
		/// <summary>
		/// Title of the section in reports, e.g. "PinMAME".
		/// </summary>
		string DiagnosticsSection { get; }

		/// <summary>
		/// Writes the current state. Called on the main thread when something changed and when a
		/// report is built. Keep it cheap: no file reads beyond checking whether a file exists.
		/// Paths are fine to include, they're redacted before anything leaves the machine.
		/// </summary>
		void Describe(DiagnosticsWriter writer);
	}

	/// <summary>
	/// Collects the key/value lines of one diagnostics section, in order.
	/// </summary>
	public sealed class DiagnosticsWriter
	{
		private static readonly string[] SecretWords = { "token", "secret", "password", "passwd", "license", "credential" };

		private readonly List<KeyValuePair<string, string>> _entries = new();

		/// <summary>
		/// The lines written so far.
		/// </summary>
		public IReadOnlyList<KeyValuePair<string, string>> Entries => _entries;

		public DiagnosticsWriter Add(string key, string value)
		{
			_entries.Add(new KeyValuePair<string, string>(key ?? "", value ?? ""));
			return this;
		}

		public DiagnosticsWriter Add(string key, bool value) => Add(key, value ? "yes" : "no");

		public DiagnosticsWriter Add(string key, long value) => Add(key, value.ToString(CultureInfo.InvariantCulture));

		public DiagnosticsWriter Add(string key, double value) => Add(key, value.ToString("0.###", CultureInfo.InvariantCulture));

		/// <summary>
		/// Adds a setting. Settings whose name suggests a secret (key, token, password...) are
		/// written as <c>&lt;set&gt;</c> or <c>&lt;empty&gt;</c>, never with their value.
		/// </summary>
		public DiagnosticsWriter AddSetting(string key, object value)
		{
			if (IsSecret(key)) {
				var empty = value == null || value is string s && s.Length == 0;
				return Add(key, empty ? "<empty>" : "<set>");
			}
			return Add(key, Convert.ToString(value, CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Whether a setting with this name holds a secret.
		/// </summary>
		public static bool IsSecret(string key)
		{
			if (string.IsNullOrEmpty(key)) {
				return false;
			}
			// "VniKey", "api_key", but not "keyBindings"
			if (key.EndsWith("key", StringComparison.OrdinalIgnoreCase)) {
				return true;
			}
			foreach (var word in SecretWords) {
				if (key.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) {
					return true;
				}
			}
			return false;
		}
	}
}
