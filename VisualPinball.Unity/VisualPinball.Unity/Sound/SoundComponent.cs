// Visual Pinball Engine
// Copyright (C) 2025 freezy and VPE Team
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
using System.Threading;
using NLog;
using UnityEngine;
using Logger = NLog.Logger;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Base component for playing a <c>SoundAsset</c> using the public methods <c>Play</c> and <c>Stop</c>.
	/// </summary>
	[AddComponentMenu("Visual Pinball/Sound/Sound")]
	public class SoundComponent : EnableAfterAwakeComponent
	{
		[SerializeReference]
		protected SoundAsset _soundAsset;

		[SerializeField]
		[Tooltip("Should the sound be interrupted if it is triggered again while already playing?")]
		protected bool _interrupt;

		[SerializeField, Range(0f, 1f)]
		private float _volume = 1f;

		[SerializeField]
		private SoundPriority _priority = SoundPriority.Medium;

		[SerializeField]
		private float _maxQueueTime = -1;

		private CancellationTokenSource _instantCts;
		private CancellationTokenSource _allowFadeCts;
		private CalloutCoordinator _calloutCoordinator;
		private List<int> _calloutRequestIds = new();
		private MusicCoordinator _musicCoordinator;
		private List<int> _musicRequestIds = new();
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			_calloutCoordinator = GetComponentInParent<CalloutCoordinator>();
			_musicCoordinator = GetComponentInParent<MusicCoordinator>();
		}

		protected virtual void OnDisable()
		{
			StopAllSounds(allowFade: true);
		}

		protected async void StartSound()
		{
			if (!isActiveAndEnabled)
			{
				Logger.Warn("Cannot play a disabled sound component.");
				return;
			}

			if (_soundAsset == null)
			{
				Logger.Warn("Cannot play without sound asset. Assign it in the inspector.");
				return;
			}

			if (_interrupt)
				StopAllSounds(allowFade: true);

			_allowFadeCts ??= new();
			_instantCts ??= new();

			try
			{
				if (_soundAsset is SoundEffectAsset)
				{
					await ((SoundEffectAsset)_soundAsset).Play(
						gameObject,
						_allowFadeCts.Token,
						_instantCts.Token,
						_volume
					);
				}
				else if (_soundAsset is CalloutAsset)
				{
					var request = new CalloutRequest(
						(CalloutAsset)_soundAsset,
						_priority,
						_maxQueueTime,
						_volume
					);
					_calloutCoordinator.EnqueueCallout(request, out var requestId);
					_calloutRequestIds.Add(requestId);
				}
				else if (_soundAsset is MusicAsset)
				{
					var request = new MusicRequest((MusicAsset)_soundAsset, _priority, _volume);
					_musicCoordinator.AddRequest(request, out var requestId);
					_musicRequestIds.Add(requestId);
				}
				else
				{
					throw new NotImplementedException(
						$"Unknown type of sound asset '{_soundAsset.GetType()}'"
					);
				}
			}
			catch (OperationCanceledException) { }
		}

		protected void StopAllSounds(bool allowFade)
		{
			if (allowFade)
				_allowFadeCts?.Cancel();
			else
				_instantCts?.Cancel();

			_allowFadeCts?.Dispose();
			_allowFadeCts = null;
			_instantCts?.Dispose();
			_instantCts = null;

			foreach (var id in _calloutRequestIds)
				_calloutCoordinator.DequeueCallout(id);
			_calloutRequestIds.Clear();

			foreach (var id in _musicRequestIds)
				_musicCoordinator.RemoveRequest(id);
			_musicRequestIds.Clear();
		}

		public virtual bool SupportsLoopingSoundAssets() => true;

		public virtual Type GetRequiredType() => null;
	}
}
