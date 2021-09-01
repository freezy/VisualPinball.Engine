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
	public class GamelogicEngineCoil : IGamelogicEngineDeviceItem
	{
		public string Id { get => _id; set => _id = value; }
		public string Description { get => _description; set => _description = value; }

		public int InternalId;
		public string DeviceHint;
		public string DeviceItemHint;
		public bool IsLamp;

		/// <summary>
		/// Sometimes we want to add all coils that are in the manual, so this allows adding unused coils as such.
		/// </summary>
		public bool IsUnused;

		private string _description;
		private string _id;

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
