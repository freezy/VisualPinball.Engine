// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

// ReSharper disable AssignmentInConditionalExpression

using UnityEditor;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Rubber;

namespace VisualPinball.Unity.Editor
{
	[CustomEditor(typeof(BumperColliderAuthoring))]
	public class BumperColliderInspector : ItemColliderInspector<Bumper, BumperData, BumperAuthoring, BumperColliderAuthoring>
	{
		private BumperData _bumperData;

		private bool _foldoutMaterial = true;

		protected override void OnEnable()
		{
			base.OnEnable();
			_bumperData = Data;
		}

		public override void OnInspectorGUI()
		{
			if (_bumperData == null) {
				NoDataPanel();
				return;
			}

			ItemDataField("Collidable", ref _bumperData.IsCollidable, dirtyMesh: false);

			EditorGUI.BeginDisabledGroup(!_bumperData.IsCollidable);
			ItemDataField("Has Hit Event", ref _bumperData.HitEvent, dirtyMesh: false);
			ItemDataField("Force", ref _bumperData.Force, dirtyMesh: false);
			ItemDataField("Hit Threshold", ref _bumperData.Threshold, dirtyMesh: false);
			ItemDataField("Scatter Angle", ref _bumperData.Scatter, dirtyMesh: false);
			EditorGUI.EndDisabledGroup();

			base.OnInspectorGUI();
		}
	}
}
