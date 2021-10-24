using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.Game.Engines;
using Logger = NLog.Logger;
using NLog;
using System.ComponentModel;
using System;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.DropTargetBank;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Drop Target Bank")]
	public class DropTargetBankComponent : MainComponent<DropTargetBankData>, ICoilDeviceComponent
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		[ToolboxItem("The number of the drop targets. See documentation of a description of each type.")]
		public int BankSize = 1;

		[SerializeField]
		//[TypeRestriction(typeof(DropTargetComponent), PickerLabel = "Drop Targets", DeviceItem = nameof(TargetComponent.SwitchItem))]
		[Tooltip("Drop Targets")]
		public DropTargetComponent[] DropTargets = Array.Empty<DropTargetComponent>();

		public IEnumerable<GamelogicEngineCoil> AvailableCoils => new[] {
			new GamelogicEngineCoil(name) {
				Description = "Reset Coil"
			}
		};

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.DropTargetBank;
		public override string ItemName => "DropTargetBank";

		public override IEnumerable<Type> ValidParents => System.Type.EmptyTypes;

		public override DropTargetBankData InstantiateData() => new DropTargetBankData();

		#endregion

		IEnumerable<GamelogicEngineCoil> IDeviceComponent<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IWireableComponent.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceComponent<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterMech(this);
		}

		#endregion

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(DropTargetBankData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			BankSize = data.BankSize;

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(DropTargetBankData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
			/*PlayfieldEntrySwitch = FindComponent<ITriggerComponent>(components, data.PlayfieldEntrySwitch);
			PlayfieldExitKicker = FindComponent<KickerComponent>(components, data.PlayfieldExitKicker);
			if (PlayfieldExitKicker != null)
			{
				PlayfieldExitKickerItem = PlayfieldExitKicker.AvailableCoils.First().Id;
			}*/

			return Array.Empty<MonoBehaviour>();
		}

		public override DropTargetBankData CopyDataTo(DropTargetBankData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			data.Name = name;

			data.BankSize = BankSize;
			/*data.PlayfieldEntrySwitch = PlayfieldEntrySwitch == null ? string.Empty : PlayfieldEntrySwitch.name;
			data.PlayfieldExitKicker = PlayfieldExitKicker == null ? string.Empty : PlayfieldExitKicker.name;
			data.BallCount = BallCount;
			data.SwitchCount = SwitchCount;
			data.JamSwitch = JamSwitch;
			data.RollTime = RollTime;
			data.TransitionTime = TransitionTime;
			data.KickTime = KickTime;*/

			return data;
		}

		#endregion

	}
}
