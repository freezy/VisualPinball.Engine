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
		public SoundAsset _soundAsset;
		[SerializeField]
		private string _triggerId;
		[SerializeField]
		private bool _hasStopTrigger;
		[SerializeField]
		private string _stopTriggerId;

		[Range(0.0001f, 1)]
		public float Volume = 1;

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
			if (_hasStopTrigger && e.TriggerId == _stopTriggerId) {
				_allowFadeCts?.Cancel();
				_allowFadeCts?.Dispose();
				_allowFadeCts = new();
			}

			if (e.TriggerId == _triggerId) {
				try {
					await SoundUtils.Play(_soundAsset, gameObject, _allowFadeCts.Token, _instantCts.Token);
				} catch (OperationCanceledException) { }
			}
		}
	}
}
