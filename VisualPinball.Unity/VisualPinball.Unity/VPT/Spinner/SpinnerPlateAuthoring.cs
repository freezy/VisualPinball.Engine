using Unity.Entities;
using Unity.Mathematics;
using VisualPinball.Engine.Common;
using VisualPinball.Engine.VPT.Spinner;

namespace VisualPinball.Unity
{
	public class SpinnerPlateAuthoring : ItemAuthoring<Spinner, SpinnerData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new string[0];

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			dstManager.AddComponentData(entity, new SpinnerStaticData {
				AngleMax = math.radians(data.AngleMax),
				AngleMin = math.radians(data.AngleMin),
				Damping = math.pow(data.Damping, PhysicsConstants.PhysFactor),
				Elasticity = data.Elasticity,
				Height = data.Height
			});

			dstManager.AddComponentData(entity, new SpinnerMovementData {
				Angle = math.radians(math.clamp(0.0f, data.AngleMin, data.AngleMax)),
				AngleSpeed = 0f
			});

			// register
			var spinner = transform.parent.gameObject.GetComponent<SpinnerAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterSpinner(spinner, entity, gameObject);
		}

		protected override Spinner GetItem()
		{
			return transform.parent.gameObject.GetComponent<SpinnerAuthoring>().Item;
		}
	}
}
