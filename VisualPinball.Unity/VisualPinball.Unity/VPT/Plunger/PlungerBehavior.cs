using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Plunger
{
	[AddComponentMenu("Visual Pinball/Plunger")]
	public class PlungerBehavior : ItemBehavior<Engine.VPT.Plunger.Plunger, PlungerData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => new []{ PlungerMeshGenerator.FlatName, PlungerMeshGenerator.RodName, PlungerMeshGenerator.SpringName };

		protected override Engine.VPT.Plunger.Plunger GetItem() => new Engine.VPT.Plunger.Plunger(data);

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{

		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() => data.Center.ToUnityVector3(0f);
		public override void SetEditorPosition(Vector3 pos) => data.Center = pos.ToVertex3D();
	}
}
