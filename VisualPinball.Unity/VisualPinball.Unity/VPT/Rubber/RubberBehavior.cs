#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.VPT.Rubber
{
	[AddComponentMenu("Visual Pinball/Rubber")]
	public class RubberBehavior : ItemBehavior<Engine.VPT.Rubber.Rubber, RubberData>
	{
		protected override string[] Children => null;

		protected override Engine.VPT.Rubber.Rubber GetItem()
		{
			return new Engine.VPT.Rubber.Rubber(data);
		}
	}
}
