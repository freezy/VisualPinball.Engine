using Unity.Entities;
using VisualPinball.Engine.Game;

namespace VisualPinball.Unity.Physics.Event
{
	public readonly struct EventData
	{
		public readonly EventType Type;
		public readonly Entity ItemEntity;
		public readonly float FloatParam;
		public readonly bool GroupEvent;

		public EventData(EventType type, Entity itemEntity, bool groupEvent = false) : this()
		{
			Type = type;
			ItemEntity = itemEntity;
			GroupEvent = groupEvent;
		}

		public EventData(EventType type, Entity itemEntity, float floatParam, bool groupEvent = false) : this()
		{
			Type = type;
			ItemEntity = itemEntity;
			FloatParam = floatParam;
			GroupEvent = groupEvent;
		}
	}
}
