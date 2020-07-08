using Unity.Entities;

namespace VisualPinball.Unity.Physics.Event
{
	public readonly struct EventData
	{
		public readonly VisualPinball.Engine.Game.Event Type;
		public readonly Entity ItemEntity;
		public readonly float FloatParam;

		public EventData(VisualPinball.Engine.Game.Event type, Entity itemEntity) : this()
		{
			Type = type;
			ItemEntity = itemEntity;
		}

		public EventData(VisualPinball.Engine.Game.Event type, Entity itemEntity, float floatParam) : this()
		{
			Type = type;
			ItemEntity = itemEntity;
			FloatParam = floatParam;
		}
	}
}
