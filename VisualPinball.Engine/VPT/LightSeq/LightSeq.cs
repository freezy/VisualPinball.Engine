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

using System.IO;

namespace VisualPinball.Engine.VPT.LightSeq
{
	public class LightSeq : Item<LightSeqData>
	{
		public override string ItemName => "Light Sequence";
		public override string ItemGroupName => "Light Sequences";

		public LightSeq(LightSeqData data) : base(data)
		{
		}

		public LightSeq(BinaryReader reader, string itemName) : this(new LightSeqData(reader, itemName))
		{
		}
	}
}
