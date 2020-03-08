#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Light;

namespace VisualPinball.Unity.VPT.Light
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Light")]
	public class LightBehavior : ItemBehavior<Engine.VPT.Light.Light, LightData>
	{
		protected override string[] Children => null;

		protected override Engine.VPT.Light.Light GetItem()
		{
			return new Engine.VPT.Light.Light(data);
		}
	}
}
