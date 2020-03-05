#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballKicker : ItemComponent<Kicker, KickerData>
	{
		protected override string[] Children => null;

		protected override Kicker GetItem()
		{
			return new Kicker(data);
		}
	}
}
