using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using System.ComponentModel;
using System;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Surface Switch")]
	public class SurfaceSwitchComponent : MonoBehaviour, ISwitchDeviceComponent
	{
		public const string MainSwitchItem = "surface_switch";

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
			GetComponentInParent<Player>().RegisterSurfaceSwitchComponent(this); 
		}

		#endregion
	}
}
