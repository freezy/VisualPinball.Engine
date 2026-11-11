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

namespace VisualPinball.Unity.Editor
{
	internal static class AssetQueryTokenizer
	{
		public static ParsedAssetQuery Parse(string query)
		{
			var parsed = new ParsedAssetQuery();
			var keywords = new List<string>();
			var index = 0;
			while (index < query.Length) {
				SkipWhitespace(query, ref index);
				if (index >= query.Length) {
					break;
				}

				if (query[index] == '[') {
					if (TryReadDelimited(query, ref index, '[', ']', out var tag)) {
						if (!string.IsNullOrWhiteSpace(tag)) {
							parsed.Tags.Add(tag);
						}
						continue;
					}
				}

				if (query[index] == '(') {
					if (TryReadDelimited(query, ref index, '(', ')', out var quality)) {
						if (string.IsNullOrWhiteSpace(quality)) {
							keywords.Add($"({quality})");
						} else if (parsed.Quality == null) {
							parsed.Quality = quality;
						} else {
							keywords.Add($"({quality})");
						}
						continue;
					}
				}

				var termStart = index;
				var keyOrKeyword = ReadToken(query, ref index, true);
				if (index < query.Length && query[index] == ':') {
					index++;
					var value = ReadToken(query, ref index, false);
					if (keyOrKeyword.Length > 0 && value.Length > 0) {
						parsed.AddAttribute(keyOrKeyword, value);
					} else {
						keywords.Add(query.Substring(termStart, index - termStart));
					}
				} else if (keyOrKeyword.Length > 0) {
					keywords.Add(keyOrKeyword);
				} else {
					index++;
				}
			}

			parsed.Keywords = string.Join(" ", keywords.Where(keyword => keyword.Length > 0));
			return parsed;
		}

		private static string ReadToken(string query, ref int index, bool stopAtColon)
		{
			if (index >= query.Length) {
				return string.Empty;
			}
			if (query[index] != '"') {
				var start = index;
				while (index < query.Length && !char.IsWhiteSpace(query[index]) && (!stopAtColon || query[index] != ':')) {
					index++;
				}
				return query.Substring(start, index - start);
			}

			index++;
			var value = new StringBuilder();
			while (index < query.Length) {
				var character = query[index++];
				if (character == '"') {
					break;
				}
				if (character == '\\' && index < query.Length && (query[index] == '\\' || query[index] == '"')) {
					value.Append(query[index++]);
				} else {
					value.Append(character);
				}
			}
			return value.ToString();
		}

		private static bool TryReadDelimited(string query, ref int index, char opening, char closing, out string value)
		{
			var closingIndex = query.IndexOf(closing, index + 1);
			if (closingIndex < 0) {
				value = null;
				return false;
			}
			value = query.Substring(index + 1, closingIndex - index - 1);
			index = closingIndex + 1;
			return true;
		}

		private static void SkipWhitespace(string query, ref int index)
		{
			while (index < query.Length && char.IsWhiteSpace(query[index])) {
				index++;
			}
		}
	}

	internal sealed class ParsedAssetQuery
	{
		public string Keywords = string.Empty;
		public string Quality;
		public readonly Dictionary<string, HashSet<string>> Attributes = new(StringComparer.OrdinalIgnoreCase);
		public readonly HashSet<string> Tags = new(StringComparer.OrdinalIgnoreCase);

		public void AddAttribute(string key, string value)
		{
			if (!Attributes.TryGetValue(key, out var values)) {
				values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				Attributes[key] = values;
			}
			values.Add(value);
		}
	}
}
