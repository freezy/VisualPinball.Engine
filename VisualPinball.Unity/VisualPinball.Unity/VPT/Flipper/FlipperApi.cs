// ReSharper disable EventNeverSubscribedTo.Global
#pragma warning disable 67

using System;
using Unity.Entities;

namespace VisualPinball.Unity.VPT.Flipper
{
	public class FlipperApi : IApiInitializable
	{
		public event EventHandler Collide;
		public event EventHandler Hit;
		public event EventHandler Init;
		public event EventHandler<RotationEventArgs> LimitBos;
		public event EventHandler<RotationEventArgs> LimitEos;
		public event EventHandler Timer;

		internal readonly Entity Entity;

		private readonly Engine.VPT.Flipper.Flipper _flipper;
		private readonly EntityManager _manager = World.DefaultGameObjectInjectionWorld.EntityManager;

		public FlipperApi(Engine.VPT.Flipper.Flipper flipper, Entity entity)
		{
			_flipper = flipper;
			Entity = entity;
		}

		public void RotateToEnd()
		{
			var mData = _manager.GetComponentData<FlipperMovementData>(Entity);
			mData.EnableRotateEvent = 1;
			_manager.SetComponentData(Entity, mData);
			_manager.SetComponentData(Entity, new SolenoidStateData { Value = true });
		}

		public void RotateToStart()
		{
			var mData = _manager.GetComponentData<FlipperMovementData>(Entity);
			mData.EnableRotateEvent = -1;
			_manager.SetComponentData(Entity, mData);
			_manager.SetComponentData(Entity, new SolenoidStateData { Value = false });
		}

		internal void HandleEvent(FlipperRotatedEvent rotatedEvent)
		{
			if (rotatedEvent.Direction) {
				LimitBos?.Invoke(this, new RotationEventArgs { AngleSpeed = rotatedEvent.AngleSpeed });
			} else {
				LimitEos?.Invoke(this, new RotationEventArgs { AngleSpeed = rotatedEvent.AngleSpeed });
			}
		}

		void IApiInitializable.Init()
		{
			Init?.Invoke(this, EventArgs.Empty);
		}
	}

	public class RotationEventArgs
	{
		public float AngleSpeed;
	}
}
