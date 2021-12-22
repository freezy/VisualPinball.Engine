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
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace VisualPinball.Unity.Editor
{
	public class LabelsHandler
	{
		private List<PinballLabel> _pinballLabels = new List<PinballLabel>();

		public string[] Categories => _pinballLabels.Select(L => L.Category).Distinct().ToArray();

		public LabelsHandler() { }

		public void AddLabels(IEnumerable<string> labels)
		{
			_pinballLabels.Union(labels.Select(L => new PinballLabel(L)));
		}

		public string[] GetLabels(string category) => _pinballLabels.Where(L => string.IsNullOrEmpty(category) || L.Category.Equals(category, StringComparison.InvariantCultureIgnoreCase))
																	.Select(L => L.Label).ToArray();
	

	}
}
