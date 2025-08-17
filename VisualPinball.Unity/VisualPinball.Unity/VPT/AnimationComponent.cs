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

// ReSharper disable InconsistentNaming

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// New and (simple?) class that does the wiring for rotating components.
	/// </summary>
	public abstract class AnimationComponent : MonoBehaviour
	{
		private IAnimationValueEmitter AnimationValueEmitter {
			get => _animationEmitter as IAnimationValueEmitter;
			set => _animationEmitter = value as MonoBehaviour;
		}

		[SerializeField]
		[TypeRestriction(typeof(IAnimationValueEmitter), PickerLabel = "Emitter")]
		[Tooltip("The component that emits animation values to which this component rotates.")]
		public MonoBehaviour _animationEmitter;

		protected abstract void OnAnimationValueChanged(float value);

		protected void Awake()
		{
			AnimationValueEmitter ??= GetComponentInParent<IAnimationValueEmitter>();

			if (AnimationValueEmitter == null) {
				Debug.LogError("RotatingComponent requires a RotationSource to function properly.");
			}
		}

		private void OnEnable()
		{
			if (AnimationValueEmitter != null) {
				AnimationValueEmitter.OnAnimationValueChanged += OnAnimationValueChanged;
			}
		}

		private void OnDisable()
		{
			if (AnimationValueEmitter != null) {
				AnimationValueEmitter.OnAnimationValueChanged -= OnAnimationValueChanged;
			}
		}
	}
}
