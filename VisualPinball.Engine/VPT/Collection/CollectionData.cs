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
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.VPT.Collection
{
	[Serializable]
	public class CollectionData : ItemData
	{
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 1)]
		public string Name;

		[BiffString("ITEM", IsWideString = true, TagAll = true, Pos = 2)]
		public string[] ItemNames;

		[BiffBool("EVNT", Pos = 3)]
		public bool FireEvents = false;

		[BiffBool("GREL", Pos = 5)]
		public bool GroupElements = true;

		[BiffBool("SSNG", Pos = 4)]
		public bool StopSingleEvents = false;

		protected override bool SkipWrite(BiffAttribute attr)
		{
			switch (attr.Name) {
				case "LOCK":
				case "LAYR":
				case "LANR":
				case "LVIS":
					return true;
			}
			return false;
		}

		public CollectionData Clone(string name = null)
		{
			var clone = new CollectionData(string.IsNullOrEmpty(name) ? string.Empty : name) {
				FireEvents = this.FireEvents,
				GroupElements = this.GroupElements,
				StopSingleEvents = this.StopSingleEvents,
				ItemNames = this.ItemNames
			};
			return clone;
		}

		#region BIFF

		static CollectionData()
		{
			Init(typeof(CollectionData), Attributes);
		}

		public CollectionData(string name) : base(StoragePrefix.Collection)
		{
			Name = name;
		}

		public CollectionData(string name, CollectionData data) : base(StoragePrefix.Collection)
		{
			Name = name;
			FireEvents = data.FireEvents;
			GroupElements = data.GroupElements;
			StopSingleEvents = data.StopSingleEvents;
			ItemNames = new string[data.ItemNames.Length];
			data.ItemNames.CopyTo(ItemNames, 0);
		}

		public CollectionData(BinaryReader reader, string storageName) : base(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
	}
}
