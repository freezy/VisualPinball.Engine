using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
