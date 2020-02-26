using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;

namespace VisualPinball.Unity.Extensions
{
	public static class SurfaceExtensions
	{
		public static void SetupGameObject(this Surface surface, GameObject obj, RenderObjectGroup rog)
		{
			obj.AddComponent<VisualPinballSurface>().SetData(surface.Data);
			obj.AddComponent<ConvertToEntity>();
			//rog.AddPhysicsBody(obj);
			rog.AddPhysicsShape(obj);
		}
	}
}
