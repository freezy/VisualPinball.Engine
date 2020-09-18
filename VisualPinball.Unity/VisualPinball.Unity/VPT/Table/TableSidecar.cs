// Visual Pinball Engine
// Copyright (C) 2020 freezy and VPE Team
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

using System.Collections.Generic;
using UnityEngine;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.MappingConfig;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;

namespace VisualPinball.Unity
{
	/// <summary>
    /// This monobehavior is meant to hold all the (large) serialized data needed to reconstruct
    /// a vpx table. We're storing this off on a different object so that selecting the table itself
    /// doesn't cause the editor to slow to a crawl
    /// </summary>
	internal class TableSidecar : ScriptableObject
    {
		[HideInInspector] public Dictionary<string, string> tableInfo = new SerializableDictionary<string, string>();
		[HideInInspector] public TableSerializedTextureContainer textures = new TableSerializedTextureContainer();
		[HideInInspector] public CustomInfoTags customInfoTags;
		[HideInInspector] public List<CollectionData> collections;
		[HideInInspector] public List<MappingConfigData> mappingConfigs;
		[HideInInspector] public DecalData[] decals;
		[HideInInspector] public DispReelData[] dispReels;
		[HideInInspector] public FlasherData[] flashers;
		[HideInInspector] public LightSeqData[] lightSeqs;
		[HideInInspector] public PlungerData[] plungers;
		[HideInInspector] public TableSerializedSoundContainer sounds = new TableSerializedSoundContainer();
		[HideInInspector] public TextBoxData[] textBoxes;
		[HideInInspector] public TimerData[] timers;
	}
}
