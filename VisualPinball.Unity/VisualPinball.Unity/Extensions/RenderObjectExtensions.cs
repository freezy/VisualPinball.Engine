using System;
using Unity.Physics.Authoring;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.Extensions
{
	public static class RenderObjectExtensions
	{
		public static void AddPhysicsBody(this RenderObjectGroup rog, GameObject go)
		{
			Add(rog, go, PhysicsBody);
		}

		public static void AddPhysicsShape(this RenderObjectGroup rog, GameObject go)
		{
			Add(rog, go, PhysicsShape);
		}

		public static void AddPhysicsShape(this RenderObject ro, GameObject go)
		{
			var child = go.transform.Find(ro.Name);
			if (child != null) {
				PhysicsShape(ro, child.gameObject);
			}
		}

		public static void AddPhysicsShapeToParent(this RenderObject ro, GameObject go)
		{
			var child = go.transform.Find(ro.Name);
			if (child != null) {
				var mesh = child.GetComponent<MeshFilter>();
				var shape = go.AddComponent<PhysicsShapeAuthoring>();
				shape.Friction = new PhysicsMaterialCoefficient {Value = 0};
				shape.SetMesh(mesh.sharedMesh);
			}
		}

		private static void Add(RenderObjectGroup rog, GameObject go, Action<RenderObject, GameObject> add)
		{
			if (rog.HasChildren) {
				foreach (var ro in rog.RenderObjects) {
					var child = go.transform.Find(ro.Name);
					if (child != null) {
						add(ro, child.gameObject);
					}
				}
			}

			if (rog.HasOnlyChild) {
				add(rog.RenderObjects[0], go);
			}
		}

		private static void PhysicsShape(RenderObject ro, GameObject go)
		{
			var mesh = go.GetComponent<MeshFilter>();
			var shape = go.AddComponent<PhysicsShapeAuthoring>();
			shape.Friction = new PhysicsMaterialCoefficient {Value = 0};
			shape.SetMesh(mesh.sharedMesh);
		}

		private static void PhysicsBody(RenderObject ro, GameObject go)
		{
			var body = go.AddComponent<PhysicsBodyAuthoring>();
			body.MotionType = BodyMotionType.Kinematic;
		}
	}
}
