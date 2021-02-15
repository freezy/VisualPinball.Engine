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

namespace VisualPinball.Engine.VPT.Surface
{
	[Serializable]
	public class SurfaceData : ItemData, IPhysicalData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 16)]
		public string Name = string.Empty;

		[BiffBool("HTEV", Pos = 1)]
		public bool HitEvent = false;

		[BiffBool("DROP", Pos = 2)]
		public bool IsDroppable = false;

		[BiffBool("FLIP", Pos = 3)]
		public bool IsFlipbook = false;

		[BiffBool("ISBS", Pos = 4)]
		public bool IsBottomSolid = false;

		[BiffBool("CLDW", Pos = 5)]
		public bool IsCollidable = true;

		[BiffFloat("THRS", Pos = 8)]
		public float Threshold = 2.0f;

		[TextureReference]
		[BiffString("IMAG", Pos = 9)]
		public string Image = string.Empty;

		[TextureReference]
		[BiffString("SIMG", Pos = 10)]
		public string SideImage = string.Empty;

		[MaterialReference]
		[BiffString("SIMA", Pos = 11)]
		public string SideMaterial = string.Empty;

		[MaterialReference]
		[BiffString("TOMA", Pos = 12)]
		public string TopMaterial = string.Empty;

		[MaterialReference]
		[BiffString("MAPH", Pos = 30)]
		public string PhysicsMaterial = string.Empty;

		[MaterialReference]
		[BiffString("SLMA", Pos = 13)]
		public string SlingShotMaterial = string.Empty;

		[BiffFloat("HTBT", Pos = 14)]
		public float HeightBottom = 0f;

		[BiffFloat("HTTP", Pos = 15)]
		public float HeightTop = 50f;

		[BiffBool("INNR", SkipWrite = true)]
		public bool Inner = true;

		[BiffBool("DSPT", Pos = 17)]
		public bool DisplayTexture = false;

		[BiffFloat("SLGF", Pos = 18)]
		public float SlingshotForce = 80f;

		[BiffFloat("SLTH", Pos = 19)]
		public float SlingshotThreshold = 0f;

		[BiffBool("SLGA", Pos = 25)]
		public bool SlingshotAnimation = true;

		[BiffFloat("ELAS", Pos = 20)]
		public float Elasticity;

		[BiffFloat("ELFO", Pos = 21, WasAddedInVp107 = true)]
		public float ElasticityFalloff;

		[BiffFloat("WFCT", Pos = 22)]
		public float Friction;

		[BiffFloat("WSCT", Pos = 23)]
		public float Scatter;

		[BiffBool("VSBL", Pos = 24)]
		public bool IsTopBottomVisible = true;

		[BiffBool("OVPH", Pos = 31)]
		public bool OverwritePhysics = true;

		[BiffFloat("DILI", QuantizedUnsignedBits = 8, Pos = 27)]
		public float DisableLightingTop;

		[BiffFloat("DILB", Pos = 28)]
		public float DisableLightingBelow;

		[BiffBool("SVBL", Pos = 26)]
		public bool IsSideVisible = true;

		[BiffBool("REEN", Pos = 29)]
		public bool IsReflectionEnabled = true;

		[BiffDragPoint("DPNT", TagAll = true, Pos = 2000)]
		public DragPointData[] DragPoints;

		[BiffBool("TMON", Pos = 6)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 7)]
		public int TimerInterval;

		[BiffTag("PNTS", Pos = 1999)]
		public bool Points;

		// IPhysicalData
		public float GetElasticity() => Elasticity;
		public float GetElasticityFalloff() => ElasticityFalloff;
		public float GetFriction() => Friction;
		public float GetScatter() => Scatter;
		public bool GetOverwritePhysics() => OverwritePhysics;
		public bool GetIsCollidable() => IsCollidable;
		public string GetPhysicsMaterial() => PhysicsMaterial;

		// non-persisted
		public bool IsDisabled;

		public SurfaceData(string name, DragPointData[] dragPoints) : base(StoragePrefix.GameItem)
		{
			Name = name;
			DragPoints = dragPoints;
		}

		#region BIFF

		static SurfaceData()
		{
			Init(typeof(SurfaceData), Attributes);
		}

		public SurfaceData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Surface);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
