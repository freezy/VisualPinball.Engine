#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Extensions;

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
			transform.GetComponentInParent<Player>().RegisterKicker(Item, entity, gameObject);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.Orientation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.Orientation = rot.x;
	}
}
