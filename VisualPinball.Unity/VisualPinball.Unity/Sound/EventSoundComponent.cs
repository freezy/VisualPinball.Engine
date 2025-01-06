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

namespace VisualPinball.Unity
{
	public abstract class EventSoundComponent<EventSourceType, EventArgsType> : SoundComponent where EventSourceType : class
	{
		private EventSourceType _eventSource;

		protected abstract bool TryFindEventSource(out EventSourceType eventSource);
		protected abstract void OnEvent(object sender, EventArgsType e);
		protected abstract void Subscribe(EventSourceType eventSource);
		protected abstract void Unsubscribe(EventSourceType eventSource);

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			if (TryFindEventSource(out _eventSource))
				Subscribe(_eventSource);
			else
                Logger.Warn($"Could not find sound event source of type {typeof(EventSourceType).Name}." +
                $" Make sure an appropriate component is attached");
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			if (_eventSource != null) {
				Unsubscribe(_eventSource);
				_eventSource = null;
			}
		}
	}
}
