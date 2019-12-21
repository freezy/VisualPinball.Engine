using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Primitive
{
	public class PrimitiveMeshGenerator
	{
		private readonly PrimitiveData _data;

		public PrimitiveMeshGenerator(PrimitiveData data)
		{
			_data = data;
		}

		public Mesh GetMesh(Table.Table table)
		{
			var mesh = _data.Mesh.Clone();
			var matrix = GetMatrix(table);
			return mesh.Transform(matrix);
		}

		private Matrix3D GetMatrix(Table.Table table) {

			// scale matrix
			var scaleMatrix = new Matrix3D();
			scaleMatrix.SetScaling(_data.Size.X, _data.Size.Y, _data.Size.Z);

			// translation matrix
			var transMatrix = new Matrix3D();
			transMatrix.SetTranslation(_data.Position.X, _data.Position.Y, _data.Position.Z);

			// translation + rotation matrix
			var rotTransMatrix = new Matrix3D();
			rotTransMatrix.SetTranslation(_data.RotAndTra[3], _data.RotAndTra[4], _data.RotAndTra[5]);

			var tempMatrix = new Matrix3D();
			tempMatrix.RotateZMatrix(MathF.DegToRad(_data.RotAndTra[2]));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateYMatrix(MathF.DegToRad(_data.RotAndTra[1]));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateXMatrix(MathF.DegToRad(_data.RotAndTra[0]));
			rotTransMatrix.Multiply(tempMatrix);

			tempMatrix.RotateZMatrix(MathF.DegToRad(_data.RotAndTra[8]));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateYMatrix(MathF.DegToRad(_data.RotAndTra[7]));
			rotTransMatrix.Multiply(tempMatrix);
			tempMatrix.RotateXMatrix(MathF.DegToRad(_data.RotAndTra[6]));
			rotTransMatrix.Multiply(tempMatrix);

			var fullMatrix = scaleMatrix.Clone();
			fullMatrix.Multiply(rotTransMatrix);
			fullMatrix.Multiply(transMatrix);        // fullMatrix = Smatrix * RTmatrix * Tmatrix
			scaleMatrix.SetScaling(1.0f, 1.0f, table.GetScaleZ());
			fullMatrix.Multiply(scaleMatrix);

			return fullMatrix;
		}
	}
}
