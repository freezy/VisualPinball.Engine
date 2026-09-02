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
using System.IO;
using System.Text.RegularExpressions;

namespace VisualPinball.Unity
{
	public static class PackagedContentPath
	{
		private static readonly Regex DrivePrefix = new("^[A-Za-z]:", RegexOptions.CultureInvariant);

		public static string ValidateRelative(string path, string description = "content path")
		{
			if (string.IsNullOrWhiteSpace(path)) {
				throw new InvalidDataException($"The {description} is empty.");
			}
			if (path.IndexOf('\\') >= 0) {
				throw new InvalidDataException($"The {description} '{path}' contains a backslash; package paths must use '/'.");
			}
			if (path.IndexOf(':') >= 0) {
				throw new InvalidDataException($"The {description} '{path}' contains ':' (drive prefixes and alternate data streams are forbidden).");
			}
			if (Path.IsPathRooted(path) || path.StartsWith("/", StringComparison.Ordinal) || DrivePrefix.IsMatch(path)) {
				throw new InvalidDataException($"The {description} '{path}' is absolute.");
			}

			var parts = path.Split('/');
			foreach (var part in parts) {
				if (part.Length == 0 || part == "." || part == "..") {
					throw new InvalidDataException($"The {description} '{path}' contains an empty, '.' or '..' segment.");
				}
			}
			return string.Join("/", parts);
		}

		public static string GetContainedPath(string root, string relativePath)
		{
			var safeRelativePath = ValidateRelative(relativePath);
			var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var candidate = Path.GetFullPath(Path.Combine(fullRoot, safeRelativePath.Replace('/', Path.DirectorySeparatorChar)));
			var prefix = fullRoot + Path.DirectorySeparatorChar;
			var comparison = OperatingSystemPathComparison;
			if (!candidate.StartsWith(prefix, comparison)) {
				throw new InvalidDataException($"Content path '{relativePath}' escapes cache root '{fullRoot}'.");
			}
			return candidate;
		}

		public static StringComparer FileSystemNameComparer =>
			Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		private static StringComparison OperatingSystemPathComparison =>
			Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
	}
}
