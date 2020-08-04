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

namespace VisualPinball.Engine.VPT.Spinner
{
	[Serializable]
	public class SpinnerData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 16)]
		public string Name;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffFloat("ROTA", Pos = 2)]
		public float Rotation = 0f;

		[MaterialReference]
		[BiffString("MATR", Pos = 13)]
		public string Material = string.Empty;

		[BiffBool("SSUP", Pos = 12)]
		public bool ShowBracket = true;

		[BiffFloat("HIGH", Pos = 5)]
		public float Height = 60f;

		[BiffFloat("LGTH", Pos = 6)]
		public float Length = 80f;

		[BiffFloat("AFRC", Pos = 7)]
		public float Damping = 0.9879f;

		[BiffFloat("SMAX", Pos = 8)]
		public float AngleMax = 0f;

		[BiffFloat("SMIN", Pos = 9)]
		public float AngleMin = 0f;

		[BiffFloat("SELA", Pos = 10)]
		public float Elasticity = 0.3f;

		[BiffBool("SVIS", Pos = 11)]
		public bool IsVisible = true;

		[TextureReference]
		[BiffString("IMGF", Pos = 14)]
		public string Image = string.Empty;

		[BiffString("SURF", Pos = 15)]
		public string Surface = string.Empty;

		[BiffBool("TMON", Pos = 3)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 4)]
		public int TimerInterval;

		public SpinnerData(string name, float x, float y) : base(StoragePrefix.GameItem)
		{
			Name = name;
			Center = new Vertex2D(x, y);
		}

		#region BIFF

		static SpinnerData()
		{
			Init(typeof(SpinnerData), Attributes);
		}

		public SpinnerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Spinner);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
