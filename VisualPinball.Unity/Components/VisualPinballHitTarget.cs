#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity.Components
{
	public class VisualPinballHitTarget : ItemComponent<HitTarget, HitTargetData>
	{
		protected override string[] Children => null;

		protected override HitTarget GetItem()
		{
			return new HitTarget(data);
		}

		protected override void OnDataSet()
		{
		}

		protected override void OnFieldsUpdated()
		{
		}
	}
}
