using System;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[Serializable]
	public class MechMark
	{
		public string Description;
		public string SwitchId;
		public int StepBeginning;
		public int StepEnd;
		public int Pulse;

		public GamelogicEngineSwitch Switch => new(SwitchId) { Description = Description };

		public MechMark(string description, string switchId, int stepBeginning, int stepEnd)
		{
			Description = description;
			SwitchId = switchId;
			StepBeginning = stepBeginning;
			StepEnd = stepEnd;
		}

		public bool HasId => !string.IsNullOrEmpty(SwitchId);
		public void GenerateId() => SwitchId = $"switch_{Guid.NewGuid().ToString()[..8]}";
	}
}
