// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NLog;
using VisualPinball.Plugins;

namespace VisualPinball.Unity
{
	/// <summary>
	/// What a developer needs to reproduce a problem, in named sections: the system, the
	/// build, the loaded table, the gamelogic engine, plugins, settings, performance.
	/// </summary>
	///
	/// <remarks>
	/// Sections are written on the main thread when something changes (a table loads, a
	/// plugin starts), never polled. Each change publishes a new immutable snapshot, so error
	/// reporting can read <see cref="Sections"/> from any thread. Every change is also logged,
	/// so the session log tells the same story.
	/// </remarks>
	public static class DiagnosticsContext
	{
		/// <summary>
		/// One named group of key/value lines.
		/// </summary>
		public sealed class Section
		{
			public string Name { get; }
			public IReadOnlyList<KeyValuePair<string, string>> Entries { get; }

			public Section(string name, IReadOnlyList<KeyValuePair<string, string>> entries)
			{
				Name = name;
				Entries = entries;
			}

			public string Get(string key) => Entries.FirstOrDefault(e => e.Key == key).Value;
		}

		public const string System = "System";
		public const string Build = "Build";
		public const string Table = "Table";
		public const string Gamelogic = "Gamelogic";
		public const string Plugins = "Plugins";
		public const string Settings = "Settings";
		public const string Performance = "Performance";

		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
		private static readonly string[] Order = { System, Build, Table, Gamelogic, Plugins, Settings, Performance };
		private static readonly object Lock = new();
		private static volatile IReadOnlyList<Section> _sections = Array.Empty<Section>();

		/// <summary>
		/// The current sections, in a stable order. Safe to read from any thread.
		/// </summary>
		public static IReadOnlyList<Section> Sections => _sections;

		/// <summary>
		/// Raised after a section changed.
		/// </summary>
		public static event Action<Section> Changed;

		/// <summary>
		/// Replaces a section.
		/// </summary>
		/// <param name="log">Whether to write the section to the log. Off for sections that
		/// change often and log their own summary, like performance.</param>
		public static void Set(string name, DiagnosticsWriter writer, bool log = true)
		{
			var section = new Section(name, writer.Entries.ToArray());
			lock (Lock) {
				var sections = _sections.Where(s => s.Name != name).Append(section)
					.OrderBy(s => Rank(s.Name))
					.ThenBy(s => s.Name, StringComparer.Ordinal)
					.ToArray();
				_sections = sections;
			}
			if (log) {
				Logger.Info("{Section:l}:\n{Details:l}", name, ToText(section, "  "));
			}
			Changed?.Invoke(section);
		}

		/// <summary>
		/// Replaces the section a source describes.
		/// </summary>
		public static void Set(IDiagnosticsSource source, bool log = true)
		{
			var writer = new DiagnosticsWriter();
			try {
				source.Describe(writer);
			} catch (Exception e) {
				writer.Add("error", "describing failed: " + e.Message);
				Logger.Error(e, "{Source:l} failed to describe itself for diagnostics.", source.GetType().FullName);
			}
			Set(source.DiagnosticsSection, writer, log);
		}

		/// <summary>
		/// Returns a section, or null.
		/// </summary>
		public static Section Get(string name) => _sections.FirstOrDefault(s => s.Name == name);

		/// <summary>
		/// Removes a section, e.g. the table's when it's unloaded.
		/// </summary>
		public static void Remove(string name)
		{
			lock (Lock) {
				_sections = _sections.Where(s => s.Name != name).ToArray();
			}
		}

		/// <summary>
		/// Removes everything. Called when a session starts.
		/// </summary>
		public static void Clear()
		{
			lock (Lock) {
				_sections = Array.Empty<Section>();
			}
		}

		/// <summary>
		/// The sections as text, one "key: value" per line.
		/// </summary>
		public static string ToText(IReadOnlyList<Section> sections = null)
		{
			var sb = new StringBuilder();
			foreach (var section in sections ?? _sections) {
				sb.Append("== ").Append(section.Name).Append(" ==\n");
				sb.Append(ToText(section, ""));
			}
			return sb.ToString();
		}

		/// <summary>
		/// The sections as a JSON object of objects.
		/// </summary>
		/// <param name="transform">Applied to every key and value before it's escaped, e.g.
		/// <see cref="Redaction.Redact"/>. Redacting the JSON afterwards would miss paths whose
		/// backslashes are escaped.</param>
		public static string ToJson(IReadOnlyList<Section> sections = null, Func<string, string> transform = null)
		{
			transform ??= s => s;
			var sb = new StringBuilder("{\n");
			var first = true;
			foreach (var section in sections ?? _sections) {
				if (!first) {
					sb.Append(",\n");
				}
				first = false;
				sb.Append("  ").Append(Quote(transform(section.Name))).Append(": {");
				for (var i = 0; i < section.Entries.Count; i++) {
					sb.Append(i == 0 ? "\n" : ",\n");
					sb.Append("    ").Append(Quote(transform(section.Entries[i].Key))).Append(": ").Append(Quote(transform(section.Entries[i].Value)));
				}
				sb.Append(section.Entries.Count > 0 ? "\n  }" : "}");
			}
			return sb.Append("\n}\n").ToString();
		}

		private static string ToText(Section section, string indent)
		{
			var sb = new StringBuilder();
			foreach (var entry in section.Entries) {
				sb.Append(indent).Append(entry.Key).Append(": ").Append(entry.Value).Append('\n');
			}
			return sb.ToString();
		}

		private static int Rank(string name)
		{
			var index = Array.IndexOf(Order, name);
			return index < 0 ? Order.Length : index;
		}

		private static string Quote(string value)
		{
			var sb = new StringBuilder("\"");
			foreach (var c in value ?? "") {
				switch (c) {
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c < 0x20) {
							sb.Append("\\u").Append(((int)c).ToString("x4"));
						} else {
							sb.Append(c);
						}
						break;
				}
			}
			return sb.Append('"').ToString();
		}
	}
}
