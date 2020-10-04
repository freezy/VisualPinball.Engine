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

#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
#endregion

using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Gate;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Gate")]
	public class GateAuthoring : ItemMainAuthoring<Gate, GateData>,
		IHittableAuthoring, ISwitchAuthoring
	{
		protected override Gate InstantiateItem(GateData data) => new Gate(data);

		public IHittable Hittable => Item;
		public ISwitchable Switchable => Item;

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Gate>(Name);
			}
		}

		public void RemoveHittableComponent()
		{
		}

		public void LinkChild(IItemAuthoring item)
		{
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => Data.Center.ToUnityVector3(Data.Height);
		public override void SetEditorPosition(Vector3 pos)
		{
			Data.Center = pos.ToVertex2Dxy();
			Data.Height = pos.z;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => new Vector3(Data.Rotation, 0f, 0f);
		public override void SetEditorRotation(Vector3 rot) => Data.Rotation = rot.x;

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorScale() => new Vector3(Data.Length, 0f, 0f);
		public override void SetEditorScale(Vector3 scale) => Data.Length = scale.x;
	}
}
