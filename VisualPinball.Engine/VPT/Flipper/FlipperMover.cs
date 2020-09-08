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

// ReSharper disable CommentTypo
// ReSharper disable CompareOfFloatsByEqualityOperator

using VisualPinball.Engine.Math;
using VisualPinball.Engine.Physics;

namespace VisualPinball.Engine.VPT.Flipper
{
	public class FlipperMover
	{
		public readonly HitCircle HitCircleBase;                               // m_hitcircleBase
		public readonly float EndRadius;                                       // m_endradius
		public readonly float FlipperRadius;                                   // m_flipperradius

		/// <summary>
		/// Moment of inertia
		/// </summary>
		public float Inertia;                                                  // m_inertia

		/// <summary>
		/// base norms at zero degrees
		/// </summary>
		public readonly Vertex2D ZeroAngNorm = new Vertex2D();                 // m_zeroAngNorm

		/// <summary>
		/// -1,0,1
		/// </summary>
		public short EnableRotateEvent;                                        // m_enableRotateEvent

		public readonly float AngleStart;                                               // m_angleStart
		public readonly float AngleEnd;                                                 // m_angleEnd
		public readonly float AngleSpeed;                                               // m_angleSpeed

		/// <summary>
		/// is solenoid enabled?
		/// </summary>
		private bool _solState; // m_solState

		public FlipperMover(FlipperData data, Table.Table table, IItem item)
		{
			var tableData = table.Data;

			if (data.FlipperRadiusMin > 0 && data.FlipperRadiusMax > data.FlipperRadiusMin) {
				data.FlipperRadius = data.FlipperRadiusMax - (data.FlipperRadiusMax - data.FlipperRadiusMin) /* m_ptable->m_globalDifficulty*/;
				data.FlipperRadius = MathF.Max(data.FlipperRadius, data.BaseRadius - data.EndRadius + 0.05f);

			} else {
				data.FlipperRadius = data.FlipperRadiusMax;
			}

			EndRadius = MathF.Max(data.EndRadius, 0.01f); // radius of flipper end
			FlipperRadius = MathF.Max(data.FlipperRadius, 0.01f); // radius of flipper arc, center-to-center radius
			AngleStart = MathF.DegToRad(data.StartAngle);
			AngleEnd = MathF.DegToRad(data.EndAngle);

			if (AngleEnd == AngleStart) {
				// otherwise hangs forever in collisions/updates
				AngleEnd += 0.0001f;
			}

			var height = table.GetSurfaceHeight(data.Surface, data.Center.X, data.Center.Y);
			var baseRadius = MathF.Max(data.BaseRadius, 0.01f);
			HitCircleBase = new HitCircle(data.Center, baseRadius, height, height + data.Height, ItemType.Flipper, item);

			EnableRotateEvent = 0;
			AngleSpeed = 0;

			_solState = false;

			var ratio = (baseRadius - EndRadius) / FlipperRadius;

			// model inertia of flipper as that of rod of length flipr around its end
			var mass = data.GetFlipperMass(tableData);
			Inertia = (float) (1.0 / 3.0) * mass * (FlipperRadius * FlipperRadius);

			// F2 Norm, used in Green's transform, in FPM time search  // =  sinf(faceNormOffset)
			ZeroAngNorm.X = MathF.Sqrt(1.0f - ratio * ratio);
			// F1 norm, change sign of x component, i.E -zeroAngNorm.X // = -cosf(faceNormOffset)
			ZeroAngNorm.Y = -ratio;
		}
	}
}
