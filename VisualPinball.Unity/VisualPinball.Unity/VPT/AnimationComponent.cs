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

using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace VisualPinball.Unity
{
	/// <summary>
	/// New and (simple?) class that does the wiring for rotating components.
	/// </summary>
	public abstract class AnimationComponent<T> : MonoBehaviour
	{
		private IAnimationValueEmitter<T> Emitter {
			get => _emitter as IAnimationValueEmitter<T>;
			set => _emitter = value as MonoBehaviour;
		}

		[FormerlySerializedAs("_animationEmitter")]
		[SerializeField]
		[TypeRestriction(typeof(IAnimationValueEmitter), PickerLabel = "Emitter")]
		[Tooltip("The component that emits animation values to which this component rotates.")]
		public MonoBehaviour _emitter;

		protected abstract void OnAnimationValueChanged(T value);

		protected void Awake()
		{
			if (_emitter != null && _emitter is IAnimationValueEmitter<T> e1) {
				Emitter = e1;
			} else {
				// Fallback: search parents for any MonoBehaviour that implements the interface
				var candidates = GetComponentsInParent<IAnimationValueEmitter>();
				Emitter = candidates.OfType<IAnimationValueEmitter<T>>().FirstOrDefault();
			}

			if (Emitter == null) {
				Debug.LogError($"{GetType().Name} requires an IAnimationValueEmitter<{typeof(T).Name}>.", this);
			}
		}

		private void OnEnable()
		{
			if (Emitter != null) {
				Emitter.OnAnimationValueChanged += OnAnimationValueChanged;
			}
		}

		private void OnDisable()
		{
			if (Emitter != null) {
				Emitter.OnAnimationValueChanged -= OnAnimationValueChanged;
			}
		}

#if UNITY_EDITOR
		// Editor-time safety: warn if someone drags the wrong thing into the field
		protected virtual void OnValidate()
		{
			if (_emitter != null && !(_emitter is IAnimationValueEmitter<T>)) {
				Debug.LogError($"{name}: Assigned emitter does not implement IAnimationValueEmitter<{typeof(T).Name}>. It will be ignored.",this);
			}
		}
#endif
	}
}
