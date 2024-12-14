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
using System.Threading;
using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Sounds/Sound")]
	public class SoundComponent : MonoBehaviour
	{
		[SerializeReference]
		private SoundAsset _soundAsset;
		[SerializeField]
		[Tooltip("Should the sound be interrupted if it is triggered again while already playing?")]
		private bool _interrupt;
		[SerializeField]
		private string _triggerId;
		[SerializeField]
		private bool _hasStopTrigger;
		[SerializeField]
		private string _stopTriggerId;

		[SerializeField]
		[Range(0f, 1f)]
		private float _volume = 1f;

		private ISoundEmitter _emitter;
		private CancellationTokenSource _instantCts;
		private CancellationTokenSource _allowFadeCts;

		public void OnEnable()
		{
			_emitter = GetComponent<ISoundEmitter>();
			_emitter.OnSound += Emitter_OnSound;
			_instantCts = new();
			_allowFadeCts = new();
		}

		public void OnDisable()
		{
			_emitter.OnSound -= Emitter_OnSound;
			_emitter = null;
			_allowFadeCts?.Dispose();
			_allowFadeCts = null;
			_instantCts?.Cancel();
			_instantCts?.Dispose();
			_instantCts = null;
		}

		private async void Emitter_OnSound(object sender, SoundEventArgs e)
		{
			if (_hasStopTrigger && e.TriggerId == _stopTriggerId)
				StopRunningSounds();

			if (e.TriggerId == _triggerId) {
				if (_interrupt)
					StopRunningSounds();
				try {
					await SoundUtils.Play(_soundAsset, gameObject, _allowFadeCts.Token, _instantCts.Token, _volume);
				} catch (OperationCanceledException) { }
			}

			void StopRunningSounds()
			{
				_allowFadeCts?.Cancel();
				_allowFadeCts?.Dispose();
				_allowFadeCts = new();
			}
		}
	}
}
