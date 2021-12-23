using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

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
