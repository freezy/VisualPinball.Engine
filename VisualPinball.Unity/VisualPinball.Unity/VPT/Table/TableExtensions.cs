using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Table
{
	public static class TableExtensions
	{
		public static void SetupGameObject(this Engine.VPT.Table.Table table, GameObject go, RenderObjectGroup rog)
		{
			go.AddComponent<ConvertToEntity>();
		}
	}
}
