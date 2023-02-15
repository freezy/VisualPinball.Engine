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

using System;
using System.Collections.Generic;
using VisualPinball.Engine.IO;
using System.IO;
using VisualPinball.Engine.VPT.Table;
using UnityEngine;

namespace VisualPinball.Engine.VPT.MechSounds
{
	[Serializable]
	public class MechSoundsData : ItemData
	{

		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }

		[BiffString("NAME", IsWideString = true, Pos = 14)]
		public string Name = string.Empty;
		public SoundTrigger[] AvailableTriggers;
		public SoundTrigger SelectedTrigger;
		public VolumeEmitter[] AvailableEmitters;

		


		#region BIFF


		public MechSoundsData() : base(StoragePrefix.VpeGameItem)
		{
		}

		public MechSoundsData(string name) : base(StoragePrefix.VpeGameItem)
		{
			Name = name;
		}

		static MechSoundsData()
		{
			Init(typeof(MechSoundsData), Attributes);
		}

		public MechSoundsData(BinaryReader reader, string storageName) : this(storageName)
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			writer.Write((int)ItemType.Flipper);
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion


	}
}

