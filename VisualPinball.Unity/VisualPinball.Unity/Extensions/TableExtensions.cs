using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity.Extensions
{
	public static class TableExtensions
	{
		public static void SetupGameObject(this Table table, GameObject go, RenderObjectGroup rog)
		{
			go.AddComponent<ConvertToEntity>();
			rog.AddPhysicsShape(go);
		}
	}
}
