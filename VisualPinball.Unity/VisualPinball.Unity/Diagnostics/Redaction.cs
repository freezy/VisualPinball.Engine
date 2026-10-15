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
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Removes personal data from text that leaves the machine: the user's home folder and
	/// user name in paths.
	/// </summary>
	public static class Redaction
	{
		private static readonly Regex JsonString = new Regex(@"""((?:[^""\\]|\\.)*)""", RegexOptions.CultureInvariant);

		private static string _home;
		private static string _homeReplacement;
		private static Regex _userSegment;

		static Redaction()
		{
			Configure(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Environment.UserName, IsWindows());
		}

		/// <summary>
		/// Replaces the home folder with <c>%USERPROFILE%</c> (Windows) or <c>~</c>, and the
		/// user name where it appears as a folder under <c>Users</c> or <c>home</c>.
		/// </summary>
		public static string Redact(string text)
		{
			if (string.IsNullOrEmpty(text)) {
				return text;
			}
			if (!string.IsNullOrEmpty(_home)) {
				text = ReplaceHome(text, _home);
				text = ReplaceHome(text, _home.Replace('\\', '/'));
				text = ReplaceHome(text, _home.Replace('/', '\\'));
			}
			return _userSegment != null ? _userSegment.Replace(text, "$1<user>") : text;
		}

		/// <summary>
		/// <see cref="Redact"/> for JSON: each string value is redacted as the text it stands
		/// for, so escaped backslashes in Windows paths don't hide the home folder.
		/// </summary>
		public static string RedactJson(string json)
		{
			if (string.IsNullOrEmpty(json)) {
				return json;
			}
			return JsonString.Replace(json, match => {
				var value = Unescape(match.Groups[1].Value);
				var redacted = Redact(value);
				return redacted == value ? match.Value : "\"" + Escape(redacted) + "\"";
			});
		}

		/// <summary>
		/// Sets who to redact. Only for tests; the defaults come from the environment.
		/// </summary>
		internal static void Configure(string home, string userName, bool windows)
		{
			_home = string.IsNullOrEmpty(home) ? null : home.TrimEnd('\\', '/');
			_homeReplacement = windows ? "%USERPROFILE%" : "~";
			_userSegment = string.IsNullOrEmpty(userName)
				? null
				: new Regex(@"([\\/](?:Users|home)[\\/])" + Regex.Escape(userName) + @"(?=[\\/]|$|[\s""'])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		}

		private static string ReplaceHome(string text, string home)
		{
			var index = text.IndexOf(home, StringComparison.OrdinalIgnoreCase);
			while (index >= 0) {
				// only whole folder names: "C:\Users\ne" must not eat "C:\Users\neh", and
				// "/home/jane" must not match inside "/mnt/backup/home/jane"
				var end = index + home.Length;
				var startsPath = index == 0 || !char.IsLetterOrDigit(text[index - 1]) && text[index - 1] != '/' && text[index - 1] != '\\' && text[index - 1] != '.';
				var endsFolder = end == text.Length || text[end] == '\\' || text[end] == '/' || !char.IsLetterOrDigit(text[end]);
				if (startsPath && endsFolder) {
					text = text.Substring(0, index) + _homeReplacement + text.Substring(end);
					end = index + _homeReplacement.Length;
				}
				index = text.IndexOf(home, end, StringComparison.OrdinalIgnoreCase);
			}
			return text;
		}

		private static string Unescape(string text)
		{
			if (text.IndexOf('\\') < 0) {
				return text;
			}
			var sb = new StringBuilder(text.Length);
			for (var i = 0; i < text.Length; i++) {
				var c = text[i];
				if (c != '\\' || i + 1 >= text.Length) {
					sb.Append(c);
					continue;
				}
				var next = text[++i];
				switch (next) {
					case 'n': sb.Append('\n'); break;
					case 'r': sb.Append('\r'); break;
					case 't': sb.Append('\t'); break;
					case 'b': sb.Append('\b'); break;
					case 'f': sb.Append('\f'); break;
					case 'u' when i + 4 < text.Length && int.TryParse(text.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code):
						sb.Append((char)code);
						i += 4;
						break;
					default: sb.Append(next); break; // \\, \" and \/
				}
			}
			return sb.ToString();
		}

		private static string Escape(string text)
		{
			var sb = new StringBuilder(text.Length + 8);
			foreach (var c in text) {
				switch (c) {
					case '\\': sb.Append("\\\\"); break;
					case '"': sb.Append("\\\""); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c < ' ') {
							sb.Append("\\u").Append(((int)c).ToString("x4"));
						} else {
							sb.Append(c);
						}
						break;
				}
			}
			return sb.ToString();
		}

		private static bool IsWindows()
		{
			return Environment.OSVersion.Platform == PlatformID.Win32NT;
		}
	}
}
