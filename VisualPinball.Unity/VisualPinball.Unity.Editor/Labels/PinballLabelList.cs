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
using System.Diagnostics;
using System.Linq;

namespace VisualPinball.Unity.Editor
{
	[DebuggerTypeProxy(typeof(PinballLabelListDebugView))]
	public class PinballLabelList : HashSet<PinballLabel>
	{
		private class LabelComparer : IEqualityComparer<PinballLabel>
		{
			public bool Equals(PinballLabel l1, PinballLabel l2)
			{
				return l1.FullLabel.Equals(l2.FullLabel, StringComparison.InvariantCultureIgnoreCase);
			}

			public int GetHashCode(PinballLabel label)
			{
				return label.FullLabel.GetHashCode();
			}
		}

		public PinballLabelList() : base(new LabelComparer()) { }

		internal class PinballLabelListDebugView
		{
			private PinballLabelList list;

			public PinballLabelListDebugView(PinballLabelList list)
			{
				if (list == null) {
					throw new ArgumentNullException("list");
				}

				this.list = list;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public PinballLabel[] Items
			{
				get {
					return list.ToArray();
				}
			}
		}
	}


}
