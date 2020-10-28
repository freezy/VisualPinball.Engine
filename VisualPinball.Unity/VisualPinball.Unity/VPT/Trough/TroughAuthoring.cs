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

using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Trough")]
	public class TroughAuthoring : ItemAuthoring<Trough, TroughData>, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		protected override Trough GetItem() => new Trough(data);

		private void OnDestroy()
		{
			if (!Application.isPlaying) {
				Table?.Remove<Trough>(Name);
			}
		}

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			dstManager.AddComponentData(entity, new TroughStaticData
			{
				BallCount = data.BallCount,
				SwitchCount = data.SwitchCount,
				SettleTime = data.SettleTime
			});
		}

		//public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;
		//public override Vector3 GetEditorPosition() => data.Entrance.ToUnityVector3(0f);
		//public override void SetEditorPosition(Vector3 pos) => data.Entrance = pos.ToVertex2Dxy();

		//public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		//public override Vector3 GetEditorRotation() => new Vector3(data.Orientation, 0, 0);
		//public override void SetEditorRotation(Vector3 rot) => data.Orientation = rot.x;

		//public override ItemDataTransformType EditorScaleType => ItemDataTransformType.OneD;
		//public override Vector3 GetEditorScale() => new Vector3(data.ExitOffset, 0f, 0f);
		//public override void SetEditorScale(Vector3 scale) => data.ExitOffset = scale.x;
	}
}
