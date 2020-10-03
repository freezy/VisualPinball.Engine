// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

// ReSharper disable StringLiteralTypo
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace VisualPinball.Engine.Common
{
	public static class StringExtensions
	{
		private static readonly Dictionary<string, string> ForeignCharacters = new Dictionary<string, string>
		{
			{"äæǽ", "ae"},
			{"öœ", "oe"},
			{"ü", "ue"},
			{"Ä", "Ae"},
			{"Ü", "Ue"},
			{"Ö", "Oe"},
			{"ÀÁÂÃÄÅǺĀĂĄǍΑΆẢẠẦẪẨẬẰẮẴẲẶА", "A"},
			{"àáâãåǻāăąǎªαάảạầấẫẩậằắẵẳặа", "a"},
			{"Б", "B"},
			{"б", "b"},
			{"ÇĆĈĊČ", "C"},
			{"çćĉċč", "c"},
			{"Д", "D"},
			{"д", "d"},
			{"ÐĎĐΔ", "Dj"},
			{"ðďđδ", "dj"},
			{"ÈÉÊËĒĔĖĘĚΕΈẼẺẸỀẾỄỂỆЕЭ", "E"},
			{"èéêëēĕėęěέεẽẻẹềếễểệеэ", "e"},
			{"Ф", "F"},
			{"ф", "f"},
			{"ĜĞĠĢΓГҐ", "G"},
			{"ĝğġģγгґ", "g"},
			{"ĤĦ", "H"},
			{"ĥħ", "h"},
			{"ÌÍÎÏĨĪĬǏĮİΗΉΊΙΪỈỊИЫ", "I"},
			{"ìíîïĩīĭǐįıηήίιϊỉịиыї", "i"},
			{"Ĵ", "J"},
			{"ĵ", "j"},
			{"ĶΚК", "K"},
			{"ķκк", "k"},
			{"ĹĻĽĿŁΛЛ", "L"},
			{"ĺļľŀłλл", "l"},
			{"М", "M"},
			{"м", "m"},
			{"ÑŃŅŇΝН", "N"},
			{"ñńņňŉνн", "n"},
			{"ÒÓÔÕŌŎǑŐƠØǾΟΌΩΏỎỌỒỐỖỔỘỜỚỠỞỢО", "O"},
			{"òóôõōŏǒőơøǿºοόωώỏọồốỗổộờớỡởợо", "o"},
			{"П", "P"},
			{"п", "p"},
			{"ŔŖŘΡР", "R"},
			{"ŕŗřρр", "r"},
			{"ŚŜŞȘŠΣС", "S"},
			{"śŝşșšſσςс", "s"},
			{"ȚŢŤŦτТ", "T"},
			{"țţťŧт", "t"},
			{"ÙÚÛŨŪŬŮŰŲƯǓǕǗǙǛŨỦỤỪỨỮỬỰУ", "U"},
			{"ùúûũūŭůűųưǔǖǘǚǜυύϋủụừứữửựу", "u"},
			{"ÝŸŶΥΎΫỲỸỶỴЙ", "Y"},
			{"ýÿŷỳỹỷỵй", "y"},
			{"В", "V"},
			{"в", "v"},
			{"Ŵ", "W"},
			{"ŵ", "w"},
			{"ŹŻŽΖЗ", "Z"},
			{"źżžζз", "z"},
			{"ÆǼ", "AE"},
			{"ß", "ss"},
			{"Ĳ", "IJ"},
			{"ĳ", "ij"},
			{"Œ", "OE"},
			{"ƒ", "f"},
			{"ξ", "ks"},
			{"π", "p"},
			{"β", "v"},
			{"μ", "m"},
			{"ψ", "ps"},
			{"Ё", "Yo"},
			{"ё", "yo"},
			{"Є", "Ye"},
			{"є", "ye"},
			{"Ї", "Yi"},
			{"Ж", "Zh"},
			{"ж", "zh"},
			{"Х", "Kh"},
			{"х", "kh"},
			{"Ц", "Ts"},
			{"ц", "ts"},
			{"Ч", "Ch"},
			{"ч", "ch"},
			{"Ш", "Sh"},
			{"ш", "sh"},
			{"Щ", "Shch"},
			{"щ", "shch"},
			{"ЪъЬь", ""},
			{"Ю", "Yu"},
			{"ю", "yu"},
			{"Я", "Ya"},
			{"я", "ya"},
		};

		public static string RemoveDiacritics(this string s)
		{
			var text = "";
			foreach (var c in s) {
				var len = text.Length;
				foreach (var entry in ForeignCharacters) {
					if (entry.Key.IndexOf(c) != -1) {
						text += entry.Value;
						break;
					}
				}

				if (len == text.Length) {
					text += c;
				}
			}

			return text;
		}

		public static string ToNormalizedName(this string name)
		{
			return Regex.Replace(name.RemoveDiacritics(), @"[^.\w\d-]+", "_").Trim('_').ToLower();
		}
	}
}
