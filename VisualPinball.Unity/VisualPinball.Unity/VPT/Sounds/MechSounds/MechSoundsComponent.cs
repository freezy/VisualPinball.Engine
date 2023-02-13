// Visual Pinball Engine
// Copyright (C) 2023 freezy and VPE Team
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


using System;
using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.MechSounds;
using VisualPinball.Engine.VPT.Table;


namespace VisualPinball.Unity
{

	[AddComponentMenu("Visual Pinball/Sounds/Mechanical Sounds")]
	public class MechSoundsComponent : MainComponent<MechSoundsData>, ISoundEmitter
	{
		#region Data

		public List<MechSoundsData.MechSound> SoundList; 

		[SerializeField]
		private SoundTrigger[] _availableTriggers;
		public SoundTrigger[] AvailableTriggers
		{
			get { return _availableTriggers; }
			set { _availableTriggers = value; }
		}

		public SoundTrigger SelectedTrigger;

		[SerializeField]
		private VolumeEmitter[] _availableEmitters;
		public VolumeEmitter[] AvailableEmitters
		{

			get
			{
				_availableEmitters = GetVolumeEmitters(SelectedTrigger);
				return _availableEmitters;
			}
			set { _availableEmitters = value; }
		}

		public VolumeEmitter[] GetVolumeEmitters(SoundTrigger trigger)
		{

			string Id;
			string Name;
			string[] Ids;
			string[] Names;

			switch (trigger.Name)
			{
				case "Coil On":
					Ids = new string[1];
					Names = new string[1];
					Id = "fixed";
					Name = "Fixed";
					Ids[0] = Id;
					Names[0] = Name;
					break;
				case "Coil Off":
					Ids = new string[1];
					Names = new string[1];
					Id = "fixed";
					Name = "Fixed";
					Ids[0] = Id;
					Names[0] = Name;
					break;
				case "Ball Collision":
					Ids = new string[1];
					Names = new string[1];
					Id = "ball_velocity";
					Name = "Ball Velocity";
					Ids[0] = Id;
					Names[0] = Name;
					break;
				default:
					Ids = new string[1];
					Names = new string[1];
					Id = "fixed";
					Name = "Fixed";
					Ids[0] = Id;
					Names[0] = Name;
					break;
			}

			int index = Ids.Length;
			VolumeEmitter volEmitter;
			VolumeEmitter[] volEmitters = new VolumeEmitter[index];

			for (int i = 0; i < index; i++)
			{
				volEmitter = new VolumeEmitter();
				volEmitter.Id = Ids[i];
				volEmitter.Name = Names[i];
				volEmitters[i] = volEmitter;
			}

			return volEmitters;
		}
		#endregion

		#region ISoundEmitter
		[SerializeField]
		public event EventHandler<SoundEventArgs> OnSound;

		private void _OnSound(object sender, SwitchEventArgs e)
		{
			OnSound?.Invoke(this, new SoundEventArgs { Trigger = SelectedTrigger, Volume = SoundList[0].Volume });
		}

		#endregion

		#region Overrides and Constants

		public override ItemType ItemType => ItemType.Sound;
		public override string ItemName => "Mechanical Sounds";

		public override MechSoundsData InstantiateData() => new MechSoundsData();

		public override bool HasProceduralMesh => false;

		#endregion
		

		#region Conversion

		public override IEnumerable<MonoBehaviour> SetData(MechSoundsData data)
		{
			var updatedComponents = new List<MonoBehaviour> { this };

			SoundList = data.SoundList;
			AvailableTriggers = data.AvailableTriggers;
			SelectedTrigger = data.SelectedTrigger;
			AvailableEmitters = data.AvailableEmitters;

			return updatedComponents;
		}

		
		public override MechSoundsData CopyDataTo(MechSoundsData data, string[] materialNames, string[] textureNames, bool forExport)
		{
			data.Name = name;
			data.SoundList= SoundList;
			data.AvailableTriggers = AvailableTriggers;
			data.SelectedTrigger = SelectedTrigger;
			data.AvailableEmitters = AvailableEmitters;
			
			return data;
		}


		public override IEnumerable<MonoBehaviour> SetReferencedData(MechSoundsData data, Table table, IMaterialProvider materialProvider, ITextureProvider textureProvider, Dictionary<string, IMainComponent> components)
		{
		
			return Array.Empty<MonoBehaviour>();
		}


		#endregion

		#region Runtime

		private FlipperApi _flipperApi;

		private void Awake()
		{
			GetComponentInParent<Player>().RegisterMechSound(this);
		}

		private void Start()
		{
			_flipperApi = GetComponentInParent<Player>().TableApi.Flipper(this);
			_flipperApi.Switch += _OnSound;


		}

		private void Update()
		{
			
		}

		#endregion

		#region Editor Tools



		#endregion
	}
}

