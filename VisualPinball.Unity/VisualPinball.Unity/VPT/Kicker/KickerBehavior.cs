#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Unity.Game;

namespace VisualPinball.Unity.VPT.Kicker
{
	[AddComponentMenu("Visual Pinball/Kicker")]
	public class KickerBehavior : ItemBehavior<Engine.VPT.Kicker.Kicker, KickerData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		protected override Engine.VPT.Kicker.Kicker GetItem()
		{
			return new Engine.VPT.Kicker.Kicker(data);
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			// register
			transform.GetComponentInParent<Player>().RegisterKicker(Item, entity);
		}
	}
}
