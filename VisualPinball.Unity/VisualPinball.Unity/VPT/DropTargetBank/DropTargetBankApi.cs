using System;
using Logger = NLog.Logger;
using NLog;
using UnityEngine;
using System.Collections.Generic;

namespace VisualPinball.Unity
{
	public class DropTargetBankApi : IApi, IApiCoilDevice
	{
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		private readonly DropTargetBankComponent _dropTargetBankComponent;
		private Player _player;

		public DeviceCoil ResetCoil;

		public List<DropTargetApi> _dropTargetApis = new List<DropTargetApi>();

		public event EventHandler Init;

		internal DropTargetBankApi(GameObject go, Player player)
		{
			_dropTargetBankComponent = go.GetComponentInChildren<DropTargetBankComponent>();
			_player = player;
		}

		IApiCoil IApiCoilDevice.Coil(string deviceItem)
		{
			if (deviceItem == _dropTargetBankComponent.name)
			{
				return ResetCoil;
			}

			throw new ArgumentException($"Unknown device item {deviceItem}.");
		}

		void IApi.OnInit(BallManager ballManager)
		{
			ResetCoil = new DeviceCoil(OnResetCoilEnabled);
			
			for (var index = 0; index < _dropTargetBankComponent.BankSize; index++)
			{
				_dropTargetApis.Add(_player.TableApi.DropTarget(_dropTargetBankComponent.DropTargets[index]));
			}

			Init?.Invoke(this, EventArgs.Empty);
		}

		private void OnResetCoilEnabled()
		{
			Logger.Info($"OnResetCoilEnabled - resetting {_dropTargetBankComponent.name}");

			foreach (var dropTargetApi in _dropTargetApis)
			{
				dropTargetApi.IsDropped = false;
			}
		}

		void IApi.OnDestroy()
		{
			Logger.Info($"Destroying {_dropTargetBankComponent.name}");
		}
	}
}

