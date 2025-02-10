// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
	/// <summary>
	/// Start and or stop a sound when an event occurs.
	/// </summary>
	public abstract class EventSoundComponent<TEventSource, TEventArgs> : SoundComponent where TEventSource : class
	{
		private TEventSource _eventSource;

		protected abstract bool TryFindEventSource(out TEventSource eventSource);
		protected abstract void OnEvent(object sender, TEventArgs e);
		protected abstract void Subscribe(TEventSource eventSource);
		protected abstract void Unsubscribe(TEventSource eventSource);

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			if (TryFindEventSource(out _eventSource)) {
				Subscribe(_eventSource);
			} else {
				Logger.Warn(
					$"Could not find sound event source of type {typeof(TEventSource).Name} on "
						+ $"game object '{name}.' Make sure an appropriate component is attached."
				);
			}
		}

		protected override void OnDisable()
		{
			base.OnDisable();
			if (_eventSource != null)
			{
				Unsubscribe(_eventSource);
				_eventSource = null;
			}
		}
	}
}
