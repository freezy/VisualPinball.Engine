using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity.VPT.Gate
{
	public class GateWireBehavior : ItemBehavior<Engine.VPT.Gate.Gate, GateData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new string[0];

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			dstManager.AddComponentData(entity, new GateStaticData {
				AngleMin = data.AngleMin,
				AngleMax = data.AngleMax,
				Height = data.Height,
				Damping = math.pow(data.Damping, PhysicsConstants.PhysFactor),
				GravityFactor = data.GravityFactor,
				TwoWay = data.TwoWay
			});
			dstManager.AddComponentData(entity, new GateMovementData {
				Angle = data.AngleMin,
				AngleSpeed = 0,
				ForcedMove = false,
				IsOpen = false
			});

		}

		protected override Engine.VPT.Gate.Gate GetItem()
		{
			return transform.parent.gameObject.GetComponent<GateBehavior>().Item;
		}
	}
}
