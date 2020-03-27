
using UnityEngine;
using PhysicsCategoryTags = Unity.Physics.Authoring.PhysicsCategoryTags;
using PhysicsShapeAuthoring = Unity.Physics.Authoring.PhysicsShapeAuthoring;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity
{
	class PhysicsTags
	{
		public const uint Ball = 1 << 0;
		public const uint Playfield  = 1 << 1;
		public const uint Static  = 1 << 2;
		public const uint Flipper = 1 << 3;

		public static void SetAllPhysicsTagsAsStatic()
		{
			var table = GameObject.FindObjectOfType<TableBehavior>();
			foreach (var psa in table.GetComponentsInChildren<PhysicsShapeAuthoring>())
			{
				if (psa.BelongsTo.Value == PhysicsCategoryTags.Everything.Value && psa.CollidesWith.Value == PhysicsCategoryTags.Everything.Value)
				{
					psa.BelongsTo = new PhysicsCategoryTags { Value = Static };
					psa.CollidesWith = new PhysicsCategoryTags { Value = Ball };
				}
			}
		}

		public static void SetCollisionsFilters(PhysicsShapeAuthoring psa, uint tag)
		{
			psa.BelongsTo = new PhysicsCategoryTags { Value = tag };
			switch (tag)
			{
				case Ball:
					psa.CollidesWith = new PhysicsCategoryTags { Value = Ball | Playfield | Static | Flipper };
					break;

				case Playfield:
				case Static:
				case Flipper:
				default:
					psa.CollidesWith = new PhysicsCategoryTags { Value = Ball };
					break;
			}
		}

		public static void SetCollisionsFilters(GameObject go, uint tag)
		{
			foreach (PhysicsShapeAuthoring psa in go.GetComponents<PhysicsShapeAuthoring>())
			{
				SetCollisionsFilters(psa, tag);
			}

			foreach (var psa in go.GetComponentsInChildren<PhysicsShapeAuthoring>(true))
			{
				SetCollisionsFilters(psa, tag);
			}
		}
	}
}
