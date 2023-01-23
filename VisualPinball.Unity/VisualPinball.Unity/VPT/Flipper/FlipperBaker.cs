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
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Trigger;

namespace VisualPinball.Unity
{
	public class FlipperBaker : ItemBaker<FlipperComponent, FlipperData>
	{
		public override void Bake(FlipperComponent authoring)
		{
			base.Bake(authoring);

			var player = GetComponentInParent<Player>();
			var colliderComponent = GetComponent<FlipperColliderComponent>();

			// collision
			if (colliderComponent) {

				// vpx physics
				var d = authoring.GetMaterialData(colliderComponent);
				AddComponent(d);
				AddComponent(authoring.GetMovementData(d));
				AddComponent(FlipperComponent.GetVelocityData(d));
				AddComponent(authoring.GetHitData());
				AddComponent(authoring.GetFlipperTricksData(colliderComponent, d));
				AddComponent(new SolenoidStateData { Value = false });
				AddComponentObject(new GameObjectContainer { GameObject = authoring.gameObject });

				// flipper correction (nFozzy)
				if (colliderComponent.FlipperCorrection) {
					SetupFlipperCorrection(authoring, player, colliderComponent);
				}
			}

			// register
			player.RegisterFlipper(authoring, GetEntity());
		}
		
		private void SetupFlipperCorrection(FlipperComponent authoring, Player player, FlipperColliderComponent colliderComponent)
		{
			var fc = colliderComponent.FlipperCorrection;

			// create trigger
			var triggerData = CreateCorrectionTriggerData(authoring);
			var triggerEntity = CreateAdditionalEntity();
			AddComponent(triggerEntity, new TriggerStaticData());
			// todo add proper colliders
			//player.RegisterTrigger(triggerData, triggerEntity, authoring.gameObject);

			using (var builder = new BlobBuilder(Allocator.Temp)) {

				ref var root = ref builder.ConstructRoot<FlipperCorrectionBlob>();
				root.FlipperEntity = GetEntity();
				root.TimeDelayMs = fc.TimeThresholdMs;

				// Discretize the curves
				var polarities = builder.Allocate(ref root.Polarities, fc.PolaritiesCurveSlicingCount + 1);
				if (fc.Polarities != null)
				{
					var curve = fc.Polarities;
					float stepP = (curve[curve.length - 1].time - curve[0].time) / fc.PolaritiesCurveSlicingCount;
					int i = 0;
					for (var t = curve[0].time; t <= curve[curve.length - 1].time; t += stepP)
					{
						polarities[i].x = t;
						polarities[i++].y = curve.Evaluate(t);
					}
				}
				else
				{
					for (int i = 0; i < fc.PolaritiesCurveSlicingCount + 1; i++)
					{
						polarities[i].x = i / (float)fc.PolaritiesCurveSlicingCount;
						polarities[i].y = 0F;
					}
				}

				var velocities = builder.Allocate(ref root.Velocities, fc.VelocitiesCurveSlicingCount + 1);
				if (fc.Velocities != null)
				{
					var curve = fc.Velocities;
					float stepP = (curve[curve.length - 1].time - curve[0].time) / fc.VelocitiesCurveSlicingCount;
					int i = 0;
					for (var t = curve[0].time; t <= curve[curve.length - 1].time; t += stepP)
					{
						velocities[i].x = t;
						velocities[i++].y = curve.Evaluate(t);
					}
				}
				else
				{
					for (int i = 0; i < fc.VelocitiesCurveSlicingCount + 1; i++)
					{
						velocities[i].x = i / (float)fc.PolaritiesCurveSlicingCount;
						velocities[i].y = 1F;
					}
				}

				var blobAssetRef = builder.CreateBlobAssetReference<FlipperCorrectionBlob>(Allocator.Persistent);

				// add correction data
				AddComponent(triggerEntity, new FlipperCorrectionData {
					Value = blobAssetRef
				});
			}
		}
		
		public TriggerData CreateCorrectionTriggerData(FlipperComponent authoring)
		{
			// Get table reference
			var ta = GetComponentInParent<TableComponent>();
			if (ta != null) {

				var localPos = authoring.transform.localPosition;
				var data = new TriggerData(authoring.name + " (Correction Trigger)", localPos.x, localPos.y);
				var poly = authoring.GetEnclosingPolygon(23, 12);
				data.DragPoints = new DragPointData[poly.Count];
				data.IsLocked = true;
				data.HitHeight = 150F; // nFozzy's recommendation, but I think 50 should be ok

				for (var i = 0; i < poly.Count; i++) {
					// Poly points are expressed in flipper's frame: transpose to Table's frame as this is the basis uses for drag points
					var p = ta.transform.InverseTransformPoint(authoring.transform.TransformPoint(poly[i]));
					data.DragPoints[poly.Count - i - 1] = new DragPointData(p.x, p.y);
				}
				return data;
			}
			throw new InvalidOperationException("Cannot create correction trigger for flipper outside of the table hierarchy.");
		}
		
		internal class GameObjectContainer : IComponentData
		{
			public GameObject GameObject;
		}
	}
}
