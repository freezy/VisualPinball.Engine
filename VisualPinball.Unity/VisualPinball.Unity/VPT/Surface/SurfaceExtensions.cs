using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Surface
{
	public static class SurfaceExtensions
	{
		public static void SetupGameObject(this Engine.VPT.Surface.Surface surface, GameObject obj, RenderObjectGroup rog)
		{
			obj.AddComponent<SurfaceBehavior>().SetData(surface.Data);
			obj.AddComponent<ConvertToEntity>();
		}
	}
}
