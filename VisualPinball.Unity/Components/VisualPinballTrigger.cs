#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballTrigger : ItemComponent<Trigger, TriggerData>
	{
		protected override string[] Children => null;

		protected override Trigger GetItem()
		{
			return new Trigger(data);
		}
	}
}
