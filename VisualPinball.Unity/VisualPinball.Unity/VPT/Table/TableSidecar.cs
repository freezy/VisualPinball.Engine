using UnityEngine;
using System.Collections.Generic;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Common;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.Collection;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Sound;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;

namespace VisualPinball.Unity.VPT.Table
{
	/// <summary>
    /// This monobehavior is meant to hold all the (large) serialized data needed to reconstruct
    /// a vpx table. We're storing this off on a different object so that selecting the table itself
    /// doesn't cause the editor to slow to a crawl
    /// </summary>
	public class TableSidecar : MonoBehaviour
    {
		[HideInInspector] public Dictionary<string, string> tableInfo = new SerializableDictionary<string, string>();
		[HideInInspector] public TableSerializedTexture[] textures;
		[HideInInspector] public CustomInfoTags customInfoTags;
		[HideInInspector] public CollectionData[] collections;
		[HideInInspector] public DecalData[] decals;
		[HideInInspector] public DispReelData[] dispReels;
		[HideInInspector] public FlasherData[] flashers;
		[HideInInspector] public LightSeqData[] lightSeqs;
		[HideInInspector] public PlungerData[] plungers;
		[HideInInspector] public SoundData[] sounds;
		[HideInInspector] public TextBoxData[] textBoxes;
		[HideInInspector] public TimerData[] timers;
	}
}
