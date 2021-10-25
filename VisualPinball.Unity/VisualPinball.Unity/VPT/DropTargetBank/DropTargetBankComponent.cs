using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using NLog;
using System.ComponentModel;
using System;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Drop Target Bank")]
	public class DropTargetBankComponent : MonoBehaviour, ICoilDeviceComponent
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[ToolboxItem("The number of the drop targets. See documentation of a description of each type.")]
		public int BankSize = 1;

		[SerializeField]
		[Tooltip("Drop Targets")]
		public DropTargetComponent[] DropTargets = Array.Empty<DropTargetComponent>();

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(name) {
				Description = "Reset Coil"
			}
		};

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		public static GameObject LoadPrefab() => Resources.Load<GameObject>("Prefabs/DropTargetBank");

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterDropTargetBankComponent(this);
		}

		#endregion
	}
}
