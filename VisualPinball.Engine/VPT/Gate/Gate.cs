﻿// Visual Pinball Engine
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
using System.Linq;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Gate
{
	public class Gate : Item<GateData>, IRenderable, IHittable, ISwitchable
	{
		public override string ItemName { get; } = "Gate";
		public override string ItemGroupName { get; } = "Gates";
		public override ItemType ItemType { get; } = ItemType.Gate;

		public Vertex3D Position { get => new Vertex3D(Data.Center.X, Data.Center.Y, 0); set => Data.Center = new Vertex2D(value.X, value.Y); }
		public float RotationY { get => Data.Rotation; set => Data.Rotation = value; }

		public bool IsPulseSwitch => true;

		private readonly GateMeshGenerator _meshGenerator;
		private readonly GateHitGenerator _hitGenerator;
		private GateHit _hitGate;
		private LineSeg[] _hitLines;
		private HitCircle[] _hitCircles;

		public Gate(GateData data) : base(data)
		{
			_meshGenerator = new GateMeshGenerator(Data);
			_hitGenerator = new GateHitGenerator(Data);
		}

		public Gate(BinaryReader reader, string itemName) : this(new GateData(reader, itemName))
		{
		}

		public static Gate GetDefault(Table.Table table)
		{
			var gateData = new GateData(table.GetNewName<Gate>("Gate"), table.Width / 2f, table.Height / 2f);
			return new Gate(gateData);
		}

		public void Init(Table.Table table)
		{
			var height = table.GetSurfaceHeight(Data.Surface, Data.Center.X, Data.Center.Y);
			var radAngle = MathF.DegToRad(Data.Rotation);
			var tangent = new Vertex2D(MathF.Cos(radAngle), MathF.Sin(radAngle));

			_hitGate = _hitGenerator.GenerateGateHit(height, this);
			_hitLines = _hitGenerator.GenerateLineSegs(height, tangent, this);
			_hitCircles = _hitGenerator.GenerateBracketHits(height, tangent, this);
		}

		#region IRenderable

		Matrix3D IRenderable.TransformationMatrix(Table.Table table, Origin origin) => _meshGenerator.GetPostMatrix(table, origin);

		public RenderObject GetRenderObject(Table.Table table, string id = null, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObject(table, id, origin, asRightHanded);
		}

		public RenderObjectGroup GetRenderObjects(Table.Table table, Origin origin = Origin.Global, bool asRightHanded = true)
		{
			return _meshGenerator.GetRenderObjects(table, origin, asRightHanded);
		}

		#endregion

		public HitObject[] GetHitShapes()
		{
			return new HitObject[] {_hitGate}
				.Concat(_hitLines ?? new HitObject[0])
				.Concat(_hitCircles ?? new HitObject[0])
				.ToArray();
		}
		public bool IsCollidable => Data.IsCollidable;
	}
}
