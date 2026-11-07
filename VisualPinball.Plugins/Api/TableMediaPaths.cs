// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
// SPDX-License-Identifier: MIT

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;

namespace VisualPinball.Plugins
{
	/// <summary>Locates VPX-compatible media paths, skipping ambiguous case-insensitive matches.</summary>
	public static class TableMediaPaths
	{
		private static readonly char[] InvalidNameCharacters = { '/', '\\', ':', '\0' };

		/// <summary>Existing game directories, with the controller namespace taking precedence over a bare game key.</summary>
		public static IEnumerable<string> GameDirectories(string? root, GameIdentity game)
		{
			if (!IsName(game.Provider) || !IsName(game.Key)) yield break;
			var scoped = FindDirectory(root, game.Provider, game.Key);
			if (scoped != null) yield return scoped;
			var unscoped = FindDirectory(root, game.Key);
			if (unscoped != null) yield return unscoped;
		}

		/// <summary>Finds an existing child directory by matching each component separately.</summary>
		public static string? FindDirectory(string? root, params string[] names)
		{
			if (string.IsNullOrWhiteSpace(root) || names.Length == 0) return null;
			foreach (string name in names) {
				root = FindChild(root, name, directoryOnly: true);
				if (root == null) return null;
			}
			return root;
		}

		/// <summary>Finds a file directly in the given directory.</summary>
		public static string? FindFile(string? directory, string name) => FindChild(directory, name, directoryOnly: false);

		private static bool IsName(string? name) => !string.IsNullOrWhiteSpace(name) && name != "." && name != ".."
			&& name.IndexOfAny(InvalidNameCharacters) < 0;

		private static string? FindChild(string? directory, string name, bool directoryOnly)
		{
			if (string.IsNullOrWhiteSpace(directory) || !IsName(name)) return null;
			try {
				string? match = null;
				var entries = directoryOnly ? Directory.EnumerateDirectories(directory) : Directory.EnumerateFiles(directory);
				foreach (string path in entries) {
					string actual = Path.GetFileName(path);
					if (!string.Equals(actual, name, StringComparison.OrdinalIgnoreCase)) continue;
					if (match != null) return null;
					match = path;
				}
				return match;
			} catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
			                                || error is ArgumentException || error is NotSupportedException) {
				return null;
			}
		}
	}
}
