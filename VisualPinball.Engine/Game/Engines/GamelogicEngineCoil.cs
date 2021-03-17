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

// ReSharper disable InconsistentNaming

using System;

namespace VisualPinball.Engine.Game.Engines
{
	[Serializable]
	public class GamelogicEngineCoil
	{
		public string Id;
		public int InternalId;
		public string Description;
		public string PlayfieldItemHint;
		public string MainCoilIdOfHoldCoil;
		public string DeviceHint;
		public string DeviceItemHint;
		public bool IsLamp;
		public bool IsUnused;

		public GamelogicEngineCoil(string id)
		{
			Id = id;
			InternalId = int.TryParse(id, out var internalId) ? internalId : 0;
		}

		public GamelogicEngineCoil(string id, int internalId)
		{
			Id = id;
			InternalId = internalId;
		}
	}
}
