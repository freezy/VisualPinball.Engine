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

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VisualPinball.Unity
{
    public class MusicCoordinator : MonoBehaviour
    {
        [SerializeField][Range(0f, 10f)] private float _fadeDuration = 3f;

        private readonly List<MusicPlayer> _playerStack = new();

        public void AddRequest(MusicAsset musicAsset)
        {
            var player = _playerStack.FirstOrDefault(x => x.MusicAsset == musicAsset);
            if (player == null)
            {
                player = gameObject.AddComponent<MusicPlayer>();
                player.Init(musicAsset, _fadeDuration);
                _playerStack.Add(player);
            }
            player.AddRequest();

            OnStackChanged();
        }

        public void RemoveRequest(MusicAsset musicAsset)
        {
            var player = _playerStack.FirstOrDefault(x => x.MusicAsset == musicAsset);
            if (player != default)
            {
                player.RemoveRequest();
                OnStackChanged();
            }
        }

        private void OnStackChanged()
        {
            _playerStack.RemoveAll(x => x == null);

            if (_playerStack.Any(x => x.ActiveRequestCount > 0))
            {
                _playerStack.Sort();
                _playerStack[0].ShouldPlay = true;
                // No need to fade in if nothing else is playing
                _playerStack[0].StartAtFullVolume = !_playerStack.Any(x => x.IsPlaying);
                for (int i = 1; i < _playerStack.Count; i++)
                    _playerStack[i].ShouldPlay = false;
            }
            else
                _playerStack.ForEach(x => x.ShouldPlay = false);
        }
    }
}