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

using VisualPinball.Engine.Game;

namespace VisualPinball.Unity
{
	/// <summary>
	/// This struct passes an event triggered in a system to the main thread
	/// where it's dispatched to the API.
	/// </summary>
	public readonly struct EventData
	{
		public readonly EventId EventId;
		public readonly int ItemId;
		public readonly int BallId;
		public readonly float FloatParam;
		public readonly bool GroupEvent;

		public EventData(EventId eventId, int itemId, int ballId, bool groupEvent = false) : this()
		{
			EventId = eventId;
			ItemId = itemId;
			BallId = ballId;
			GroupEvent = groupEvent;
		}

		public EventData(EventId eventId, int itemId, int ballId, float floatParam, bool groupEvent = false) : this()
		{
			EventId = eventId;
			ItemId = itemId;
			BallId = ballId;
			FloatParam = floatParam;
			GroupEvent = groupEvent;
		}


		public EventData(EventId eventId, int itemId, bool groupEvent = false) : this()
		{
			EventId = eventId;
			ItemId = itemId;
			BallId = 0;
			GroupEvent = groupEvent;
		}

		public EventData(EventId eventId, int itemId, float floatParam, bool groupEvent = false) : this()
		{
			EventId = eventId;
			ItemId = itemId;
			BallId = 0;
			FloatParam = floatParam;
			GroupEvent = groupEvent;
		}

		public override string ToString() => $"Event {EventId} for item {ItemId} by ball {BallId} ({FloatParam})";
	}
}
