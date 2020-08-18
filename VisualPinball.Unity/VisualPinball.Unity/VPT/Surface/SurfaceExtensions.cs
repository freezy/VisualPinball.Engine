using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class SurfaceExtensions
	{
		public static SurfaceAuthoring SetupGameObject(this Engine.VPT.Surface.Surface surface, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<SurfaceAuthoring>().SetItem(surface);
			obj.AddComponent<ConvertToEntity>();
			return ic as SurfaceAuthoring;
		}
	}
}
