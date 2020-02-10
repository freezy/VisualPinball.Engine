#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Components
{
	public class VisualPinballRubber : ItemComponent<Rubber, RubberData>
	{
		protected override string[] Children => null;

		protected override Rubber GetItem()
		{
			return new Rubber(data);
		}

		protected override void OnDataSet()
		{
		}

		protected override void OnFieldsUpdated()
		{
		}
	}
}
