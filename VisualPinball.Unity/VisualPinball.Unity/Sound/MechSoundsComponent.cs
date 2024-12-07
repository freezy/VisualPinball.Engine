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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using UnityEngine;
using UnityEngine.Audio;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Sounds/Mechanical Sounds")]
	public class MechSoundsComponent : MonoBehaviour
	{
		[SerializeField]
		private List<MechSound> _sounds = new();

		[SerializeField]
		private AudioMixerGroup _audioMixerGroup;

		private ISoundEmitter _soundEmitter;
		private CancellationTokenSource tcs;

		private void OnEnable()
		{
			_soundEmitter = GetComponent<ISoundEmitter>();
			tcs = new();
			foreach (MechSound sound in _sounds)
				sound.Enable(_soundEmitter, gameObject, tcs.Token);
		}

		private void OnDisable()
		{
			tcs.Cancel();
			tcs.Dispose();
			tcs = null;
			foreach (MechSound sound in _sounds)
				sound.Disable();
		}
	}
}

