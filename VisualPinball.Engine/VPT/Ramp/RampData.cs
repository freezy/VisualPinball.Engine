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

#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Ramp
{
	[Serializable]
	public class RampData : ItemData, IPhysicalData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 9)]
		public string Name = string.Empty;

		[BiffFloat("RADB", Pos = 24)]
		public float DepthBias = 0f;  // FP: no 

		[BiffDragPoint("DPNT", TagAll = true, Pos = 2000)]
		public DragPointData[] DragPoints;  // FP: yes (ramp points)

		[BiffFloat("ELAS", Pos = 19)]
		public float Elasticity;  // FP: no 

		[BiffFloat("RFCT", Pos = 20)]
		public float Friction;  // FP: no (physics material)

		[BiffBool("HTEV", Pos = 17)]
		public bool HitEvent = false;  // FP: no 

		[BiffFloat("HTBT", Pos = 1)]
		public float HeightBottom = 0f;  // FP: yes

		[BiffFloat("HTTP", Pos = 2)]
		public float HeightTop = 50f;  // FP: yes

		[BiffInt("ALGN", Pos = 11)]
		public int ImageAlignment = RampImageAlignment.ImageModeWorld;  // FP: no 

		[BiffBool("IMGW", Pos = 12)]
		public bool ImageWalls = true;  // FP: yes? (texture?)

		[BiffBool("CLDR", Pos = 22)]
		public bool IsCollidable = true;  // FP: no 

		[BiffBool("REEN", Pos = 28)]
		public bool IsReflectionEnabled = true;  // FP: yes (reflects_off_playfield)

		[BiffBool("RVIS", Pos = 23)]
		public bool IsVisible = true;  // FP: no 

		[BiffFloat("WLHL", Pos = 13)]
		public float LeftWallHeight = 62f;  // FP: yes (left_side_height)

		[BiffFloat("WVHL", Pos = 15)]
		public float LeftWallHeightVisible = 30f;  // FP: no 

		[BiffBool("OVPH", Pos = 30)]
		public bool OverwritePhysics = true;  // FP: no 

		[BiffInt("TYPE", Pos = 8)]
		public int RampType = VisualPinball.Engine.VPT.RampType.RampTypeFlat;

		[BiffFloat("WLHR", Pos = 14)]
		public float RightWallHeight = 62f;// FP: ]yes (right_side_height)


		[BiffFloat("WVHR", Pos = 16)]
		public float RightWallHeightVisible = 30f;  // FP: no 

		[BiffFloat("RSCT", Pos = 21)]
		public float Scatter;  // FP: no 

		[TextureReference]
		[BiffString("IMAG", Pos = 10)]
		public string Image = string.Empty;  // FP: yes (texture) 

		[MaterialReference]
		[BiffString("MATR", Pos = 5)]
		public string Material = string.Empty;  // FP: no 

		[MaterialReference]
		[BiffString("MAPH", Pos = 29)]
		public string PhysicsMaterial = string.Empty;  // FP: no 

		[BiffFloat("THRS", Pos = 18)]
		public float Threshold;  // FP: no 

		[BiffFloat("WDBT", Pos = 3)]
		public float WidthBottom = 75f;  // FP: yes (start_width)

		[BiffFloat("WDTP", Pos = 4)]
		public float WidthTop = 60f;  // FP: yes (end_width)

		[BiffFloat("RADI", Pos = 25)]
		public float WireDiameter = 8f;  // FP: no (for wireramps)

		[BiffFloat("RADX", Pos = 26)]
		public float WireDistanceX = 38f;  // FP: no 

		[BiffFloat("RADY", Pos = 27)]
		public float WireDistanceY = 88f;  // FP: no 

		[BiffBool("TMON", Pos = 6)]
		public bool IsTimerEnabled;  // FP: no 

		[BiffInt("TMIN", Pos = 7)]
		public int TimerInterval;  // FP: no 

		[BiffTag("PNTS", Pos = 1999)]
		public bool Points;  // FP: yes

		//FP+ : model (for modelramps), transparency, profile_type, color, sphere_mapping

		public RampData(string name, DragPointData[] dragPoints) : base(StoragePrefix.GameItem)
		{
			Name = name;
			DragPoints = dragPoints;
		}

		#region BIFF

		static RampData()
		{
			Init(typeof(RampData), Attributes);
		}

		public RampData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Ramp);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion

		// IPhysicalData
		public float GetElasticity() => Elasticity;
		public float GetElasticityFalloff() => 0;
		public float GetFriction() => Friction;
		public float GetScatter() => Scatter;
		public bool GetOverwritePhysics() => OverwritePhysics;
		public bool GetIsCollidable() => IsCollidable;
		public string GetPhysicsMaterial() => PhysicsMaterial;
	}
}
