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

using VisualPinball.Engine.Math;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;

namespace VisualPinball.Engine.Physics
{
	public abstract class HitObject
	{
		public int Id;

		/// <summary>
		/// Base object pointer.
		///
		/// Mainly used as IFireEvents, but also as HitTarget or Primitive or
		/// Trigger or Kicker or Gate.
		/// </summary>
		public IItem Item;                                                 // m_obj

		/// <summary>
		/// Threshold for firing an event (usually (always??) normal dot
		/// ball-velocity)
		/// </summary>
		public float Threshold = 0f;                                           // m_threshold
		public Rect3D HitBBox = new Rect3D(true);                                  // m_hitBBox

		public float Elasticity = 0.3f;                                        // m_elasticity
		public float ElasticityFalloff;                                        // m_elasticityFalloff
		public float Friction = 0.3f;                                          // m_friction

		/// <summary>
		/// Scatter in Radians
		/// </summary>
		public float Scatter;                                                  // m_scatter

		public readonly ItemType ObjType;
		public bool IsEnabled = true;                                          // m_enabled

		/// <summary>
		/// FireEvents for m_obj?
		/// </summary>
		public bool FireEvents = false;                                        // m_fe

		/// <summary>
		/// currently only used to determine which HitTriangles/HitLines/HitPoints
		/// are being part of the same Primitive element m_obj, to be able to early
		/// out intersection traversal if primitive is flagged as not collidable
		/// </summary>
		public bool E = false;                                                 // m_e

		public int ItemIndex;
		public int ItemVersion;

		protected HitObject(ItemType objType, IItem item)
		{
			ObjType = objType;
			Item = item;
		}

		public abstract void CalcHitBBox();

		protected const float HardScatter = 0.0f;

		public HitObject SetFriction(float friction)
		{
			Friction = friction;
			return this;
		}

		public HitObject SetScatter(float scatter)
		{
			Scatter = scatter;
			return this;
		}

		public HitObject SetElasticity(float elasticity)
		{
			Elasticity = elasticity;
			return this;
		}

		public HitObject SetElasticity(float elasticity, float elasticityFalloff)
		{
			Elasticity = elasticity;
			ElasticityFalloff = elasticityFalloff;
			return this;
		}

		public HitObject SetZ(float zLow, float zHigh)
		{
			HitBBox.ZLow = zLow;
			HitBBox.ZHigh = zHigh;
			return this;
		}

		public void SetEnabled(bool isEnabled)
		{
			IsEnabled = isEnabled;
		}

		public void ApplyPhysics(IPhysicalData data, Table table)
		{
			var mat = table.GetMaterial(data.GetPhysicsMaterial());
			if (mat != null && !data.GetOverwritePhysics()) {
				SetElasticity(mat.Elasticity, mat.ElasticityFalloff);
				SetFriction(mat.Friction);
				SetScatter(MathF.DegToRad(mat.ScatterAngle));

			} else {
				SetElasticity(data.GetElasticity(), data.GetElasticityFalloff());
				SetFriction(data.GetFriction());
				SetScatter(MathF.DegToRad(data.GetScatter()));
			}

			SetEnabled(data.GetIsCollidable());
		}

		public virtual void SetIndex(int index, int version)
		{
			ItemIndex = index;
			ItemVersion = version;
		}
	}
}
