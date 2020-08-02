using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Surface
{
	public static class SurfaceExtensions
	{
		public static SurfaceBehavior SetupGameObject(this Engine.VPT.Surface.Surface surface, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<SurfaceBehavior>().SetItem(surface);
			obj.AddComponent<ConvertToEntity>();
			return ic as SurfaceBehavior;
		}
	}
}
