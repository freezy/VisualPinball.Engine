using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Surface
{
	public static class SurfaceExtensions
	{
		public static SurfaceBehavior SetupGameObject(this Engine.VPT.Surface.Surface surface, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<SurfaceBehavior>().SetData(surface.Data);
			obj.AddComponent<ConvertToEntity>();
			return ic as SurfaceBehavior;
		}
	}
}
