#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballPrimitive : ItemComponent<Primitive, PrimitiveData>
	{
		protected override string[] Children => null;

		protected override Primitive GetItem()
		{
			return new Primitive(data);
		}
	}
}
