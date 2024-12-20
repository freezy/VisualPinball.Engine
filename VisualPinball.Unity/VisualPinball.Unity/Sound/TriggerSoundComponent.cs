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
using UnityEngine;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Sound/Trigger Sound")]
	public class TriggerSoundComponent : EventSoundComponent<ISoundEmitter, SoundEventArgs>
	{
		[SerializeField, HideInInspector]
		private string _triggerId;
		[SerializeField, HideInInspector]
		private bool _hasStopTrigger;
		[SerializeField, HideInInspector]
		private string _stopTriggerId;

		public override Type GetRequiredType() => typeof(ISoundEmitter);
		public override bool SupportsLoopingSoundAssets() => _hasStopTrigger;

		protected override bool TryFindEventSource(out ISoundEmitter soundEmitter)
			=> TryGetComponent(out soundEmitter);

		protected override async void OnEvent(object sender, SoundEventArgs e)
		{
			if (_hasStopTrigger && e.TriggerId == _stopTriggerId)
				Stop(allowFade: true);

			if (e.TriggerId == _triggerId) {
				await Play(e.Volume);
			}
		}

		protected override void Subscribe(ISoundEmitter eventSource)
			=> eventSource.OnSound += OnEvent;

		protected override void Unsubscribe(ISoundEmitter eventSource)
			=> eventSource.OnSound -= OnEvent;
	}
}
