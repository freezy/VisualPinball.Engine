#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Kicker;

namespace VisualPinball.Unity.VPT.Kicker
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Kicker")]
	public class KickerBehavior : ItemBehavior<Engine.VPT.Kicker.Kicker, KickerData>
	{
		protected override string[] Children => null;

		protected override Engine.VPT.Kicker.Kicker GetItem()
		{
			return new Engine.VPT.Kicker.Kicker(data);
		}
	}
}
