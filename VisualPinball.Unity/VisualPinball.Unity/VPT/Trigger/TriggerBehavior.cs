#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.VPT.Trigger
{
	[AddComponentMenu("Visual Pinball/Trigger")]
	public class TriggerBehavior : ItemBehavior<Engine.VPT.Trigger.Trigger, TriggerData>, IDragPointsEditable, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var table = gameObject.GetComponentInParent<TableBehavior>().Item;
			dstManager.AddComponentData(entity, new TriggerAnimationData());
			dstManager.AddComponentData(entity, new TriggerMovementData());
			dstManager.AddComponentData(entity, new TriggerStaticData {
				AnimSpeed = data.AnimSpeed,
				Radius = data.Radius,
				Shape = data.Shape,
				TableScaleZ = table.GetScaleZ()
			});

			// register
			var trigger = GetComponent<TriggerBehavior>().Item;
			transform.GetComponentInParent<Player>().RegisterTrigger(trigger, entity, gameObject);
		}

		protected override Engine.VPT.Trigger.Trigger GetItem()
		{
			return new Engine.VPT.Trigger.Trigger(data);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex2Dxy();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.Rotation = rot.x;

		//IDragPointsEditable
		public bool DragPointEditEnabled { get; set; }
		public DragPointData[] GetDragPoints() => data.DragPoints;
		public void SetDragPoints(DragPointData[] dragPoints) { data.DragPoints = dragPoints; }
		public Vector3 GetEditableOffset() => new Vector3(-data.Center.X, -data.Center.Y, 0.0f);
		public Vector3 GetDragPointOffset(float ratio) => Vector3.zero;
		public bool PointsAreLooping() => true;
		public IEnumerable<DragPointExposure> GetDragPointExposition() => new[] { DragPointExposure.Smooth, DragPointExposure.SlingShot };
		public ItemDataTransformType GetHandleType() => ItemDataTransformType.TwoD;
	}
}
