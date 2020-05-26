#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Unity.Game;
using VisualPinball.Unity.Extensions;

namespace VisualPinball.Unity.VPT.Surface
{
	[AddComponentMenu("Visual Pinball/Surface")]
	public class SurfaceBehavior : ItemBehavior<Engine.VPT.Surface.Surface, SurfaceData>, IConvertGameObjectToEntity
	{
		public override bool RebuildMeshOnMove => true;
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

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		public override Vector3 GetEditorPosition() {
			if (data == null || data.DragPoints.Length == 0) {
				return Vector3.zero;
			}
			return data.DragPoints[0].Vertex.ToUnityVector3();
		}
		public override void SetEditorPosition(Vector3 pos) {
			if (data == null || data.DragPoints.Length == 0) {
				return;
			}

			var diff = pos.ToVertex3D().Sub(data.DragPoints[0].Vertex);
			diff.Z = 0f;
			data.DragPoints[0].Vertex = pos.ToVertex3D();
			for (int i = 1; i < data.DragPoints.Length; i++) {
				var pt = data.DragPoints[i];
				pt.Vertex = pt.Vertex.Add(diff);
			}
		}
	}
}
