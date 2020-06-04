using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Gate
{
	public static class GateExtensions
	{
		public static GateBehavior SetupGameObject(this Engine.VPT.Gate.Gate gate, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<GateBehavior>().SetData(gate.Data);
			obj.AddComponent<ConvertToEntity>();
			return ic as GateBehavior;
		}
	}
}
