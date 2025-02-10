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

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VisualPinball.Unity
{
    [CreateAssetMenu(fileName = "Voice Asset", menuName = "Visual Pinball/Sound/Voice Asset", order = 102)]

    public class VoiceAsset : SoundAsset
    {
        public SoundPriority Priority => _priority;

        [SerializeField] private SoundPriority _priority;
	}
}