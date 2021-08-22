// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.IO;
using Codice.Client.ChangeTrackerService;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Primitive
{
	/// <summary>
	/// A primitive is a 3D playfield element usually imported from
	/// third party software such as Blender.
	/// </summary>
	///
	/// <see href="https://github.com/vpinball/vpinball/blob/master/primitive.cpp"/>
	public class Primitive : Item<PrimitiveData>, IRenderable
	{
		public override string ItemName => "Primitive";
		public override string ItemGroupName => "Primitives";

		public bool UseAsPlayfield => Name == "playfield_mesh";

		public float RotationY { get => Data.RotAndTra[1]; set => Data.RotAndTra[1] = value; }

		private readonly PrimitiveMeshGenerator _meshGenerator;

		public Primitive(PrimitiveData data) : base(data)
		{
			_meshGenerator = new PrimitiveMeshGenerator(Data);
		}

		public Primitive(BinaryReader reader, string itemName) : this(new PrimitiveData(reader, itemName))
		{
		}

		public static Primitive GetDefault(Table.Table table)
		{
			var primitiveData = new PrimitiveData(table.GetNewName<Primitive>("Primitive"), table.Width / 2f, table.Height / 2f);
			return new Primitive(primitiveData);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => _meshGenerator.GetPostMatrix(table, origin);

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObject(table, null, origin, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true) =>
			_meshGenerator.GetRenderObjects(table, Data.Mesh, origin, asRightHanded);

		#endregion

		public Mesh GetMesh() => _meshGenerator.GetMesh(Data.Mesh);

	}
}
