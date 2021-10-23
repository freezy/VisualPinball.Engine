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

		[ToolboxItem("The type of the drop target bank. See documentation of a description of each type.")]
		public int Type = 1;

		[SerializeField]
		//[TypeRestriction(typeof(DropTargetComponent), PickerLabel = "Drop Targets", DeviceItem = nameof(TargetComponent.SwitchItem))]
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

		private void Awake()
		{
			var player = GetComponentInParent<Player>();
			if (player == null)
			{
				Logger.Error($"Cannot find player {name}.");
				return;
			}

			player.RegisterMech(this);
		}
	}
}
