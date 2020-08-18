#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Hit Target")]
	public class HitTargetAuthoring : ItemAuthoring<HitTarget, HitTargetData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		protected override HitTarget GetItem() => new HitTarget(data);

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<HitTarget>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;

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
			var hitTarget = transform.GetComponent<HitTargetAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterHitTarget(hitTarget, entity, gameObject);
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
