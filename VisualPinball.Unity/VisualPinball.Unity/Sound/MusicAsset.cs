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
using System.Threading.Tasks;
using UnityEngine;

namespace VisualPinball.Unity
{

    [CreateAssetMenu(fileName = "Music Asset", menuName = "Visual Pinball/Sound/Music Asset", order = 102)]
    public class MusicAsset : SoundAsset
    {
        private enum PlaybackState
        {
            Waiting,
            FadingIn,
            Playing,
            FadingOut
        }

        public override bool Loop => _loop;
        public SoundPriority Priority => _priority;

        [SerializeField] private SoundPriority _priority;
        [SerializeField] private bool _loop;
        [SerializeField] private MusicTransitionType _transitionType;
        [SerializeField][Range(0f, 10f)] private float _fadeDuration;
    }
}