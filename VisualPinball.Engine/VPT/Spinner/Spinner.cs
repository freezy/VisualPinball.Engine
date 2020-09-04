// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Spinner
{
	public class Spinner : Item<SpinnerData>, IRenderable, IHittable
	{
		public const string BracketMaterialName = "__spinnerBracketMaterial";


		private readonly SpinnerMeshGenerator _meshGenerator;
		private readonly SpinnerHitGenerator _hitGenerator;

		private SpinnerHit _hitSpinner;
		private HitCircle[] _hitCircles;

		public Spinner(SpinnerData data) : base(data)
		{
			_meshGenerator = new SpinnerMeshGenerator(Data);
			_hitGenerator = new SpinnerHitGenerator(Data);
		}

		public Spinner(BinaryReader reader, string itemName) : this(new SpinnerData(reader, itemName))
		{
		}

		public static Spinner GetDefault(Table.Table table)
		{
			var spinnerData = new SpinnerData(table.GetNewName<Spinner>("Spinner"), table.Width / 2f, table.Height / 2f);
			return new Spinner(spinnerData);
		}

		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);

			_hitSpinner = new SpinnerHit(Data, height, this);
			_hitCircles = _hitGenerator.GetHitCircles(height, this);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		public HitObject[] GetHitShapes()
		{
			return new HitObject[] {_hitSpinner}
				.Concat(_hitCircles)
				.ToArray();
		}
	}
}
