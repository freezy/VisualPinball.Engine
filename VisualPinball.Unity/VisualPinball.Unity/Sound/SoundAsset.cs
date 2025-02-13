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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

namespace VisualPinball.Unity
{
	public enum SoundPriority
	{
		Lowest,
		Low,
		Medium,
		High,
		Highest,
	}

	/// <summary>
	/// Common base class for sound effect assets, callout assets, and music assets. Multiple audio
	/// clips can be assigned for variation. Instances of this class are Unity assets and can
	/// therefore be stored in the project files or in an asset library for reuse across tables.
	/// </summary>
	public abstract class SoundAsset : ScriptableObject
	{
		private enum SelectionMethod
		{
			RoundRobin,
			Random,
		}

		[SerializeField]
		private string _description;

		[SerializeField]
		private AudioClip[] _clips;

		[SerializeField]
		private SelectionMethod _clipSelectionMethod;

		[SerializeField]
		private AudioMixerGroup _audioMixerGroup;

		[NonSerialized]
		private int _roundRobinIndex = 0;

		public abstract bool Loop { get; }

		public virtual void ConfigureAudioSource(AudioSource audioSource)
		{
			audioSource.clip = GetClip();
			audioSource.outputAudioMixerGroup = _audioMixerGroup;
			audioSource.playOnAwake = false;
		}

		public bool IsValid()
		{
			if (_clips == null)
				return false;

			foreach (var clip in _clips)
			{
				if (clip != null)
					return true;
			}

			return false;
		}

		private AudioClip GetClip()
		{
			_clips.ToList().RemoveAll(clip => clip == null);
			if (_clips.Length == 0)
				throw new InvalidOperationException(
					$"The sound asset '{name}' has no audio clips to play."
				);
			switch (_clipSelectionMethod)
			{
				case SelectionMethod.RoundRobin:
					_roundRobinIndex %= _clips.Length;
					var clip = _clips[_roundRobinIndex];
					_roundRobinIndex++;
					return clip;
				case SelectionMethod.Random:
					return _clips[Random.Range(0, _clips.Length)];
				default:
					throw new NotImplementedException("Selection method not implemented.");
			}
		}

		public static async Task WaitUntilAudioStops(AudioSource audioSource, CancellationToken ct)
		{
			while (audioSource != null && (audioSource.isPlaying || !EditorApplication.isFocused))
			{
				await Task.Yield();
				ct.ThrowIfCancellationRequested();
			}
		}
	}
}
