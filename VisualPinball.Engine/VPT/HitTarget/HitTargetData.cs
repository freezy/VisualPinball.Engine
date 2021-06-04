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

namespace VisualPinball.Engine.VPT.HitTarget
{
	[Serializable]
	public class HitTargetData : ItemData, IPhysicalData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 6)]
		public string Name = string.Empty; 

		[BiffFloat("PIDB", Pos = 20)]
		public float DepthBias; // FP: no 

		[BiffFloat("DILB", Pos = 18)]
		public float DisableLightingBelow; // FP: no 

		[BiffFloat("DILI", QuantizedUnsignedBits = 8, Pos = 17)]
		public float DisableLightingTop; // FP: no 

		[BiffFloat("DRSP", Pos = 22)]
		public float DropSpeed =  0.5f; // FP: no 

		[BiffBool("REEN", Pos = 19)]
		public bool IsReflectionEnabled = true; // FP: yes 

		[BiffInt("RADE", Pos = 25)]
		public int RaiseDelay = 100; // FP: no 

		[BiffFloat("ELAS", Pos = 12)]
		public float Elasticity; // FP: no 

		[BiffFloat("ELFO", Pos = 13)]
		public float ElasticityFalloff; // FP: no 

		[BiffFloat("RFCT", Pos = 14)]
		public float Friction; // FP: no 

		[BiffBool("CLDR", Pos = 16)]
		public bool IsCollidable = true; // FP: no (always)

		[BiffBool("ISDR", Pos = 21)]
		public bool IsDropped = false; // FP: no 

		[BiffBool("TVIS", Pos = 8)]
		public bool IsVisible = true; // FP: no 

		[BiffBool("LEMO", Pos = 9)]
		public bool IsLegacy = false; // FP: no 

		[BiffBool("OVPH", Pos = 27)]
		public bool OverwritePhysics = false; // FP: no 

		[BiffFloat("ROTZ", Pos = 3)]
		public float RotZ = 0f; // FP: yes (rotation)

		[BiffFloat("RSCT", Pos = 15)]
		public float Scatter; // FP: no 

		[TextureReference]
		[BiffString("IMAG", Pos = 4)]
		public string Image = string.Empty; // FP: yes (texture)

		[MaterialReference]
		[BiffString("MATR", Pos = 7)]
		public string Material = string.Empty; // FP: no 

		[MaterialReference]
		[BiffString("MAPH", Pos = 26)]
		public string PhysicsMaterial = string.Empty; // FP: no 

		// FP: Actually, two kinds of "targets": leaf target and drop target banks (generating multiple drop targets)
		[BiffInt("TRTY", Pos = 5)]
		public int TargetType = VisualPinball.Engine.VPT.TargetType.DropTargetSimple;

		[BiffFloat("THRS", Pos = 11)]
		public float Threshold = 2.0f; // FP: no 

		[BiffBool("HTEV", Pos = 10)]
		public bool UseHitEvent = true; // FP: no 

		[BiffVertex("VPOS", IsPadded = true, Pos = 1)]
		public Vertex3D Position = new Vertex3D(); // FP: yes

		[BiffVertex("VSIZ", IsPadded = true, Pos = 2)]
		public Vertex3D Size = new Vertex3D(32, 32, 32); // FP: no 

		[BiffBool("TMON", Pos = 23)]
		public bool IsTimerEnabled; // FP: no 

		[BiffInt("TMIN", Pos = 24)]
		public int TimerInterval; // FP: no 

		// FP+: Model, color, sound when hit, sound when released

		public bool IsDropTarget =>
			   TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetBeveled
			|| TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetFlatSimple
			|| TargetType == VisualPinball.Engine.VPT.TargetType.DropTargetSimple;

		public HitTargetData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Position = new Vertex3D(x, y, 0f);
		}

		#region BIFF

		static HitTargetData()
		{
			Init(typeof(HitTargetData), Attributes);
		}

		public HitTargetData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.HitTarget);
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
