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
using System.Text.RegularExpressions;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Removes personal data from text that leaves the machine: the user's home folder and
	/// user name in paths.
	/// </summary>
	public static class Redaction
	{
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

		private static bool IsWindows()
		{
			return Environment.OSVersion.Platform == PlatformID.Win32NT;
		}
	}
}
