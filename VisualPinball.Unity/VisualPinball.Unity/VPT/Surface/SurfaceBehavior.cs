#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.VPT.Surface
{
	[ExecuteInEditMode]
	[AddComponentMenu("Visual Pinball/Surface")]
	public class SurfaceBehavior : ItemBehavior<Engine.VPT.Surface.Surface, SurfaceData>
	{
		protected override string[] Children => new [] { "Side", "Top" };

		protected override Engine.VPT.Surface.Surface GetItem()
		{
			return new Engine.VPT.Surface.Surface(data);
		}
	}
}
