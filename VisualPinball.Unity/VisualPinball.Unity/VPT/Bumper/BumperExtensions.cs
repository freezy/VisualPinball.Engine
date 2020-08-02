using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.VPT.Bumper
{
	public static class BumperExtensions
	{
		public static BumperBehavior SetupGameObject(this Engine.VPT.Bumper.Bumper bumper, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<BumperBehavior>().SetItem(bumper);

			obj.AddComponent<ConvertToEntity>();

			var ring = obj.transform.Find("Ring").gameObject;
			var skirt = obj.transform.Find("Skirt").gameObject;

			ring.AddComponent<BumperRingBehavior>();
			skirt.AddComponent<BumperSkirtBehavior>();

			return ic as BumperBehavior;
		}
	}
}
