#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Light;
using Light = VisualPinball.Engine.VPT.Light.Light;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballLight : ItemComponent<Light, LightData>
	{
		protected override string[] Children => null;

		protected override Light GetItem()
		{
			return new Light(data);
		}
	}
}
