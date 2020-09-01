using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	internal static class TableExtensions
	{
		public static MonoBehaviour SetupGameObject(this Engine.VPT.Table.Table table, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<PlayfieldAuthoring>();
			obj.AddComponent<ConvertToEntity>();
			return ic;
		}
	}
}
