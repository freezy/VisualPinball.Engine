using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using NLog;
using System.ComponentModel;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Drop Target Bank")]
	public class DropTargetBankComponent : MonoBehaviour, ICoilDeviceComponent
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[ToolboxItem("The type of the drop target bank. See documentation of a description of each type.")]
		public int Type = 1;

		[SerializeField]
		[TypeRestriction(typeof(DropTargetComponent), PickerLabel = "Drop Target 1", DeviceItem = nameof(DropTarget1Item))]
		[Tooltip("Drop Target 1")]
		public MonoBehaviour _dropTarget1;
		public string DropTarget1Item = string.Empty;

		[SerializeField]
		[TypeRestriction(typeof(DropTargetComponent), PickerLabel = "Drop Target 1", DeviceItem = nameof(DropTarget2Item))]
		[Tooltip("Drop Target 2")]
		public MonoBehaviour _dropTarget2;
		public string DropTarget2Item = string.Empty;

		[SerializeField]
		[TypeRestriction(typeof(DropTargetComponent), PickerLabel = "Drop Target 3", DeviceItem = nameof(DropTarget3Item))]
		[Tooltip("Drop Target 3")]
		public MonoBehaviour _dropTarget3;
		public string DropTarget3Item = string.Empty;

		[SerializeField]
		[TypeRestriction(typeof(DropTargetComponent), PickerLabel = "Drop Target 4", DeviceItem = nameof(DropTarget4Item))]
		[Tooltip("Drop Target 4")]
		public MonoBehaviour _dropTarget4;
		public string DropTarget4Item = string.Empty;

		[SerializeField]
		[TypeRestriction(typeof(DropTargetComponent), PickerLabel = "Drop Target 5", DeviceItem = nameof(DropTarget5Item))]
		[Tooltip("Drop Target 5")]
		public MonoBehaviour _dropTarget5;
		public string DropTarget5Item = string.Empty;

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
