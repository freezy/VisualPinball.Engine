#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.VPT.Trigger
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Trigger")]
	public class TriggerBehavior : ItemBehavior<Engine.VPT.Trigger.Trigger, TriggerData>
	{
		protected override string[] Children => null;

		protected override Engine.VPT.Trigger.Trigger GetItem()
		{
			return new Engine.VPT.Trigger.Trigger(data);
		}
	}
}
