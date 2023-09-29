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

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Unity.Playfield;

namespace VisualPinball.Unity
{
	public class PlayfieldApi : CollidableApi<PlayfieldComponent, PlayfieldColliderComponent, TableData>
	{
		internal PlayfieldApi(GameObject go, Player player) : base(go, player)
		{
		}

		#region Collider Generation

		protected override void CreateColliders(List<ICollider> colliders, float margin)
		{
			var info = ((IApiColliderGenerator)this).GetColliderInfo();
			var planeColliderInfo = new ColliderInfo {
				Id = -1, // is set during allocation
				ItemId = ItemId,
				ItemType = ItemType.Table,
				FireEvents = false,
				IsEnabled = true,
				Material = new PhysicsMaterialData {
					Elasticity = ColliderComponent.Elasticity,
					ElasticityFalloff = ColliderComponent.ElasticityFalloff,
					Friction = ColliderComponent.Friction,
					ScatterAngleRad = ColliderComponent.Scatter
				},
				HitThreshold = 0
			};

			// do we have a playfield mesh?
			var meshComp = GameObject.GetComponent<PlayfieldMeshComponent>();
			if (meshComp && !meshComp.AutoGenerate) {
				var mf = GameObject.GetComponent<MeshFilter>();
				if (mf && mf.sharedMesh) {
					ColliderUtils.GenerateCollidersFromMesh(mf.sharedMesh.ToVpMesh().TransformToVpx(), info, colliders);
					
				} else {
					Debug.LogWarning($"Could not find mesh filter on playfield {GameObject.name}");
					colliders.Add(new PlaneCollider(new float3(0, 0, 1), MainComponent.TableHeight, planeColliderInfo));
				}
			} else {
				colliders.Add(new PlaneCollider(new float3(0, 0, 1), MainComponent.TableHeight, planeColliderInfo));
			}
			// add playfield glass collider
			colliders.Add(new PlaneCollider(new float3(0, 0, -1), MainComponent.GlassHeight, planeColliderInfo));

			if (ColliderComponent.CollideWithBounds) {

				// simple outer borders:
				colliders.Add(new LineCollider(
					new float2(MainComponent.Right, MainComponent.Top),
					new float2(MainComponent.Right, MainComponent.Bottom),
					MainComponent.TableHeight,
					MainComponent.GlassHeight,
					info
				));

				colliders.Add(new LineCollider(
					new float2(MainComponent.Left, MainComponent.Bottom),
					new float2(MainComponent.Left, MainComponent.Top),
					MainComponent.TableHeight,
					MainComponent.GlassHeight,
					info
				));

				colliders.Add(new LineCollider(
					new float2(MainComponent.Right, MainComponent.Bottom),
					new float2(MainComponent.Left, MainComponent.Bottom),
					MainComponent.TableHeight,
					MainComponent.GlassHeight,
					info
				));

				colliders.Add(new LineCollider(
					new float2(MainComponent.Left, MainComponent.Top),
					new float2(MainComponent.Right, MainComponent.Top),
					MainComponent.TableHeight,
					MainComponent.GlassHeight,
					info
				));
			}

			// glass (it's handled by the plane)
			// var rgv3D = new[] {
			// 	new float3(MainComponent.Left, MainComponent.Top, MainComponent.GlassHeight),
			// 	new float3(MainComponent.Right, MainComponent.Top, MainComponent.GlassHeight),
			// 	new float3(MainComponent.Right, MainComponent.Bottom, MainComponent.GlassHeight),
			// 	new float3(MainComponent.Left, MainComponent.Bottom, MainComponent.GlassHeight)
			// };
			// ColliderUtils.Generate3DPolyColliders(rgv3D, info, colliders);
		}

		#endregion
	}
}
