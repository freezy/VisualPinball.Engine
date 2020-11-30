// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using Unity.Entities;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	/// <summary>
	/// This struct passes an event triggered in a system to the main thread
	/// where it's dispatched to the API.
	/// </summary>
	public readonly struct EventData
	{
		public readonly EventId eventId;
		public readonly Entity ItemEntity;
		public readonly float FloatParam;
		public readonly bool GroupEvent;

		public EventData(EventId eventId, Entity itemEntity, bool groupEvent = false) : this()
		{
			this.eventId = eventId;
			ItemEntity = itemEntity;
			GroupEvent = groupEvent;
		}

		public EventData(EventId eventId, Entity itemEntity, float floatParam, bool groupEvent = false) : this()
		{
			this.eventId = eventId;
			ItemEntity = itemEntity;
			FloatParam = floatParam;
			GroupEvent = groupEvent;
		}
	}
}
