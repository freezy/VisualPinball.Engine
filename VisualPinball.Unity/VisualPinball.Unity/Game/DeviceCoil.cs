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

using System;

namespace VisualPinball.Unity
{
	public class DeviceCoil: IApiCoil
	{
		public bool IsEnabled;

		protected Action OnEnable;
		protected Action OnDisable;

		public DeviceCoil(Action onEnable = null, Action onDisable = null)
		{
			OnEnable = onEnable;
			OnDisable = onDisable;
		}

		public void OnCoil(bool enabled)
		{
			IsEnabled = enabled;
			if (enabled) {
				OnEnable?.Invoke();
			} else {
				OnDisable?.Invoke();
			}
#if UNITY_EDITOR
			UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
		}

		public void OnChange(bool enabled) => OnCoil(enabled);
	}
}
