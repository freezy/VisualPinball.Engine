#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable ConvertToConstant.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Kicker
{
	[Serializable]
	public class KickerData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 8)]
		public string Name;

		[BiffInt("TYPE", Pos = 9)]
		public int KickerType = VisualPinball.Engine.VPT.KickerType.KickerHole;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffFloat("RADI", Pos = 2)]
		public float Radius = 25f;

		[BiffFloat("KSCT", Pos = 10)]
		public float Scatter = 0.0f;

		[BiffFloat("KHAC", Pos = 11)]
		public float HitAccuracy = 0.7f;

		[BiffFloat("KHHI", Pos = 12)]
		public float HitHeight = 40.0f;

		[BiffFloat("KORI", Pos = 13)]
		public float Orientation = 0.0f;

		[MaterialReference]
		[BiffString("MATR", Pos = 5)]
		public string Material;

		[BiffString("SURF", Pos = 6)]
		public string Surface;

		[BiffBool("FATH", Pos = 14)]
		public bool FallThrough = false;

		[BiffBool("EBLD", Pos = 7)]
		public bool IsEnabled = true;

		[BiffBool("LEMO", Pos = 15)]
		public bool LegacyMode = false;

		[BiffBool("TMON", Pos = 3)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 4)]
		public int TimerInterval;

		#region BIFF

		static KickerData()
		{
			Init(typeof(KickerData), Attributes);
		}

		public KickerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Kicker);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
