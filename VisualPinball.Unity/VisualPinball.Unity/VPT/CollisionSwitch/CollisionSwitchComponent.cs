using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Collision Switch")]
	public class CollisionSwitchComponent : MonoBehaviour, ISwitchDeviceComponent
	{
		public const string MainSwitchItem = "collision_switch";

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches => new[] {
			new GamelogicEngineSwitch(MainSwitchItem)  {
				IsPulseSwitch = true
			}
		};

		public SwitchDefault SwitchDefault => SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineSwitch> IDeviceComponent<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterCollisionSwitchComponent(this); 
		}

		#endregion
	}
}
