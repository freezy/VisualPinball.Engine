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


using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT;

namespace VisualPinball.Unity
{

	[AddComponentMenu("Visual Pinball/Sounds/Mechanical Sounds")]
	public class MechSoundsComponent : MonoBehaviour
	{
		#region Data

		[Serializable]
		public class MechSound
		{
			public int Trigger;
			public ScriptableObject Sound;
			public int Volume;
			public float VolumeValue = 1;
			public actionType Action = actionType.PlayOnce;
			public float Fade = 50;

		}

		void AddNew()
		{
			SoundList.Add(new MechSound());
		}

		void Remove(int index)
		{
			SoundList.RemoveAt(index);
		}

		
		public List<MechSound> SoundList = new List<MechSound>(1);
		public enum actionType { PlayOnce, Loop };

		#endregion

	}
}

