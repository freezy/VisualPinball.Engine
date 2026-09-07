// Visual Pinball Engine
// Copyright (C) 2026 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System;
using Unity.Mathematics;
using VisualPinball.Unity.Collections;

namespace VisualPinball.Unity
{
	public class SpringHingeApi : IApi
	{
		private readonly SpringHingeComponent _component;
		private readonly PhysicsEngine _physicsEngine;
		private readonly int _itemId;

		public event EventHandler Init;

		internal SpringHingeApi(SpringHingeComponent component, PhysicsEngine physicsEngine)
		{
			_component = component;
			_physicsEngine = physicsEngine;
			_itemId = component.ItemId;
		}

		internal float Angle => _component.PublishedAngle;

		public void Reset(float angle)
		{
			if (!_physicsEngine) {
				return;
			}
			_physicsEngine.MutateState((ref PhysicsState state) => {
				if (!state.SpringHingeStates.ContainsKey(_itemId)) {
					return;
				}
				ref var hinge = ref state.SpringHingeStates.GetValueByRef(_itemId);
				hinge.Movement.Angle = math.clamp(math.radians(angle),
					hinge.Static.MinimumAngle, hinge.Static.MaximumAngle);
				hinge.Movement.AngularVelocity = 0f;
				hinge.Movement.ActiveStop = 0;
			});
		}

		void IApi.OnInit(BallManager ballManager) => Init?.Invoke(this, EventArgs.Empty);

		void IApi.OnDestroy()
		{
		}
	}
}
