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
using System.Text.RegularExpressions;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Decides which automatic error reports of a session are sent: the first of each kind, at
	/// most a few in total, but fatal ones always. Thread-safe.
	/// </summary>
	public sealed class ReportDedupe
	{
		public const int DefaultCap = 10;
		private const int StackFrames = 5;

		private static readonly Regex FrameLine = new(@"^\s*at\s+(?<method>[^(\[]+)", RegexOptions.Multiline);

		private readonly Dictionary<string, int> _repeats = new();
		private readonly HashSet<string> _fatalSent = new();
		private readonly int _cap;
		private int _sent;

		public ReportDedupe(int cap = DefaultCap)
		{
			_cap = cap;
		}

		/// <summary>
		/// Whether a report with this key should be sent.
		/// </summary>
		/// <param name="repeats">How often the key was seen before and not sent.</param>
		public bool ShouldSend(string key, bool fatal, out int repeats)
		{
			lock (_repeats) {
				var known = _repeats.TryGetValue(key, out var seen);
				// The first fatal one of a kind always goes, even when the same kind came before
				// as an error that wasn't sent (the cap was reached, say).
				if (fatal && _fatalSent.Add(key)) {
					_repeats[key] = known ? seen : 0;
					repeats = known ? seen : 0;
					return true;
				}
				if (known) {
					_repeats[key] = seen + 1;
					repeats = seen + 1;
					return false;
				}
				_repeats[key] = 0;
				repeats = 0;
				if (_sent >= _cap) {
					return false;
				}
				_sent++;
				return true;
			}
		}

		/// <summary>
		/// Forgets everything, for a new session.
		/// </summary>
		public void Reset()
		{
			lock (_repeats) {
				_repeats.Clear();
				_fatalSent.Clear();
				_sent = 0;
			}
		}

		/// <summary>
		/// What makes two reports the same: the exception type and where it was thrown, or the
		/// logger and the message template when there's no exception.
		/// </summary>
		public static string Key(Exception exception, string logger, string template)
		{
			if (exception == null) {
				return "log|" + logger + "|" + template;
			}
			var inner = exception.GetBaseException();
			var frames = FrameLine.Matches(inner.StackTrace ?? exception.StackTrace ?? "")
				.Cast<Match>()
				.Take(StackFrames)
				.Select(m => m.Groups["method"].Value.Trim());
			return "ex|" + inner.GetType().FullName + "|" + string.Join(";", frames);
		}
	}
}
