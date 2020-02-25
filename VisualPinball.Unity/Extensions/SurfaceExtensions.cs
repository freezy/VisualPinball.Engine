using System.Linq;
using Unity.Physics;
using Unity.Physics.Authoring;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Components;

namespace VisualPinball.Unity.Extensions
{
	public static class SurfaceExtensions
	{
		public static void OnGameObject(this Surface surface, GameObject obj, Table table)
		{
			obj.AddComponent<VisualPinballSurface>().SetData(surface.Data);
			obj.AddComponent<PhysicsBodyAuthoring>();
		}

		private static void AddPhysicsShape(IRenderable renderable, GameObject obj, Table table)
		{
			var shape = obj.AddComponent<PhysicsShapeAuthoring>();
			shape.Friction = new PhysicsMaterialCoefficient {Value = 0};

			var combine = renderable.GetRenderObjects(table).RenderObjects
				.Select(ro => ro.Mesh.ToUnityMesh())
				.Select(mesh => new CombineInstance {mesh = mesh, transform = obj.transform.localToWorldMatrix })
				.ToArray();

			var combinedMesh = new UnityEngine.Mesh();
			combinedMesh.CombineMeshes(combine);
			shape.SetMesh(combinedMesh);
		}
	}
}
