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
		public IRotationSource RotationSource { get => _rotationSource as IRotationSource; set => _rotationSource = value as MonoBehaviour; }

		[SerializeField]
		[TypeRestriction(typeof(IRotationSource), PickerLabel = "Rotation Source")]
		[Tooltip("The component that emits rotation values to which this component rotates.")]
		public MonoBehaviour _rotationSource;

		public Vector3 RotationAngle = Vector3.forward;

		protected abstract void OnAngleChanged(float angleRad);

		protected void Awake()
		{
			RotationSource ??= GetComponentInParent<IRotationSource>();

			if (RotationSource == null) {
				Debug.LogError("RotatingComponent requires a RotationSource to function properly.");
			}
		}

		private void OnEnable()
		{
			if (RotationSource != null) {
				RotationSource.OnAngleChanged += OnAngleChanged;
			}
		}

		private void OnDisable()
		{
			if (RotationSource != null) {
				RotationSource.OnAngleChanged -= OnAngleChanged;
			}
		}
	}
}
