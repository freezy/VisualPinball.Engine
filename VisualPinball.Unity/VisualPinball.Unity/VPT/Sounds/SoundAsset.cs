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
using System;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	/// <summary>

	/// </summary>
	[CreateAssetMenu(fileName = "Sound", menuName = "Visual Pinball/Sound", order = 102)]
	public class SoundAsset : ScriptableObject
	{

		public string Name;
		[Space(15)]
		public string Description;

		[Range(0,1)]
		[Space(15)]
		public float VolumeCorrection = 1; //audio clips in unity have a volume range of 0 to 1

		[Space(15)]
		public AudioClip[] Clips;

		public enum Selection
		{
			RoundRobin,
			Random
		}

		[Space(15)]
		public Selection ClipSelection;

		[Range(0.1f, 2f)]
		[Space(15)]
		public float RandomizePitch = 1;

		[Range(0.1f, 2f)]
		[Space(15)]
		public float RandomizeSpeed = 1;

		[Range(0, 1f)]
		[Space(15)]
		public float RandomizeVolume = 1;

		void OnValidate()
		{

		}

		

	}
		
}
