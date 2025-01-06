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
using NLog;
using Logger = NLog.Logger;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Sound/Sound")]
	public class SoundComponent : EnableAfterAwakeMonoBehaviour
	{
		[SerializeReference]
		protected SoundAsset _soundAsset;
		[SerializeField]
		[Tooltip("Should the sound be interrupted if it is triggered again while already playing?")]
		protected bool _interrupt;
		[SerializeField, Range(0f, 1f)]
		private float _volume = 1f;

		private CancellationTokenSource _instantCts;
		private CancellationTokenSource _allowFadeCts;
		private float _lastPlayStartTime;
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			_instantCts = new();
			_allowFadeCts = new();
		}

		protected virtual void OnDisable()
		{
			_allowFadeCts?.Dispose();
			_allowFadeCts = null;
			_instantCts?.Cancel();
			_instantCts?.Dispose();
			_instantCts = null;
		}

		public async Task Play(float volume = 1f)
		{
			if (!isActiveAndEnabled) {
				Logger.Warn("Cannot play a disabled sound component.");
				return;
			}
			if (_soundAsset == null) {
				Logger.Warn("Cannot play without sound asset. Assign it in the inspector.");
				return;
			}
			float timeSinceLastPlay = Time.unscaledTime - _lastPlayStartTime;
			if (timeSinceLastPlay < 0.1f) {
				Logger.Warn("Sound spam protection engaged. There is probably something wrong with the calling code.");
				return;
			}

			if (_interrupt)
				Stop(allowFade: true);
			try {
				var combinedVol = _volume * volume;
				_lastPlayStartTime = Time.unscaledTime;
				await SoundUtils.Play(_soundAsset, gameObject, _allowFadeCts.Token, _instantCts.Token, combinedVol);
			} catch (OperationCanceledException) { }
		}

		public void Stop(bool allowFade)
		{
			if (!isActiveAndEnabled)
				return;
			if (allowFade) {
				_allowFadeCts?.Cancel();
				_allowFadeCts?.Dispose();
				_allowFadeCts = new();
			} else {
				_instantCts?.Cancel();
				_instantCts?.Dispose();
				_instantCts = new();
			}
		}

		public virtual bool SupportsLoopingSoundAssets() => true;

		public virtual Type GetRequiredType() => null;
	}
}
