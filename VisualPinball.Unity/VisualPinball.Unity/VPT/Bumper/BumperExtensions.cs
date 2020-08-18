using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	public static class BumperExtensions
	{
		public static BumperAuthoring SetupGameObject(this Engine.VPT.Bumper.Bumper bumper, GameObject obj, RenderObjectGroup rog)
		{
			var ic = obj.AddComponent<BumperAuthoring>().SetItem(bumper);

			obj.AddComponent<ConvertToEntity>();

			var ring = obj.transform.Find("Ring").gameObject;
			var skirt = obj.transform.Find("Skirt").gameObject;

			ring.AddComponent<BumperRingAuthoring>();
			skirt.AddComponent<BumperSkirtAuthoring>();

			return ic as BumperAuthoring;
		}
	}
}
