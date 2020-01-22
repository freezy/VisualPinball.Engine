using System;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Resources.Meshes;

namespace VisualPinball.Engine.VPT.Trigger
{
	public class TriggerMeshGenerator : MeshGenerator
	{
		private readonly TriggerData _data;

		protected override Vertex3D Position => new Vertex3D(_data.Center.X, _data.Center.Y, 0);
		protected override Vertex3D Scale => Vertex3D.One;
		protected override float RotationZ => MathF.DegToRad(_data.Rotation);

		public TriggerMeshGenerator(TriggerData data)
		{
			_data = data;
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin, bool asRightHanded)
		{
			var (preMatrix, _) = GetPreMatrix(table, origin, asRightHanded);
			var postMatrix = GetPostMatrix(table, origin);
			return new RenderObjectGroup(_data.Name, "Triggers", postMatrix, new RenderObject(
					name: _data.Name,
					mesh: GetMesh().Transform(preMatrix),
					material: table.GetMaterial(_data.Material),
					isVisible: _data.IsVisible
				)
			);
		}

		protected override float BaseHeight(Table.Table table)
		{
			return table.GetSurfaceHeight(_data.Surface, _data.Center.X, _data.Center.Y);
		}

		private Mesh GetMesh()
		{
			var vertexMatrix = GetVertexTransformationMatrix();
			return GetBaseMesh().Transform(vertexMatrix);
		}

		private Matrix3D GetVertexTransformationMatrix()
		{
			var rotX = 0f;
			var offsetZ = 0f;
			var scale = new Vertex3D(_data.ScaleX, _data.ScaleY, 1.0f);
			switch (_data.Shape) {

				case TriggerShape.TriggerWireB:
					rotX = -23.0f;
					break;

				case TriggerShape.TriggerWireC:
					rotX = 140.0f;
					offsetZ = -19.0f;
					break;

				case TriggerShape.TriggerButton:
					offsetZ = 5.0f;
					scale.X = _data.Radius;
					scale.Y = _data.Radius;
					scale.Z = _data.Radius;
					break;

				case TriggerShape.TriggerStar:
					scale.X = _data.Radius;
					scale.Y = _data.Radius;
					scale.Z = _data.Radius;
					break;
			}

			// scale matrix
			var scaleMatrix = new Matrix3D();
			scaleMatrix.SetScaling(scale.X, scale.Y, scale.Z);

			// translation matrix
			var transMatrix = new Matrix3D();
			transMatrix.SetTranslation(0f, 0f, offsetZ);

			// rotation matrix
			var rotMatrix = new Matrix3D();
			rotMatrix.RotateXMatrix(MathF.DegToRad(rotX));

			var fullMatrix = scaleMatrix;
			fullMatrix.Multiply(rotMatrix);
			fullMatrix.Multiply(transMatrix);

			return fullMatrix;
		}

		private Mesh GetBaseMesh()
		{
			switch (_data.Shape) {
				case TriggerShape.TriggerWireA:
				case TriggerShape.TriggerWireB:
				case TriggerShape.TriggerWireC:
					return TriggerSimpleMesh.Clone(_data.Name);
				case TriggerShape.TriggerWireD:
					return TriggerWireDMesh.Clone(_data.Name);
				case TriggerShape.TriggerButton:
					return TriggerButtonMesh.Clone(_data.Name);
				case TriggerShape.TriggerStar:
					return TriggerStarMesh.Clone(_data.Name);
				default: return TriggerSimpleMesh.Clone(_data.Name);
			}
		}

		#region Mesh Imports

		private static readonly Mesh TriggerButtonMesh = new Mesh(TriggerButton.Vertices, TriggerButton.Indices);
		private static readonly Mesh TriggerSimpleMesh = new Mesh(TriggerSimple.Vertices, TriggerSimple.Indices);
		private static readonly Mesh TriggerStarMesh = new Mesh(TriggerStar.Vertices, TriggerStar.Indices);
		private static readonly Mesh TriggerWireDMesh = new Mesh(TriggerWireD.Vertices, TriggerWireD.Indices);

		#endregion
	}
}
