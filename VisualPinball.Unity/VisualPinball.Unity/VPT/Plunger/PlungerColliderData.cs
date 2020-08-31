﻿using Unity.Entities;

namespace VisualPinball.Unity
{
	internal struct PlungerColliderData : IComponentData
	{
		public LineCollider LineSegSide0;
		public LineCollider LineSegSide1;
		public LineCollider LineSegEnd;
		public LineZCollider JointEnd0;
		public LineZCollider JointEnd1;
	}
}
