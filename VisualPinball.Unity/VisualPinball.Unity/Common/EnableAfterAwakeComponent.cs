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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// MonoBehaviour.OnEnable is always called after Awake on the same instance,
	/// but not across all instances. This is often inconvenient. This base class
	/// provides the virtual method OnEnableAfterAwake, which always runs after all
	/// instances have run their Awake methods.
	/// </summary>
	public abstract class EnableAfterAwakeComponent : MonoBehaviour
	{
		private bool _wasStartCalled;

		private void Start()
		{
			_wasStartCalled = true;
			OnEnableAfterAfterAwake();
		}

		private void OnEnable()
		{
			if (_wasStartCalled) {
				OnEnableAfterAfterAwake();
			}
		}

		protected virtual void OnEnableAfterAfterAwake()
		{
		}
	}
}
