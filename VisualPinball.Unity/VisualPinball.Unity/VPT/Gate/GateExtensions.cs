using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Gate
{
	public static class GateExtensions
	{
		public static GateBehavior SetupGameObject(this Engine.VPT.Gate.Gate gate, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<GateBehavior>().SetItem(gate);
			obj.AddComponent<ConvertToEntity>();

			var wire = obj.transform.Find("Wire").gameObject;
			wire.AddComponent<GateWireBehavior>().SetItem(gate, "Wire");

			return ic as GateBehavior;
		}
	}
}
