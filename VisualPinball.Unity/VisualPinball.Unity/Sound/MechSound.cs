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

// ReSharper disable InconsistentNaming

using System;
using UnityEngine;

namespace VisualPinball.Unity
{
	[Serializable]
	public class MechSound : ISerializationCallbackReceiver
	{
		[SerializeReference]
		public SoundAsset Sound;

		public string TriggerId;
		public bool HasStopTrigger;
		public string StopTriggerId;

		[Range(0.0001f, 1)]
		// This initialization doesnt work in inspector 
		public float Volume = 1;

		#region DefaultValuesWorkaround
		// When an instance is created by pressing the + icon on a list in the inspector,
		// Unity does not apply default values (such as Volume = 1) and no constructor is called.
		// See https://www.reddit.com/r/Unity3D/comments/j5i6cj/inspector_struct_default_values/.
		// This workaround applies default values the first time the struct is serialized instead.
		// It only works for the first instance in the list, because for any subsequent instance Unity
		// clones the field values of the previous instance, including the areDefaultsApplied flag.
		[SerializeField]
		private bool areDefaultsApplied = false;

		public void OnAfterDeserialize() { }
		public void OnBeforeSerialize()
		{
			if (!areDefaultsApplied) {
				Volume = 1;
				areDefaultsApplied = true;
			}
		}
		#endregion
	}
}
