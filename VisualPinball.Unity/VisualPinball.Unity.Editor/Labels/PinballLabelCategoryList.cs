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
	[DebuggerTypeProxy(typeof(PinballLabelCategoryListDebugView))]
	public class PinballLabelCategoryList : HashSet<PinballLabelCategory>
	{
		private class CategoryComparer : IEqualityComparer<PinballLabelCategory>
		{
			public bool Equals(PinballLabelCategory c1, PinballLabelCategory c2)
			{
				return c1.Name.Equals(c2.Name, StringComparison.InvariantCultureIgnoreCase);
			}

			public int GetHashCode(PinballLabelCategory cat)
			{
				return cat.Name.GetHashCode();
			}
		}

		public PinballLabelCategoryList() : base(new CategoryComparer()) { }

		internal class PinballLabelCategoryListDebugView
		{
			private PinballLabelCategoryList list;

			public PinballLabelCategoryListDebugView(PinballLabelCategoryList list)
			{
				if (list == null) {
					throw new ArgumentNullException("list");
				}

				this.list = list;
			}

			[DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
			public PinballLabelCategory[] Items
			{
				get {
					return list.ToArray();
				}
			}
		}
	}
}
