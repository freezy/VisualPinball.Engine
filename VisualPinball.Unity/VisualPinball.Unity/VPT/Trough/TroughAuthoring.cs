// Visual Pinball Engine
// Copyright (C) 2021 freezy and VPE Team
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

#region ReSharper
// ReSharper disable CompareOfFloatsByEqualityOperator
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable InconsistentNaming
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Profiling;
using VisualPinball.Engine.Game.Engines;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Trough;

namespace VisualPinball.Unity
{
	[AddComponentMenu("Visual Pinball/Trough")]
	[HelpURL("https://docs.visualpinball.org/creators-guide/manual/mechanisms/troughs.html")]
	public class TroughAuthoring : ItemMainAuthoring<TroughData>,
		ISwitchDeviceAuthoring, ICoilDeviceAuthoring
	{
		#region Data

		[ToolboxItem("The type of the opto. See documentation of a description of each type.")]
		public int Type = TroughType.ModernOpto;

		public ITriggerAuthoring PlayfieldEntrySwitch
		{
			get => _playfieldEntrySwitch as ITriggerAuthoring;
			set => _playfieldEntrySwitch = value as MonoBehaviour;
		}

		public string PlayfieldEntrySwitchId;

		[SerializeField]
		[TypeRestriction(typeof(ITriggerAuthoring), PickerLabel = "Triggers & Kickers")]
		[Tooltip("The trigger or kicker that eats the ball and puts it into the trough.")]
		public MonoBehaviour _playfieldEntrySwitch;

		[Tooltip("The kicker that creates and ejects the ball to the playfield.")]
		[TypeRestriction(typeof(KickerAuthoring), PickerLabel = "Kickers")]
		public KickerAuthoring PlayfieldExitKicker;

		[Range(1, 10)]
		[Tooltip("How many balls the trough holds when the game starts.")]
		public int BallCount = 6;

		[Range(1, 10)]
		[Tooltip("How many ball switches are available.")]
		public int SwitchCount = 6;

		[Tooltip("Defines if the trough has a jam switch.")]
		public bool JamSwitch;

		[Min(0)]
		[Tooltip("Sets how long it takes the ball to roll from one switch to the next.")]
		public int RollTime = 300;

		[Min(0)]
		[Tooltip("Defines how long the opto switch closes between balls.")]
		public int TransitionTime = 50;

		[Min(0)]
		[Tooltip("Defines how long it takes the ball to get kicked from the drain into the trough.")]
		public int KickTime = 100;

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Trough;
		public override string ItemName => "Trough";

		public override IEnumerable<Type> ValidParents => System.Type.EmptyTypes;

		public override TroughData InstantiateData() => new TroughData();

		#endregion

		#region Wiring

		public SwitchDefault SwitchDefault => Type == TroughType.ModernOpto ? SwitchDefault.NormallyClosed : SwitchDefault.NormallyOpen;

		IEnumerable<GamelogicEngineCoil> IDeviceAuthoring<GamelogicEngineCoil>.AvailableDeviceItems => AvailableCoils;
		IEnumerable<GamelogicEngineSwitch> IDeviceAuthoring<GamelogicEngineSwitch>.AvailableDeviceItems => AvailableSwitches;

		#endregion

		/// <summary>
		/// Time in milliseconds it takes the switch to enable when the ball enters.
		/// </summary>
		/// <exception cref="ArgumentException"></exception>
		public int RollTimeEnabled {
			get {
				switch (Type) {
					case TroughType.ModernOpto:
						return TransitionTime;

					case TroughType.ModernMech:
					case TroughType.TwoCoilsNSwitches:
					case TroughType.TwoCoilsOneSwitch:
					case TroughType.ClassicSingleBall:
						return RollTime / 2;

					default:
						throw new ArgumentException("Invalid trough type " + Type);
				}
			}
		}

		/// <summary>
		/// Time in milliseconds it takes the switch to disable after ball starts rolling.
		/// </summary>
		/// <exception cref="ArgumentException"></exception>
		public int RollTimeDisabled {
			get {
				switch (Type) {
					case TroughType.ModernOpto:
						return RollTime - TransitionTime;

					case TroughType.ModernMech:
					case TroughType.TwoCoilsNSwitches:
					case TroughType.TwoCoilsOneSwitch:
					case TroughType.ClassicSingleBall:
						return RollTime / 2;

					default:
						throw new ArgumentException("Invalid trough type " + Type);
				}
			}
		}

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(TroughData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			Type = data.Type;
			BallCount = data.BallCount;
			SwitchCount = data.SwitchCount;
			JamSwitch = data.JamSwitch;
			RollTime = data.RollTime;
			TransitionTime = data.TransitionTime;
			KickTime = data.KickTime;

			return updatedComponents;
		}

		public override IEnumerable<MonoBehaviour> SetReferencedData(TroughData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IItemMainAuthoring> components)
		{
			PlayfieldEntrySwitch = GetAuthoring<ITriggerAuthoring>(components, data.PlayfieldEntrySwitch);
			PlayfieldExitKicker = GetAuthoring<KickerAuthoring>(components, data.PlayfieldExitKicker);

			return Array.Empty<MonoBehaviour>();
		}

		public override TroughData CopyDataTo(TroughData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			data.Name = name;

			data.Type = Type;
			data.PlayfieldEntrySwitch = PlayfieldEntrySwitch == null ? string.Empty : PlayfieldEntrySwitch.name;
			data.PlayfieldExitKicker = PlayfieldExitKicker == null ? string.Empty : PlayfieldExitKicker.name;
			data.BallCount = BallCount;
			data.SwitchCount = SwitchCount;
			data.JamSwitch = JamSwitch;
			data.RollTime = RollTime;
			data.TransitionTime = TransitionTime;
			data.KickTime = KickTime;

			return data;
		}

		#endregion

		#region ISwitchableDevice

		public const string EntrySwitchId = "drain_switch";
		public const string TroughSwitchId = "trough_switch";
		public const string JamSwitchId = "jam_switch";

		public IEnumerable<GamelogicEngineSwitch> AvailableSwitches {
			get {

				switch (Type) {
					case TroughType.ModernOpto:
					case TroughType.ModernMech:
						return Enumerable.Repeat(0, SwitchCount)
							.Select((_, i) => new GamelogicEngineSwitch($"ball_switch_{i + 1}", i + 1)
								{ Description = SwitchDescription(i) })
							.Concat(JamSwitch
								? new [] { new GamelogicEngineSwitch(JamSwitchId) { Description = "Jam Switch" }}
								: Array.Empty<GamelogicEngineSwitch>()
							);

					case TroughType.TwoCoilsNSwitches:
						return new[] {
							new GamelogicEngineSwitch(EntrySwitchId) { Description = "Entry Switch" }
						}.Concat(Enumerable.Repeat(0, SwitchCount)
							.Select((_, i) => new GamelogicEngineSwitch($"ball_switch_{i + 1}", i + 1)
								{ Description = SwitchDescription(i) } )
						).Concat(JamSwitch
							? new [] { new GamelogicEngineSwitch(JamSwitchId) { Description = "Jam Switch" }}
							: Array.Empty<GamelogicEngineSwitch>()
						);

					case TroughType.TwoCoilsOneSwitch:
						return new[] {
							new GamelogicEngineSwitch(EntrySwitchId) { Description = "Entry Switch" },
							new GamelogicEngineSwitch(TroughSwitchId) { Description = "Trough Switch" },
						}.Concat(JamSwitch
							? new [] { new GamelogicEngineSwitch(JamSwitchId) { Description = "Jam Switch" }}
							: Array.Empty<GamelogicEngineSwitch>()
						);

					case TroughType.ClassicSingleBall:
						return new[] {
							new GamelogicEngineSwitch(EntrySwitchId) { Description = "Drain Switch" },
						};

					default:
						throw new ArgumentException("Invalid trough type " + Type);
				}
			}
		}

		private string SwitchDescription(int i)
		{
			if (i == 0) {
				return "Ball 1 (eject)";
			}


			return i == SwitchCount - 1
				? $"Ball {i + 1} (entry)"
				: $"Ball {i + 1}";
		}

		#endregion

		#region ICoilableDevice

		public const string EjectCoilId = "eject_coil";
		public const string EntryCoilId = "entry_coil";

		public IEnumerable<GamelogicEngineCoil> AvailableCoils {
			get {
				switch (Type) {
					case TroughType.ModernOpto:
					case TroughType.ModernMech:
						return new[] {
							new GamelogicEngineCoil(EjectCoilId) { Description = "Eject" }
						};
					case TroughType.TwoCoilsNSwitches:
					case TroughType.TwoCoilsOneSwitch:
						return new[] {
							new GamelogicEngineCoil(EntryCoilId) { Description = "Entry" },
							new GamelogicEngineCoil(EjectCoilId) { Description = "Eject" }
						};
					case TroughType.ClassicSingleBall:
						return new[] {
							new GamelogicEngineCoil(EjectCoilId) { Description = "Eject" }
						};
					default:
						throw new ArgumentException("Invalid trough type " + Type);
				}
			}
		}

		IEnumerable<IGamelogicEngineDeviceItem> IWireableAuthoring.AvailableWireDestinations => AvailableCoils;
		IEnumerable<IGamelogicEngineDeviceItem> IDeviceAuthoring<IGamelogicEngineDeviceItem>.AvailableDeviceItems => AvailableCoils;

		#endregion

		#region Runtime

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterTrough(this);
		}

		#endregion

		#region Editor Tools

		private Vector3 EntryPos(float height)
		{
			return PlayfieldEntrySwitch == null
				? Vector3.zero
				: new Vector3(PlayfieldEntrySwitch.Center.x, PlayfieldEntrySwitch.Center.y, height);
		}

		private Vector3 ExitPos(float height) => PlayfieldExitKicker == null
			? Vector3.zero
			: new Vector3(PlayfieldExitKicker.Position.x, PlayfieldExitKicker.Position.y, height);


		private void OnDrawGizmosSelected()
		{
			Profiler.BeginSample("TroughAuthoring.OnDrawGizmosSelected");
			if (PlayfieldEntrySwitch != null && PlayfieldExitKicker != null) {
				var ltw = GetComponentInParent<TableAuthoring>().transform;
				var entryPos = EntryPos(0f);
				var exitPos = ExitPos(0f);
				var entryWorldPos = ltw.TransformPoint(entryPos);
				var exitWorldPos = ltw.TransformPoint(exitPos);
				var localPos = transform.localPosition;
				var localPos0 = new Vector3(localPos.x, localPos.y, 0f);
				var pos = ltw.TransformPoint(localPos0);
				DrawArrow(entryWorldPos, pos - entryWorldPos);
				DrawArrow(pos, exitWorldPos - pos);
			}
			Profiler.EndSample();
		}

		public void UpdatePosition()
		{
			// place trough between entry and exit kicker
			var pos = (EntryPos(75f) + ExitPos(75f)) / 2;
			transform.localPosition = pos;
		}

		#endregion
	}
}
