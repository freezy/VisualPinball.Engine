#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.VPT.HitTarget
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Hit Target")]
	public class HitTargetBehavior : ItemBehavior<Engine.VPT.HitTarget.HitTarget, HitTargetData>
	{
		protected override string[] Children => null;

		protected override Engine.VPT.HitTarget.HitTarget GetItem()
		{
			return new Engine.VPT.HitTarget.HitTarget(data);
		}
	}
}
