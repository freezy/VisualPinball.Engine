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

using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.VPT.Primitive;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Primitive")]
	public class PrimitiveAuthoring : ItemAuthoring<Primitive, PrimitiveData>, IHittableAuthoring, ISwitchableAuthoring, IConvertGameObjectToEntity
	{
		protected override string[] Children => null;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			// register
			var primitive = GetComponent<PrimitiveAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterPrimitive(primitive, entity, gameObject);
		}

		protected override Primitive GetItem() => new Primitive(data);

		public IHittable Hittable => Item;

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorPosition() => data.Position.ToUnityVector3();
		public override void SetEditorPosition(Vector3 pos) => data.Position = pos.ToVertex3D();

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorRotation() => new Vector3(data.RotAndTra[0], data.RotAndTra[1], data.RotAndTra[2]);
		public override void SetEditorRotation(Vector3 rot)
		{
			data.RotAndTra[0] = rot.x;
			data.RotAndTra[1] = rot.y;
			data.RotAndTra[2] = rot.z;
		}

		public override ItemDataTransformType EditorScaleType => ItemDataTransformType.ThreeD;
		public override Vector3 GetEditorScale() => data.Size.ToUnityVector3();
		public override void SetEditorScale(Vector3 scale) => data.Size = scale.ToVertex3D();
	}
}
