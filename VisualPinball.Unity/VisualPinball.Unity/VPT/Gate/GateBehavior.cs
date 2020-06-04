#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Gate
{
	[AddComponentMenu("Visual Pinball/Gate")]
	public class GateBehavior : ItemBehavior<Engine.VPT.Gate.Gate, GateData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new []{"Wire", "Bracket"};

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			dstManager.AddComponentData(entity, new GateStaticData {
				AngleMin = data.AngleMin,
				AngleMax = data.AngleMax,
				Height = data.Height,
				Damping = data.Damping,
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
			return new Engine.VPT.Gate.Gate(data);
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(data.Height);
		public override void SetEditorPosition(Vector3 pos)
		{
			data.Center = pos.ToVertex2Dxy();
			data.Height = pos.z;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(data.Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => data.Rotation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(data.Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => data.Length = scale.x;
	}
}
