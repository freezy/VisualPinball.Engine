using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class GateExtensions
	{
		public static GateAuthoring SetupGameObject(this Engine.VPT.Gate.Gate gate, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<GateAuthoring>().SetItem(gate);
			obj.AddComponent<ConvertToEntity>();

			var wire = obj.transform.Find("Wire").gameObject;
			wire.AddComponent<GateWireAuthoring>().SetItem(gate, "Wire");

			return ic as GateAuthoring;
		}
	}
}
