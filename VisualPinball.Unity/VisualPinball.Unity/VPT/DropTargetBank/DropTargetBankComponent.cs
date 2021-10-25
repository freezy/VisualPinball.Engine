using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using System.ComponentModel;
using System;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Drop Target Bank")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/drop-target-banks.html")]
	public class DropTargetBankComponent : MonoBehaviour, ICoilDeviceComponent
	{
		public const string ResetCoilItem = "reset_coil";

		[ToolboxItem("The number of the drop targets. See documentation of a description of each type.")]
		public int BankSize = 1;

		[SerializeField]
		[Tooltip("Drop Targets")]
		public DropTargetComponent[] DropTargets = Array.Empty<DropTargetComponent>();

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(ResetCoilItem) {
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
