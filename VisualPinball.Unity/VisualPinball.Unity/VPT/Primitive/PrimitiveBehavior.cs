#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity.VPT.Primitive
{
	[AddComponentMenu("Visual Pinball/Primitive")]
	public class PrimitiveBehavior : ItemBehavior<Engine.VPT.Primitive.Primitive, PrimitiveData>
	{
		protected override string[] Children => null;

		protected override Engine.VPT.Primitive.Primitive GetItem()
		{
			return new Engine.VPT.Primitive.Primitive(data);
		}
	}
}
