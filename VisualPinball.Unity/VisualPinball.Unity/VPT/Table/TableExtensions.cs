using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Table
{
	public static class TableExtensions
	{
		public static MonoBehaviour SetupGameObject(this Engine.VPT.Table.Table table, GameObject go, RenderObjectGroup rog)
		{
			go.AddComponent<ConvertToEntity>();
			return null;
		}
	}
}
