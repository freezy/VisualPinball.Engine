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
using System.Threading;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace VisualPinball.Unity
{
    public class MusicPlayer : MonoBehaviour, IComparable<MusicPlayer>
    {
        public bool ShouldPlay { get; set; }
        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;

        public MusicAsset MusicAsset { get; private set; }
        public float FadeDuration { get; set; }
        public bool StartAtFullVolume { get; set; }
        public int ActiveRequestCount => _activeRequestCount;

        private AudioSource _audioSource;
        // The number of times this music asset was requested to be added to the player stack.
        // We don't literally add it multiple times to avoid cross-fading the same track into itself.
        private int _activeRequestCount;
        private DateTime _lastRequestTime;

        public void Init(MusicAsset musicAsset, float fadeDuration)
        {
            MusicAsset = musicAsset;
            FadeDuration = fadeDuration;
        }

        public void AddRequest()
        {
            _activeRequestCount++;
            _lastRequestTime = DateTime.Now;
        }

        public void RemoveRequest()
        {
            if (_activeRequestCount > 0)
                _activeRequestCount--;
        }

        private void Start()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.volume = 0f;
        }

        private void Update()
        {
            var canPlay = _audioSource.isActiveAndEnabled;
#if UNITY_EDITOR
            canPlay &= EditorApplication.isFocused;
#else
            canPlay &= Application.isFocused;
#endif
            if (ShouldPlay && canPlay && !_audioSource.isPlaying)
            {
                var oldVolume = _audioSource.volume;
                MusicAsset.ConfigureAudioSource(_audioSource);
                _audioSource.Play();
                _audioSource.volume = StartAtFullVolume ? MusicAsset.Volume : oldVolume;
            }
            else if (!ShouldPlay && _audioSource.isPlaying && _audioSource.volume == 0f)
            {
                _audioSource.Stop();
            }

            var targetVolume = ShouldPlay ? MusicAsset.Volume : 0f;
            if (_audioSource.volume != targetVolume)
            {
                if (FadeDuration == 0f)
                {
                    _audioSource.volume = targetVolume;
                }
                else
                {
                    if (_audioSource.volume < targetVolume)
                        _audioSource.volume += 1 / FadeDuration * Time.deltaTime;
                    else
                        _audioSource.volume -= 1 / FadeDuration * Time.deltaTime;
                    _audioSource.volume = Mathf.Clamp(_audioSource.volume, 0f, MusicAsset.Volume);
                }
            }
        }

        private async Task Play(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            MusicAsset.ConfigureAudioSource(_audioSource);
            _audioSource.Play();
            try
            {
                _audioSource.volume = StartAtFullVolume ? MusicAsset.Volume : 0f;

                while (true)
                {
                    while (_audioSource.isPlaying)
                    {
                        var targetVolume = ShouldPlay ? MusicAsset.Volume : 0f;
                        if (_audioSource.volume != targetVolume)
                        {
                            if (FadeDuration == 0f)
                            {
                                _audioSource.volume = targetVolume;
                            }
                            else
                            {
                                if (_audioSource.volume < targetVolume)
                                    _audioSource.volume += 1 / FadeDuration * Time.deltaTime;
                                else
                                    _audioSource.volume -= 1 / FadeDuration * Time.deltaTime;
                                _audioSource.volume = Mathf.Clamp(_audioSource.volume, 0f, MusicAsset.Volume);
                            }
                        }
                        await Task.Yield();
                        ct.ThrowIfCancellationRequested();
                    }
                    MusicAsset.ConfigureAudioSource(_audioSource);
                    _audioSource.Play();
                }
            }
            finally
            {
                _audioSource.Stop();
            }
        }

        private void OnDestroy()
        {
            if (_audioSource != null)
                Destroy(_audioSource);
        }

        // Used to sort the music player stack and determine which player should play
        public int CompareTo(MusicPlayer other)
        {
            if (_activeRequestCount > 0 && other._activeRequestCount == 0) return -1;
            if (_activeRequestCount == 0 && other._activeRequestCount > 0) return 1;
            if (MusicAsset.Priority != other.MusicAsset.Priority)
                return other.MusicAsset.Priority.CompareTo(MusicAsset.Priority);
            if (_lastRequestTime != null)
                return _lastRequestTime.CompareTo(other._lastRequestTime);
            return 0;
        }
    }
}