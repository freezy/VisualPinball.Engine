#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Primitive")]
	public class PrimitiveAuthoring : ItemAuthoring<Primitive, PrimitiveData>, IHittableAuthoring, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);
		}

		protected override Primitive GetItem() => new Primitive(data);

		public IHittable Hittable => Item;

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => data.Position.ToUnityVector3();
		public override void SetEditorPosition(Vector3 pos) => data.Position = pos.ToVertex3D();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorRotation() => new Vector3(data.RotAndTra[0], data.RotAndTra[1], data.RotAndTra[2]);
		public override void SetEditorRotation(Vector3 rot)
		{
			data.RotAndTra[0] = rot.x;
			data.RotAndTra[1] = rot.y;
			data.RotAndTra[2] = rot.z;
		}

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => data.Size.ToUnityVector3();
		public override void SetEditorScale(Vector3 scale) => data.Size = scale.ToVertex3D();
	}
}
