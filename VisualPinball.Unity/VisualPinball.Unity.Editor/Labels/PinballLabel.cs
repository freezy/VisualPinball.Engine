// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
using System.Diagnostics;
using UnityEngine;

namespace VisualPinball.Unity.Editor
{
	/// <summary>
	/// Expose classic Unity's labels (strings) as categorized labels
	/// Provide helpers to split directly a string into PinballLabel parts following proper format.
	/// </summary>
	[DebuggerDisplay("Category = {Category}, Path = {Path}, Label = {Label}")]
	[Serializable]
	public class PinballLabel : ISerializationCallbackReceiver
	{
		public static readonly char Separator = '.';

		[SerializeField]
		private string _fullLabel = string.Empty;
		public string FullLabel
		{
			get { return _fullLabel; }
			set { _fullLabel = value; Build(); }
		}

		[field: NonSerialized]
		public bool Editable = true;

		[field:NonSerialized]
		public string Label { get; private set; } = string.Empty;
		[field: NonSerialized]
		public string Path { get; private set; } = string.Empty;
		[field: NonSerialized]
		public string Category { get; private set; } = string.Empty;

		public PinballLabel(string label)
		{
			_fullLabel = label;
			Build();
		}

		private void Build()
		{
			if (string.IsNullOrEmpty(_fullLabel)) {
				Label = string.Empty;
				Category = string.Empty;
				return;
			}
			var tuple = Split(_fullLabel);
			Category = tuple.Item1;
			Path = tuple.Item2;
			Label = tuple.Item3;
		}

		internal static Tuple<string, string, string> Split(string fullLabel)
		{
			var split = fullLabel.Split(Separator);
			string category = string.Empty, path = string.Empty, label;

			if (split.Length > 1) {
				category = split[0];
				if (split.Length > 2) {
					path = string.Join(Separator, split, 1, split.Length - 2);
				} 
				label = split[split.Length - 1];
			} else {
				label = fullLabel;
			}

			return new Tuple<string, string, string>(category, path, label);
		}

		public void OnBeforeSerialize()		{}

		public void OnAfterDeserialize() { Build(); }

		public new string ToString()
		{
			return FullLabel;
		}
	}
}
