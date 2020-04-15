#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Surface
{
	[AddComponentMenu("Visual Pinball/Surface")]
	public class SurfaceBehavior : ItemBehavior<Engine.VPT.Surface.Surface, SurfaceData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new [] { "Side", "Top" };

		protected override Engine.VPT.Surface.Surface GetItem()
		{
			return new Engine.VPT.Surface.Surface(data);
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			transform.GetComponentInParent<Player>().RegisterSurface(Item, entity, gameObject);
		}
	}
}
