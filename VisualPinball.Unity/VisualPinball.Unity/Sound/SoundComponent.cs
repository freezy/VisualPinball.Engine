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
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// Base component for playing a <c>SoundAsset</c> using the public methods <c>Play</c> and <c>Stop</c>.
	/// </summary>
	[PackAs("Sound")]
	[AddComponentMenu("Visual Pinball/Sound/Sound")]
	public class SoundComponent : EnableAfterAwakeComponent, IPackable
	{
		[FormerlySerializedAs("_soundAsset")]
		public SoundAsset SoundAsset;

		[FormerlySerializedAs("_interrupt")]
		[Tooltip("Should the sound be interrupted if it is triggered again while already playing?")]
		public bool Interrupt;

		[FormerlySerializedAs("_volume")]
		[SerializeField, Range(0f, 1f)]
		public float Volume = 1f;

		private CancellationTokenSource _instantCts;
		private CancellationTokenSource _allowFadeCts;
		private float _lastPlayStartTime = float.NegativeInfinity;
		protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		#region Packaging

		public byte[] Pack() => SoundPackable.Pack(this);

		public byte[] PackReferences(Transform root, PackNameLookup lookup, PackagedFiles files) =>
			SoundReferencesPackable.PackReferences(this, files);

		public void Unpack(byte[] bytes) => SoundPackable.Unpack(bytes, this);

		public void UnpackReferences(byte[] data, Transform root, PackNameLookup lookup, PackagedFiles files)
			=> SoundReferencesPackable.Unpack(data, this, files);

		#endregion

		protected override void OnEnableAfterAfterAwake()
		{
			base.OnEnableAfterAfterAwake();
			_instantCts = new CancellationTokenSource();
			_allowFadeCts = new CancellationTokenSource();
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
			if (SoundAsset == null) {
				Logger.Warn("Cannot play without sound asset. Assign it in the inspector.");
				return;
			}
			float timeSinceLastPlay = Time.unscaledTime - _lastPlayStartTime;
			if (timeSinceLastPlay < 0.01f) {
				Logger.Warn($"Sound spam protection engaged. Time since last play was less than " +
					$"0.01 seconds ({timeSinceLastPlay}). There is probably something wrong with " +
					$"the calling code.");
				return;
			}

			if (Interrupt) {
				Stop(allowFade: true);
			}
			try {
				var combinedVol = Volume * volume;
				_lastPlayStartTime = Time.unscaledTime;
				await SoundUtils.Play(SoundAsset, gameObject, _allowFadeCts.Token, _instantCts.Token, combinedVol);
			} catch (OperationCanceledException) { }
		}

		public void Stop(bool allowFade)
		{
			if (!isActiveAndEnabled) {
				return;
			}
			if (allowFade) {
				_allowFadeCts?.Cancel();
				_allowFadeCts?.Dispose();
				_allowFadeCts = new CancellationTokenSource();
			} else {
				_instantCts?.Cancel();
				_instantCts?.Dispose();
				_instantCts = new CancellationTokenSource();
			}
		}

		public virtual bool SupportsLoopingSoundAssets() => true;

		public virtual Type GetRequiredType() => null;
	}
}
