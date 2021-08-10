// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
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
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Game;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	[ExecuteAlways]
	[AddComponentMenu("Visual Pinball/Game Item/Trigger")]
	public class TriggerAuthoring : ItemMainRenderableAuthoring<Trigger, TriggerData>,
		ISwitchAuthoring, IDragPointsAuthoring, IConvertGameObjectToEntity
	{
		#region Data

		public float Radius = 25f;

		public bool IsEnabled = true;

		public float HitHeight = 50f;

		public float AnimSpeed = 1f;

		public float WireThickness;

		[SerializeField]
		private DragPointData[] _dragPoints;
		public DragPointData[] DragPoints { get => _dragPoints; set => _dragPoints = value; }

		#endregion

		protected override Trigger InstantiateItem(TriggerData data) => new Trigger(data);
		protected override TriggerData InstantiateData() => new TriggerData();

		protected override Type MeshAuthoringType { get; } = typeof(ItemMeshAuthoring<Trigger, TriggerData, TriggerAuthoring>);
		protected override Type ColliderAuthoringType { get; } = typeof(ItemColliderAuthoring<Trigger, TriggerData, TriggerAuthoring>);

		public override IEnumerable<Type> ValidParents => TriggerColliderAuthoring.ValidParentTypes
			.Concat(TriggerMeshAuthoring.ValidParentTypes)
			.Distinct();

		public ISwitchable Switchable => Item;

		public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
		{
			Convert(entity, dstManager);

			var table = gameObject.GetComponentInParent<TableAuthoring>().Item;
			dstManager.AddComponentData(entity, new TriggerAnimationData());
			dstManager.AddComponentData(entity, new TriggerMovementData());
			dstManager.AddComponentData(entity, new TriggerStaticData {
				AnimSpeed = Data.AnimSpeed,
				Radius = Data.Radius,
				Shape = Data.Shape,
				TableScaleZ = table.GetScaleZ()
			});

			// register
			var trigger = GetComponent<TriggerAuthoring>().Item;
			transform.GetComponentInParent<Player>().RegisterTrigger(trigger, entity, ParentEntity, gameObject);
		}

		public override IEnumerable<MonoBehaviour> SetData(TriggerData data, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			DragPoints = data.DragPoints;
			Radius = data.Radius;
			IsEnabled = data.IsEnabled;
			HitHeight = data.HitHeight;
			AnimSpeed = data.AnimSpeed;
			WireThickness = data.WireThickness;

			return updatedComponents;
		}

		public override TriggerData CopyDataTo(TriggerData data, string[] materialNames, string[] textureNames)
		{
			var localPos = transform.localPosition;

			// name and position
			data.Name = name;
			data.Center = localPos.ToVertex2Dxy();

			// update visibility
			data.IsVisible = false;
			foreach (var meshComponent in MeshComponents) {
				switch (meshComponent) {
					case TriggerMeshAuthoring meshAuthoring:
						data.IsVisible = meshAuthoring.gameObject.activeInHierarchy;
						break;
				}
			}

			// triggers are always collidable
			// todo handle IsEnabled

			// other props
			data.DragPoints = DragPoints;
			data.Radius = Radius;
			data.IsEnabled = IsEnabled;
			data.HitHeight = HitHeight;
			data.AnimSpeed = AnimSpeed;
			data.WireThickness = WireThickness;

			return data;
		}

		public override ItemDataTransformType EditorPositionType => ItemDataTransformType.TwoD;

		public override void SetEditorPosition(Vector3 pos)
		{
			if (DragPoints.Length == 0) {
				return;
			}
			var diff = pos - transform.localPosition;
			foreach (var pt in DragPoints) {
				pt.Center += new Vertex3D(diff.x, diff.y, 0f);
			}
			transform.localPosition = pos;
		}

		public override ItemDataTransformType EditorRotationType => ItemDataTransformType.OneD;
		public override Vector3 GetEditorRotation() => transform.localEulerAngles;
		public override void SetEditorRotation(Vector3 rot) => transform.localEulerAngles = rot;

	}
}
