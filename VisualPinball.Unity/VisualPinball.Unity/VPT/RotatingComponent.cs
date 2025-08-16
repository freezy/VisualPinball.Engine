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

using UnityEngine;

namespace VisualPinball.Unity
{
	/// <summary>
	/// New and (simple?) class that does the wiring for rotating components.
	/// </summary>
	public abstract class RotatingComponent : MonoBehaviour
	{
		public IAnimationValueSource AnimationValueSource { get => _rotationSource as IAnimationValueSource; set => _rotationSource = value as MonoBehaviour; }

		[SerializeField]
		[TypeRestriction(typeof(IAnimationValueSource), PickerLabel = "Rotation Source")]
		[Tooltip("The component that emits rotation values to which this component rotates.")]
		public MonoBehaviour _rotationSource;

		public Vector3 RotationAngle = Vector3.forward;

		protected abstract void AnimationValueChanged(AnimationValue value);

		protected void Awake()
		{
			AnimationValueSource ??= GetComponentInParent<IAnimationValueSource>();

			if (AnimationValueSource == null) {
				Debug.LogError("RotatingComponent requires a RotationSource to function properly.");
			}
		}

		private void OnEnable()
		{
			if (AnimationValueSource != null) {
				AnimationValueSource.OnAnimationValueChanged += AnimationValueChanged;
			}
		}

		private void OnDisable()
		{
			if (AnimationValueSource != null) {
				AnimationValueSource.OnAnimationValueChanged -= AnimationValueChanged;
			}
		}
	}
}
