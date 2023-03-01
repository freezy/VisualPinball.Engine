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
using System.Collections.Generic;
using System.Linq;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Sounds/Mechanical Sounds")]
	[RequireComponent(typeof(AudioSource))]
	public class MechSoundsComponent : MonoBehaviour
	{
		[SerializeField]
		public List<MechSound> Sounds = new();
		
		[NonSerialized]
		private ISoundEmitter _soundEmitter;
		[NonSerialized]
		private AudioSource _audioSource;
		[NonSerialized]
		private Dictionary<string, MechSound> _sounds = new();
		
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private void Awake()
		{
			_soundEmitter = GetComponent<ISoundEmitter>();
			_audioSource = GetComponent<AudioSource>();
			
			_sounds = Sounds.ToDictionary(s => s.TriggerId, s => s);
		}

		private void Start()
		{
			if (_soundEmitter != null && _audioSource) {
				_soundEmitter.OnSound += EmitSound;

			} else {
				Logger.Warn($"Cannot initialize mech sound for {name} due to missing ISoundEmitter or AudioSource.");
			}
		}

		private void OnDestroy()
		{
			if (_soundEmitter != null) {
				_soundEmitter.OnSound -= EmitSound;
			}
		}

		private void EmitSound(object sender, SoundEventArgs e)
		{
			if (_sounds.ContainsKey(e.TriggerId)) {
				_sounds[e.TriggerId].Sound.Play(_audioSource, e.Volume);
				Debug.Log($"Playing sound {e.TriggerId} for {name}");
				
			} else {
				Debug.LogError($"Unknown trigger {e.TriggerId} for {name}");
			}
		}
	}
}

