// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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

#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Math
{
	[Serializable]
	public class DragPointData : BiffData
	{
		public string Id;
		
		[BiffVertex("VCEN", Pos = 1, WriteAsVertex2D = true)]
		public Vertex3D Center;

		[BiffFloat("POSZ", Pos = 2)]
		public float PosZ { set => Center.Z = value; get => Center.Z; }

		[BiffBool("SMTH", Pos = 3)]
		public bool IsSmooth;

		[BiffBool("SLNG", Pos = 4)]
		public bool IsSlingshot;

		[BiffBool("ATEX", Pos = 5)]
		public bool HasAutoTexture;

		[BiffFloat("TEXC", Pos = 6)]
		public float TextureCoord;

		[BiffBool("LOCK", Pos = 7)]
		public bool IsLocked;

		[BiffInt("LAYR", Pos = 8)]
		public int EditorLayer;

		[BiffString("LANR", Pos = 9, WasAddedInVp107 = true)]
		public string EditorLayerName  = string.Empty;

		[BiffBool("LVIS", Pos = 10, WasAddedInVp107 = true)]
		public bool EditorLayerVisibility = true;
		
		public float CalcHeight;

		[ExcludeFromCodeCoverage]
		public override string ToString()
		{
			return $"DragPoint({Center.X}/{Center.Y}/{Center.Z}, {(IsSmooth ? "S" : "")}{(IsSlingshot ? "SS" : "")}{(HasAutoTexture ? "A" : "")})";
		}

		public DragPointData AssertId()
		{
			Id ??= GenerateId();
			return this;
		}

		public DragPointData Lerp(DragPointData dp, float pos)
		{
			return new DragPointData(Center + pos * (dp.Center - Center)) {
				IsSmooth = dp.IsSmooth,
				IsSlingshot = dp.IsSlingshot,
				HasAutoTexture = dp.HasAutoTexture,
				TextureCoord = dp.TextureCoord,
				IsLocked = dp.IsLocked,
				EditorLayer = dp.EditorLayer,
				EditorLayerName = dp.EditorLayerName,
				EditorLayerVisibility = EditorLayerVisibility
			};
		}

		public DragPointData Translate(Vertex3D v)
		{
			Center += v;
			return this;
		}

		public DragPointData Clone()
		{
			return new DragPointData(Center) {
				PosZ = PosZ,
				IsSmooth = IsSmooth,
				IsSlingshot = IsSlingshot,
				HasAutoTexture = HasAutoTexture,
				TextureCoord = TextureCoord,
				IsLocked = IsLocked,
				EditorLayer = EditorLayer,
				EditorLayerName = EditorLayerName,
				EditorLayerVisibility = EditorLayerVisibility,
				CalcHeight = CalcHeight,
			};
		}

		#region BIFF

		static DragPointData()
		{
			Init(typeof(DragPointData), Attributes);
		}

		public DragPointData(Vertex3D center) : base(null)
		{
			Id = GenerateId();
			Center = center;
			HasAutoTexture = true;
		}

		public DragPointData(float x, float y) : base(null)
		{
			Id = GenerateId();
			Center = new Vertex3D(x, y, 0f);
			HasAutoTexture = true;
		}

		public DragPointData(DragPointData rf) : base(null)
		{
			Id = GenerateId();
			Center = rf.Center;
			PosZ = rf.PosZ;
			IsSmooth = rf.IsSmooth;
			IsSlingshot = rf.IsSlingshot;
			HasAutoTexture = rf.HasAutoTexture;
			TextureCoord = rf.TextureCoord;
			IsLocked = rf.IsLocked;
			EditorLayer = rf.EditorLayer;
			CalcHeight = rf.CalcHeight;
		}

		public DragPointData(BinaryReader reader) : base(null)
		{
			Load(this, reader, Attributes);
			Id = GenerateId();
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
		
		private static string GenerateId() => Guid.NewGuid().ToString()[..8];

		#endregion

	}
}
