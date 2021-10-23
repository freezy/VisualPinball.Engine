using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using NLog;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Drop Target Bank")]
	public class DropTargetBankComponent : MonoBehaviour, ICoilDeviceComponent
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[Range(1, 5)]
		[Tooltip("How many drop targets for this bank.")]
		public int BankSize = 3;

		[Tooltip("The drop targets")]
		[TypeRestriction(typeof(DropTargetComponent), PickerLabel = "Drop Targets", DeviceItem = nameof(DropTargetItem))]
		public DropTargetComponent[] DropTargets = new DropTargetComponent[5];
		public string[] DropTargetItem = new string[] {
			string.Empty, string.Empty, string.Empty, string.Empty, string.Empty
		};


		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(name) {
				Description = "Reset Coil"
			}
		};

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null)
			{
				Logger.Error($"Cannot find player {name}.");
				return;
			}

			//player.RegisterMech(this);
		}
	}
}
