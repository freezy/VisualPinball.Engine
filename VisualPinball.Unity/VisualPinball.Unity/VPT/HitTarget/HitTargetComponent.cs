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

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Game Item/Hit Target")]
	public class HitTargetComponent : TargetComponent
	{
		protected override float ZOffset => 0;

		public override bool HasProceduralMesh => false;

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(HitTargetData data)
		{
			var updatedComponents = base.SetData(data).ToList();

			// hit target collider data
			var colliderComponent = GetComponent<HitTargetColliderComponent>();
			if (colliderComponent) {
				colliderComponent.enabled = data.IsCollidable;
				colliderComponent.Threshold = data.Threshold;

				colliderComponent.OverwritePhysics = data.OverwritePhysics;
				colliderComponent.Elasticity = data.Elasticity;
				colliderComponent.ElasticityFalloff = data.ElasticityFalloff;
				colliderComponent.Friction = data.Friction;
				colliderComponent.Scatter = data.Scatter;

				updatedComponents.Add(colliderComponent);
			}

			// animation data
			var animationComponent = GetComponent<HitTargetAnimationComponent>();
			if (animationComponent) {
				animationComponent.enabled = !data.IsDropTarget;
				animationComponent.Speed = data.DropSpeed;

				updatedComponents.Add(animationComponent);
			}

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(HitTargetData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			var colliderComponent = GetComponent<HitTargetColliderComponent>();
			if (colliderComponent) {
				colliderComponent.PhysicsMaterial = materialProvider.GetPhysicsMaterial(data.PhysicsMaterial);
			}

			// visibility
			SetEnabled<Renderer>(data.IsVisible);

			return Array.Empty<MonoBehaviour>();
		}

		public override HitTargetData CopyDataTo(HitTargetData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			base.CopyDataTo(data, materialNames, textureNames, forExport);

			// collision data
			var colliderComponent = GetComponent<HitTargetColliderComponent>();
			if (colliderComponent) {
				data.IsCollidable = colliderComponent.enabled;
				data.Threshold = colliderComponent.Threshold;
				data.PhysicsMaterial = colliderComponent.PhysicsMaterial == null ? string.Empty : colliderComponent.PhysicsMaterial.name;

				data.OverwritePhysics = colliderComponent.OverwritePhysics;
				data.Elasticity = colliderComponent.Elasticity;
				data.ElasticityFalloff = colliderComponent.ElasticityFalloff;
				data.Friction = colliderComponent.Friction;
				data.Scatter = colliderComponent.Scatter;

			} else {
				data.IsCollidable = false;
			}

			// animation data
			var animationComponent = GetComponent<HitTargetAnimationComponent>();
			if (animationComponent) {
				data.DropSpeed = animationComponent.Speed;
			}

			return data;
		}

		#endregion
	}
}
