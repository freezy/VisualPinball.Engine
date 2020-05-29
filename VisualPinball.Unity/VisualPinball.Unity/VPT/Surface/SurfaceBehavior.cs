#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Physics.Collision;

namespace VisualPinball.Unity.VPT.Surface
{
	[AddComponentMenu("Visual Pinball/Surface")]
	public class SurfaceBehavior : ItemBehavior<Engine.VPT.Surface.Surface, Engine.VPT.Surface.SurfaceData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new [] { "Side", "Top" };

		protected override Engine.VPT.Surface.Surface GetItem()
		{
			return new Engine.VPT.Surface.Surface(data);
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			dstManager.AddComponentData(entity, new LineSlingshotData {
				IsDisabled = false,
				Threshold = data.SlingshotThreshold,
			});
			transform.GetComponentInParent<Player>().RegisterSurface(Item, entity, gameObject);
		}
	}
}
