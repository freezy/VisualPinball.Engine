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

using System.Text;
using NLog;
using NLog.Targets;

namespace VisualPinball.Unity
{
	/// <summary>
	/// The last lines logged in this session, kept in memory and attached to error reports.
	/// </summary>
	internal static class LogTail
	{
		public const int Capacity = 2000;
		public const int DefaultMaxChars = 1_000_000;

		private static readonly string[] Lines = new string[Capacity];
		private static readonly object Lock = new object();
		private static int _next;
		private static int _count;

		public static void Append(string line)
		{
			lock (Lock) {
				Lines[_next] = line;
				_next = (_next + 1) % Capacity;
				if (_count < Capacity) {
					_count++;
				}
			}
		}

		public static void Clear()
		{
			lock (Lock) {
				System.Array.Clear(Lines, 0, Capacity);
				_next = 0;
				_count = 0;
			}
		}

		/// <summary>
		/// Returns the newest lines that fit into <paramref name="maxChars"/>, oldest first.
		/// </summary>
		public static string Snapshot(int maxChars)
		{
			lock (Lock) {
				// walk back from the newest line until the budget is used up
				var take = 0;
				var chars = 0;
				while (take < _count) {
					var line = Lines[(_next - 1 - take + Capacity) % Capacity];
					if (chars + line.Length + 1 > maxChars) {
						break;
					}
					chars += line.Length + 1;
					take++;
				}
				var sb = new StringBuilder(chars);
				for (var i = take - 1; i >= 0; i--) {
					sb.Append(Lines[(_next - 1 - i + Capacity) % Capacity]).Append('\n');
				}
				return sb.ToString();
			}
		}
	}

	[Target("VpeTail")]
	internal sealed class LogTailTarget : TargetWithLayout
	{
		protected override void Write(LogEventInfo logEvent)
		{
			LogTail.Append(RenderLogEvent(Layout, logEvent));
		}
	}
}
