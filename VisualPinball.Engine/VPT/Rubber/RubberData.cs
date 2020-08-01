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

namespace VisualPinball.Engine.VPT.Rubber
{
	[Serializable]
	public class RubberData : ItemData, IPhysicalData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 8)]
		public string Name;

		[BiffFloat("HTTP", Pos = 1)]
		public float Height = 25f;

		[BiffFloat("HTHI", Pos = 2)]
		public float HitHeight = -1.0f;

		[BiffInt("WDTP", Pos = 3)]
		public int Thickness = 8;

		[BiffBool("HTEV", Pos = 4)]
		public bool HitEvent = false;

		[MaterialReference]
		[BiffString("MATR", Pos = 5)]
		public string Material;

		[TextureReference]
		[BiffString("IMAG", Pos = 9)]
		public string Image;

		[BiffFloat("ELAS", Pos = 10)]
		public float Elasticity;

		[BiffFloat("ELFO", Pos = 11)]
		public float ElasticityFalloff;

		[BiffFloat("RFCT", Pos = 12)]
		public float Friction;

		[BiffFloat("RSCT", Pos = 13)]
		public float Scatter;

		[BiffBool("CLDR", Pos = 14)]
		public bool IsCollidable = true;

		[BiffBool("RVIS", Pos = 15)]
		public bool IsVisible = true;

		[BiffBool("REEN", Pos = 21)]
		public bool IsReflectionEnabled = true;

		[BiffBool("ESTR", Pos = 16)]
		public bool StaticRendering = true;

		[BiffBool("ESIE", Pos = 17)]
		public bool ShowInEditor = true;

		[BiffFloat("ROTX", Pos = 18)]
		public float RotX = 0f;

		[BiffFloat("ROTY", Pos = 19)]
		public float RotY = 0f;

		[BiffFloat("ROTZ", Pos = 20)]
		public float RotZ = 0f;

		[MaterialReference]
		[BiffString("MAPH", Pos = 22)]
		public string PhysicsMaterial;

		[BiffBool("OVPH", Pos = 23)]
		public bool OverwritePhysics = false;

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
		public float GetElasticityFalloff() => 0;
		public float GetFriction() => Friction;
		public float GetScatter() => Scatter;
		public bool GetOverwritePhysics() => OverwritePhysics;
		public bool GetIsCollidable() => IsCollidable;
		public string GetPhysicsMaterial() => PhysicsMaterial;

		#region BIFF

		static RubberData()
		{
			Init(typeof(RubberData), Attributes);
		}

		public RubberData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Rubber);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
