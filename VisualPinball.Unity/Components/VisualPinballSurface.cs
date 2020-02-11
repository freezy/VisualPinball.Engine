#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.VPT.Surface;

namespace VisualPinball.Unity.Components
{
	[ExecuteInEditMode]
	public class VisualPinballSurface : ItemComponent<Surface, SurfaceData>
	{
		protected override string[] Children => new [] { "Side", "Top" };

		protected override Surface GetItem()
		{
			return new Surface(data);
		}
	}
}
