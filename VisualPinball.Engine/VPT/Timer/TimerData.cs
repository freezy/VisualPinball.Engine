#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Timer
{
	[Serializable]
	public class TimerData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 4)]
		public string Name;

		[BiffVertex("VCEN", Pos = 1)]
		public Vertex2D Center;

		[BiffBool("BGLS", Pos = 5)]
		public bool Backglass = false;

		[BiffBool("TMON", Pos = 2)]
		public bool IsTimerEnabled;

		[BiffInt("TMIN", Pos = 3)]
		public int TimerInterval;

		#region BIFF

		static TimerData()
		{
			Init(typeof(TimerData), Attributes);
		}

		public TimerData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Timer);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
