using System;
using Unity.Entities;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Unity.Physics.Flipper;

namespace VisualPinball.Unity.Api
{
	public class FlipperApi
	{
		public event EventHandler Collide;
		public event EventHandler Hit;
		public event EventHandler Init;
		public event EventHandler<RotationEventArgs> LimitBOS;
		public event EventHandler<RotationEventArgs> LimitEOS;
		public event EventHandler Timer;

		internal readonly Entity Entity;

		private readonly Flipper _flipper;
		private readonly EntityManager _manager = World.DefaultGameObjectInjectionWorld.EntityManager;

		public FlipperApi(Flipper flipper, Entity entity)
		{
			_flipper = flipper;
			Entity = entity;

			Init?.Invoke(this, EventArgs.Empty);
		}

		public void RotateToEnd()
		{
			_manager.SetComponentData(Entity, new SolenoidStateData { Value = true });
			var mData = _manager.GetComponentData<FlipperMovementData>(Entity);
			mData.EnableRotateEvent = 1;
			_manager.SetComponentData(Entity, mData);
		}

		public void RotateToStart()
		{
			_manager.SetComponentData(Entity, new SolenoidStateData { Value = false });
			var mData = _manager.GetComponentData<FlipperMovementData>(Entity);
			mData.EnableRotateEvent = -1;
			_manager.SetComponentData(Entity, mData);
		}

		internal void HandleEvent(FlipperRotatedEvent rotatedEvent)
		{
			if (rotatedEvent.Direction) {
				LimitBOS?.Invoke(this, new RotationEventArgs { AngleSpeed = rotatedEvent.AngleSpeed });
			} else {
				LimitEOS?.Invoke(this, new RotationEventArgs { AngleSpeed = rotatedEvent.AngleSpeed });
			}
		}
	}

	public class RotationEventArgs
	{
		public float AngleSpeed;
	}
}
