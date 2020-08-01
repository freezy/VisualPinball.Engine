#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.VPT.HitTarget
{
	[AddComponentMenu("Visual Pinball/Hit Target")]
	public class HitTargetBehavior : ItemBehavior<Engine.VPT.HitTarget.HitTarget, HitTargetData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var table = gameObject.GetComponentInParent<TableBehavior>().Item;

			dstManager.AddComponentData(entity, new HitTargetStaticData {
				TargetType = data.TargetType,
				DropSpeed = data.DropSpeed,
				RaiseDelay = data.RaiseDelay,
				UseHitEvent = data.UseHitEvent,
				RotZ = data.RotZ,
				TableScaleZ = table.GetScaleZ()
			});
			dstManager.AddComponentData(entity, new HitTargetAnimationData {
				IsDropped = data.IsDropped
			});
			dstManager.AddComponentData(entity, new HitTargetMovementData());

			// register
			var hitTarget = transform.GetComponent<HitTargetBehavior>().Item;
			transform.GetComponentInParent<Player>().RegisterHitTarget(hitTarget, entity, gameObject);
		}

		protected override Engine.VPT.HitTarget.HitTarget GetItem()
		{
			return new Engine.VPT.HitTarget.HitTarget(data);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => data.Position.ToUnityVector3();
		public override void SetEditorPosition(Vector3 pos) => data.Position = pos.ToVertex3D();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.RotZ, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.RotZ = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => data.Size.ToUnityVector3();
		public override void SetEditorScale(Vector3 scale) => data.Size = scale.ToVertex3D();
	}
}
