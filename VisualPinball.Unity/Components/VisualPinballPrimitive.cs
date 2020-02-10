#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Components
{
	public class VisualPinballPrimitive : ItemComponent<Primitive, PrimitiveData>
	{
		protected override string[] Children => null;

		protected override Primitive GetItem()
		{
			return new Primitive(data);
		}

		protected override void OnDataSet()
		{
		}

		protected override void OnFieldsUpdated()
		{
		}
	}
}
