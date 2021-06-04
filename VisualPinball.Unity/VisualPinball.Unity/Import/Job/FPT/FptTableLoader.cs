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

// ReSharper disable PossibleNullReferenceException

using NLog;
using OpenMcdf;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using VisualPinball.Engine.VPT;
using VisualPinball.Engine.VPT.Bumper;
using VisualPinball.Engine.VPT.Decal;
using VisualPinball.Engine.VPT.DispReel;
using VisualPinball.Engine.VPT.Flasher;
using VisualPinball.Engine.VPT.Flipper;
using VisualPinball.Engine.VPT.Gate;
using VisualPinball.Engine.VPT.HitTarget;
using VisualPinball.Engine.VPT.Kicker;
using VisualPinball.Engine.VPT.Light;
using VisualPinball.Engine.VPT.LightSeq;
using VisualPinball.Engine.VPT.Plunger;
using VisualPinball.Engine.VPT.Primitive;
using VisualPinball.Engine.VPT.Ramp;
using VisualPinball.Engine.VPT.Rubber;
using VisualPinball.Engine.VPT.Spinner;
using VisualPinball.Engine.VPT.Surface;
using VisualPinball.Engine.VPT.Table;
using VisualPinball.Engine.VPT.TextBox;
using VisualPinball.Engine.VPT.Timer;
using VisualPinball.Engine.VPT.Trigger;
using VisualPinball.Engine.VPT.Trough;

using UnityEngine;
using VisualPinball.Unity.FP.defs;
using System.Collections.Generic;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.FP
{
	public static class FptUtils
	{
		const float fp2vpUnits = 47F / 26.9875F; // "50 units = 1 1/16 inches" to mm, https://www.vpforums.org/index.php?showtopic=24895
		public static float mm2VpUnits(float mm) { return mm * fp2vpUnits; }
		public static Vertex2D mm2VpUnits(Vertex2D mm) { return mm * fp2vpUnits; }
		public static Vertex3D mm2VpUnits(Vertex3D mm) { return mm * fp2vpUnits; }
		public static DragPointData[] FPShapePoint2DragPointData(List<FPShapePoint> list)
		{
			if (list == null)
				return null;
			var ret = new DragPointData[list.Count];
			for(int i=0;i<list.Count;i++)
				ret[i] = list[i].ToVpx();
			return ret;
		}
	}
	public static class FptTableLoader
	{
		private static readonly NLog.Logger Logger = LogManager.GetCurrentClassLogger();


		private static Table loadingTable = null;

		public static Table LoadTable(string path)
		{ 
			var cf = new CompoundFile(path);
			try
			{
				CFItem item = cf.GetAllNamedEntries("Table Data")[0];
				var fpTableData = FPBaseHandler.GetTableData((CFStream)item);

				TableData tableData = fpTableData.ToVpx();
				loadingTable = new Table(tableData);
				loadingTable.TableInfo = fpTableData.PopulateTableInfo();

				var mainStorage = cf.RootStorage.GetStorage("Future Pinball");

				// Load unloaded resources
				//mainStorage.VisitEntries(VisitResources, false);
				//AssetDatabase.Refresh();

				mainStorage.VisitEntries(VisitSurfaces, false);
				//mainStorage.VisitEntries(VisitWalls, false);
				mainStorage.VisitEntries(VisitElements, false);

				//LoadGameItems(table);

				/*				var gameStorage = cf.RootStorage.GetStorage("GameStg");
				var gameData = gameStorage.GetStream("GameData");
/*
				var fileVersion = BitConverter.ToInt32(gameStorage.GetStream("Version").GetData(), 0);
				using (var stream = new MemoryStream(gameData.GetData()))
				using (var reader = new BinaryReader(stream))
				{
					var table = new Table(reader);

					LoadTableInfo(table, cf.RootStorage, gameStorage);
					if (loadGameItems)
					{
						LoadGameItems(table, gameStorage);
					}
					LoadTextures(table, gameStorage);
					LoadSounds(table, gameStorage, fileVersion);
					LoadCollections(table, gameStorage);
					LoadMappings(table, gameStorage);
					LoadTableMeta(table, gameStorage);

					table.SetupPlayfieldMesh();
					return table;
				}
				*/

				return loadingTable;

			}
			finally
			{
				cf.Close();
			}

//			var table = Table.Load(path, false);

//			var job = new GameItemJob(table.Data.NumGameItems);
//			var gameItems = Engine.VPT.Table.TableLoader.ReadGameItems(path, table.Data.NumGameItems);
//			for (var i = 0; i < table.Data.NumGameItems; i++) {
//				job.Data[i] = MemHelper.FromByteArray(gameItems[i]);
//				job.DataLength[i] = gameItems[i].Length;
//			}

//			// parse threaded
//			var handle = job.Schedule(table.Data.NumGameItems, 64);
//			handle.Complete();

//			// update table with results
//			for (var i = 0; i < table.Data.NumGameItems; i++) {
//				if (job.ItemObj[i].ToInt32() > 0) {
//					var objHandle = (GCHandle) job.ItemObj[i];
//					switch ((ItemType)job.ItemType[i]) {
//						case ItemType.Bumper: {
//							table.Add(objHandle.Target as Bumper);
//							break;
//						}
//						case ItemType.Decal: {
//							table.Add(objHandle.Target as Decal);
//							break;
//						}
//						case ItemType.DispReel: {
//							table.Add(objHandle.Target as DispReel);
//							break;
//						}
//						case ItemType.Flasher: {
//							table.Add(objHandle.Target as Flasher);
//							break;
//						}
//						case ItemType.Flipper: {
//							table.Add(objHandle.Target as Flipper);
//							break;
//						}
//						case ItemType.Gate: {
//							table.Add(objHandle.Target as Gate);
//							break;
//						}
//						case ItemType.HitTarget: {
//							table.Add(objHandle.Target as HitTarget);
//							break;
//						}
//						case ItemType.Kicker: {
//							table.Add(objHandle.Target as Kicker);
//							break;
//						}
//						case ItemType.Light: {
//							table.Add(objHandle.Target as Light);
//							break;
//						}
//						case ItemType.LightSeq: {
//							table.Add(objHandle.Target as LightSeq);
//							break;
//						}
//						case ItemType.Plunger: {
//							table.Add(objHandle.Target as Plunger);
//							break;
//						}
//						case ItemType.Primitive: {
//							table.Add(objHandle.Target as Primitive);
//							break;
//						}
//						case ItemType.Ramp: {
//							table.Add(objHandle.Target as Ramp);
//							break;
//						}
//						case ItemType.Rubber: {
//							table.Add(objHandle.Target as Rubber);
//							break;
//						}
//						case ItemType.Spinner: {
//							table.Add(objHandle.Target as Spinner);
//							break;
//						}
//						case ItemType.Surface: {
//							table.Add(objHandle.Target as Surface);
//							break;
//						}
//						case ItemType.TextBox: {
//							table.Add(objHandle.Target as TextBox);
//							break;
//						}
//						case ItemType.Timer: {
//							table.Add(objHandle.Target as Timer);
//							break;
//						}
//						case ItemType.Trigger: {
//							table.Add(objHandle.Target as Trigger);
//							break;
//						}
//						case ItemType.Trough: {
//							table.Add(objHandle.Target as Trough);
//							break;
//						}
//						default:
//							throw new ArgumentException("Unknown item type " + (ItemType)job.ItemType[i] + ".");
//					}
//				}
//			}
//			table.SetupPlayfieldMesh();
//			job.Dispose();
			
			return null;
		}

		static void VisitSurfaces(CFItem item)
        {
            if (item.Name.Contains("Table Element"))
            {
                RawData rawData = new RawData((CFStream)item);
                ChunkChunkList chunks = new ChunkChunkList(ChunkTypes.CHUNK_TABLE_ELEMENT);
                GCHandle handle = GCHandle.Alloc(rawData.data, GCHandleType.Pinned);
                int datavalue = Marshal.ReadInt32(handle.AddrOfPinnedObject());
                handle.Free();
                chunks.Add((ChunkGeneric)new ChunkInt(4, 0, ChunkTypes.CHUNK_ELEMENT_TYPE, datavalue));
                byte[] newdata = new byte[rawData.len - 4];
                Array.Copy(rawData.data, 4, newdata, 0, rawData.len - 4);
                RawData validRawData = new RawData(rawData.len - 4, newdata);
                if (datavalue == 2)////Surfaces
                {
                    FPBaseHandler.analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_2_ARRAY, validRawData);
                    string surfaceName = ((ChunkWString)FPBaseHandler.getChunkByLabel((ChunkChunkList)chunks, "name"))._value;

					//if (IsTableElement(surfaceName)) // Don't reload if already existing
					//    return;

					FP_Surface fps = new FP_Surface();
					FPBaseHandler.ChunksToFP(chunks, fps, false);
					if (fps.render_object) // Do not all the work for invisible dummy object
						fps.shape_points = FPBaseHandler.GetShapePoints(chunks);

					var vpxConv = fps.ToVpx();
					if (vpxConv.Points)
						loadingTable.Add<Surface>(new Surface(fps.ToVpx()));
	 //              
				}
            }
        }

		//static void VisitWalls(CFItem item)
		//{
		//	if (item.Name.Contains("Table Element"))
		//	{
		//		RawData rawData = new RawData((CFStream)item);
		//		ChunkChunkList chunks = new ChunkChunkList(ChunkTypes.CHUNK_TABLE_ELEMENT);
		//		GCHandle handle = GCHandle.Alloc(rawData.data, GCHandleType.Pinned);
		//		int datavalue = Marshal.ReadInt32(handle.AddrOfPinnedObject());
		//		handle.Free();
		//		chunks.Add((ChunkGeneric)new ChunkInt(4, 0, ChunkTypes.CHUNK_ELEMENT_TYPE, datavalue));
		//		byte[] newdata = new byte[rawData.len - 4];
		//		Array.Copy(rawData.data, 4, newdata, 0, rawData.len - 4);
		//		RawData validRawData = new RawData(rawData.len - 4, newdata);
		//		if (datavalue == 16)////Walls
		//		{
		//			FPBaseHandler.analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_16_ARRAY, validRawData);
		//			string wallName = ((ChunkWString)FPBaseHandler.getChunkByLabel((ChunkChunkList)chunks, "name"))._value;
		//			//if (IsTableElement(wallName)) // Don't reload if already existing
		//			//	return;

		//			FP_Wall fpw = new FP_Wall();
		//			FPBaseHandler.ChunksToFP(chunks, fpw, false);
		//			fpw.shape_points = FPBaseHandler.GetShapePoints(chunks);

		//		}
		//	}
		//}

		static void VisitElements(CFItem item)
		{
			if (item.Name.Contains("Table Element"))
			{
				RawData rawData = new RawData((CFStream)item);
				ChunkChunkList chunks = new ChunkChunkList(ChunkTypes.CHUNK_TABLE_ELEMENT);
				GCHandle handle = GCHandle.Alloc(rawData.data, GCHandleType.Pinned);
				int datavalue = Marshal.ReadInt32(handle.AddrOfPinnedObject());
				handle.Free();
				chunks.Add((ChunkGeneric)new ChunkInt(4, 0, ChunkTypes.CHUNK_ELEMENT_TYPE, datavalue));
				byte[] newdata = new byte[rawData.len - 4];
				Array.Copy(rawData.data, 4, newdata, 0, rawData.len - 4);
				RawData validRawData = new RawData(rawData.len - 4, newdata);
				
				// Get the element name and optionnal parameters
				string elementName = "";
				ImporterParams importerParams = new ImporterParams();
				{
					FPBaseHandler.analyseRawData(chunks, Descriptors.CHUNKS_DEFAULTNAME_ARRAY, validRawData);
					ChunkWString nameChunk = FPBaseHandler.getChunkByLabel(chunks, "name") as ChunkWString;
					elementName = nameChunk != null ? nameChunk._value : "";
					elementName = importerParams.Parse(elementName);
					if (nameChunk != null)
						nameChunk._value = elementName;
				}

				bool doNotProcess = false;// (elementName == "DummyTrigger") || IsTableElement(elementName);

				if (!doNotProcess)
				{
					switch (datavalue)
					{
						case 2: break;//surfaces, handled by "VisitSurfaces"
						//case 3:///RoundShapeLamp
						//	analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_3_ARRAY, validRawData);

						//	// Is Light coding for magnet ? (Special Case as Magnets are no elements in FP)
						//	if (importerParams.Contains("IsMagnet")
						//		|| (elementName.Contains("PU3D#Magnet#") || elementName.Contains("UP3D#Magnet#"))) // For backward compatibility
						//	{
						//		elementName = elementName.Replace("PU3D#Magnet#", ""); // For backward compatibility
						//		elementName = elementName.Replace("UP3D#Magnet#", ""); // For backward compatibility

						//		GameObject obj = CreateFromPrefab("Assets/PinAssets/_IO/BaseSolenoidIO", elementName);
						//		FP_RoundShapeLamp infos = obj.AddComponent<FP_RoundShapeLamp>();
						//		ChunksToFP(chunks, infos, false);

						//		MagnetBuilder.Setup(obj, infos);
						//		roundShapeIsMagnet = true;
						//		break; ;
						//	}


						//	ShapeLampBuilder srlb = ShapeLampBuilder.Create(elementName, true, false);

						//	srlb.gameObject = CreateFromPrefab("Assets/PinAssets/_IO/Lamps/BaseLampIO", elementName);

						//	shapelamp_builders.Add(srlb);
						//	FP_RoundShapeLamp fprsl = srlb.gameObject.AddComponent<FP_RoundShapeLamp>();
						//	ChunksToFP(chunks, fprsl, false);
						//	FPUtilsEditor.CorrectEmptySurface(ref fprsl.surface, fprsl.object_appers_on);
						//	fprsl.position = new Vector3(fprsl.position.x * Globals.g_Scale, fprsl.position.y * Globals.g_Scale);
						//	fprsl.shape_points = FPUtils.MakeCircleShapePointList(fprsl.position, 32, fprsl.diameter * 0.5f * Globals.g_Scale);
						//	srlb.Init(fprsl, importerParams);
						//	FPUtilsEditor.SetRoundShapeLampMaterial(srlb);
						//	break;

						//case 4:///ShapeLamp
						//	analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_4_ARRAY, validRawData);
						//	ShapeLampBuilder slb = ShapeLampBuilder.Create(elementName, false, false);
						//	slb.gameObject = CreateFromPrefab("Assets/PinAssets/_IO/Lamps/BaseLampIO", elementName);
						//	shapelamp_builders.Add(slb);
						//	FP_ShapeLamp fpsl = slb.gameObject.AddComponent<FP_ShapeLamp>();
						//	ChunksToFP(chunks, fpsl, false);
						//	FPUtilsEditor.CorrectEmptySurface(ref fpsl.surface, fpsl.object_appers_on);
						//	fpsl.shape_points = GetShapePointsLight(chunks);
						//	slb.Init(fpsl, importerParams);
						//	FPUtilsEditor.SetShapeLampMaterial(slb);
						//	break;
						//case 6: // Pegs
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_6_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks);

						//		var fp = obj.AddComponent<FP_Peg>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			Color mainC = fp.color;
						//			mainC.a = fp.crystal != 0 ? 0.3f : mainC.a;
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													fp.crystal != 0 ? "BackReflTrans" : "TexBackTrans",
						//													mainC,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(obj, 0),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h));
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material);
						//	}
						//	break;
						case 7: // Flippers
							{
								FPBaseHandler.analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_7_ARRAY, validRawData);
								var fp = new FP_Flipper();
								FPBaseHandler.ChunksToFP(chunks, fp, false);

								loadingTable.Add(new Flipper(fp.ToVpx()));
								// MODEL PRIMITIVE ?

							}
							break;
						//case 8: // Bumpers
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_8_ARRAY, validRawData);

						//		var obj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Bumpers/BaseBumperIO");
						//		var fp = obj.AddComponent<FP_Bumper>();
						//		ChunksToFP(chunks, fp, false);
						//		BumperBuilder.Setup(obj, fp, importerParams);
						//	}
						//	break;
						//case 10: // Leaf targets
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_10_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Switches/BaseSwitchIO");

						//		var fp = obj.AddComponent<FP_LeafTarget>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													"TexRefl",
						//													fp.color,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(obj, 2),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h));
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material);
						//		foreach (var c in obj.GetComponentsInChildren<Collider>())
						//			c.gameObject.AddComponent<UP2.Behaviours.BasicSwitch>();
						//	}
						//	break;
						//case 11: // Drop targets bank
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_11_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Switches/BaseSwitchIO");

						//		var fp = obj.AddComponent<FP_DropTargetsBank>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													"TexRefl",
						//													fp.color,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(obj, 2),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h));
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material);
						//		DropTargetsBankBuilder.Setup(obj, fp);
						//	}
						//	break;
						//case 12: // Plungers
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_12_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks);//, "Assets/PinAssets/_IO/Switches/BaseSwitchIO");

						//		var fp = obj.AddComponent<FP_Plunger>();
						//		ChunksToFP(chunks, fp, false);

						//		PlungerBuilder.Create(obj, fp);
						//		//Material material = null;
						//		//{
						//		//    float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//		//    material = FPUtilsEditor.Set_Material(obj.name,
						//		//                                            "TexRefl",
						//		//                                            fp.color,
						//		//                                            fp.texture,
						//		//                                            FindMaterialTypeFromColliders(obj, 2),
						//		//                                            /*3000 +*/ Mathf.RoundToInt(parent_h));
						//		//}

						//		//ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material);
						//		//DropTargetsBankBuilder.Setup(obj, fp);
						//	}
						//	break;
						//case 13: // Rubber
						//	{
						//		//reverseAnanlyseRawData(datavalue, chunks, Descriptors.CHUNKS_ELEMENT_13_ARRAY, validRawData);
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_13_ARRAY, validRawData);
						//		GameObject obj = new GameObject(elementName);
						//		var rub = obj.AddComponent<FP_Rubber>();
						//		ChunksToFP(chunks, rub, false);
						//		RubberBuilder.Create(obj, rub);
						//	}
						//	break;
						//case 14: // Shapeable Rubber
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_14_ARRAY, validRawData);
						//		GameObject shr = new GameObject(elementName);
						//		ShapeableRubberBuilder srBuilder = ShapeableRubberBuilder.Create(shr);
						//		srBuilder.AttachToParent();  //To be referenced as table object
						//		shapeablerubbers_builders.Add(srBuilder);

						//		FP_ShapeableRubber fp = shr.AddComponent<FP_ShapeableRubber>();
						//		ChunksToFP(chunks, fp, false);
						//		bool hasSlingshot = false, hasLeaf = false;
						//		fp.shape_points = GetShapeableRubberPoints(chunks, ref hasSlingshot, ref hasLeaf, false);


						//		GameObject slingshotHammer = null, leafPrefab = null;
						//		// TODO: from importer params
						//		if (hasSlingshot)
						//			slingshotHammer = CreateFromPrefab("Assets/PinAssets/Hammers/FP_slinghammer_alone", "slinghammer");

						//		if (hasLeaf)
						//			leafPrefab = CreateFromPrefab("Assets/PinAssets/Switches/SingleLeaf", "singleleaf", false);

						//		srBuilder.Init(fp, slingshotHammer, leafPrefab);
						//		//FPUtilsEditor.SetWireGuideMaterial(wgb);
						//	}
						//	break;
						//case 15: // Ornaments
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_15_ARRAY, validRawData);
						//		GameObject ornObj = SetupModelBased(ref chunks);

						//		var fp = ornObj.AddComponent<FP_Ornaments>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(ornObj.name,
						//													"TexRefl",
						//													fp.color,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(ornObj, 2),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h + fp.offset));
						//		}

						//		bool hasMask = ornObj.GetComponentInChildren<MaskPoints>() != null;
						//		ModelBased.SetupStandardModelBasedObject(ornObj, fp.position, fp.rotation, fp.surface, material, false, true, hasMask);
						//	}
						//	break;
						//case 16: break;  //walls
						//case 18: // Decals
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_18_ARRAY, validRawData);
						//		GameObject obj = new GameObject(elementName);
						//		var decal = obj.AddComponent<FP_Decal>();
						//		ChunksToFP(chunks, decal, false);
						//		FPUtils.Attach_GameObject(obj, decal.surface, true);
						//		DecalBuilder.Generate(decal);
						//		break;
						//	}
						//case 19: // Kicker
						//	{
						//		//reverseAnanlyseRawData(datavalue, chunks, Descriptors.CHUNKS_ELEMENT_19_ARRAY, validRawData);
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_19_ARRAY, validRawData);
						//		GameObject kickerObj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Kickers/BaseKickerIO");

						//		var kicker = kickerObj.AddComponent<FP_Kicker>();
						//		ChunksToFP(chunks, kicker, false);

						//		Material material = null;
						//		{
						//			float parent_h = 0F;// FPUtilsEditor.GetParentTopHeight(kicker.surface);
						//			material = FPUtilsEditor.Set_Material(kickerObj.name,
						//													"TexRefl",
						//													kicker.color,
						//													kicker.texture,
						//													FindMaterialTypeFromColliders(kickerObj, 2),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h));// + kicker.offset));
						//		}

						//		ModelBased.SetupStandardModelBasedObject(kickerObj, kicker.position, kicker.rotation, "", material, true, true, true);

						//		var bc = kickerObj.AddComponent<BoxCollider>();
						//		var mf = kickerObj.GetComponentInChildren<MeshFilter>();
						//		var bnd = mf.sharedMesh.bounds;

						//		float triggerWidth = 3F; // in mm
						//		var center = bnd.center;
						//		var size = bnd.size;
						//		center.y = center.y - size.y * 0.5F + triggerWidth;
						//		size.y = triggerWidth;
						//		bc.center = center;// new Vector3(0F, kickerObj.transform.localPosition.x * 2, 0F);
						//		bc.size = size;// new Vector3(bc.size.x,0.01F,bc.size.z);
						//		bc.isTrigger = true;

						//		Behaviours.Kicker kick = kickerObj.GetComponent<Behaviours.Kicker>();
						//		if (kick != null)
						//		{
						//			kick.m_type = kicker.type;
						//			kick.m_strength = kicker.strength;
						//			kick.m_releaseTime = 0.25F / (kicker.strength + 1);
						//			if (kick.m_audioOn != null && !string.IsNullOrEmpty(kicker.sound_when_hit))
						//				kick.m_audioOn.clip = FPUtilsEditor.FindAudioClip(kicker.sound_when_hit);
						//		}

						//		break;
						//	}
						//case 20: // LaneGuides
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_20_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks);

						//		var fp = obj.AddComponent<FP_LaneGuide>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			Color mainC = fp.color;
						//			mainC.a = fp.crystal != 0 ? 0.3f : mainC.a;
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													fp.crystal != 0 ? "BackReflTrans" : "TexBackTrans",
						//													mainC,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(obj, 0),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h));
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material);
						//	}
						//	break;
						//case 21: // MOdel Rubbers
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_21_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks);

						//		var fp = obj.AddComponent<FP_ModelRubber>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													"ReflTexBackTrans",
						//													fp.color,
						//													fp.texture,
						//													3,
						//													/*3000 +*/ Mathf.RoundToInt(parent_h));
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material);
						//		foreach (var col in obj.GetComponentsInChildren<Collider>())
						//		{
						//			col.material = (PhysicMaterial)PhysicMaterial.Instantiate(Resources.Load("PhysicMaterials/Rubber") as PhysicMaterial);
						//			col.material.bounciness = FPUtilsEditor.set_elasticity(fp.elasticity);
						//		}
						//	}
						//	break;
						//case 22: // Switchs
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_22_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Switches/BaseSwitchIO");

						//		var fp = obj.AddComponent<FP_Switch>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													"ReflTex",
						//													fp.color,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(obj, 0),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h));
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material);
						//		foreach (var c in obj.GetComponentsInChildren<Collider>())
						//			c.gameObject.AddComponent<UP2.Behaviours.BasicSwitch>();
						//	}
						//	break;
						//case 23: // Flashers
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_23_ARRAY, validRawData);
						//		var obj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Lamps/BaseLampIO");

						//		var fp = obj.AddComponent<FP_Flasher>();
						//		ChunksToFP(chunks, fp, false);

						//		Color mainC = fp.lit_color;
						//		mainC.a = 0.3f;

						//		float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//		var material = FPUtilsEditor.Set_Material(fp.name + "_Flasher",
						//												"BackReflTrans",
						//												mainC,
						//												"",
						//												.8f,
						//												.8f,
						//												1.5f,
						//												/*3000 +*/ Mathf.RoundToInt(parent_h),
						//												obj.GetComponentInChildren<Renderer>()?.gameObject);

						//		var fpBulb = obj.AddComponent<FP_Bulb>();
						//		fpBulb.SetupFromFlasher(fp);
						//		Bulb.Setup(obj, fpBulb, importerParams, material);
						//	}
						//	break;
						//case 24: //WireGuide
						//	analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_24_ARRAY, validRawData);
						//	WireGuideBuilder wgb = WireGuideBuilder.Create(elementName, false);
						//	wgb.gameObject = new GameObject(elementName);
						//	wireguide_builders.Add(wgb);
						//	FP_WireGuide fpwg = wgb.gameObject.AddComponent<FP_WireGuide>();
						//	ChunksToFP(chunks, fpwg, false);
						//	fpwg.shape_points = GetShapePoints(chunks);
						//	wgb.Init(fpwg);
						//	FPUtilsEditor.SetWireGuideMaterial(wgb);
						//	break;
						//case 27: // Overlays
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_27_ARRAY, validRawData);
						//		GameObject obj = null;
						//		if (importerParams.Contains("DMD") || elementName == "DMD")
						//		{
						//			obj = FPUtilsEditor.FindAndLoadAsset<GameObject>("BasicDMD", true, new[] { "Assets/PinAssets/DMD" });
						//			obj.name = elementName;
						//		}
						//		else
						//			obj = new GameObject(elementName);
						//		var fp = obj.AddComponent<FP_Overlay>();
						//		ChunksToFP(chunks, fp, false);
						//		FPUtils.Attach_GameObject(obj, fp.surface, !fp.render_onto_translite);
						//		if (importerParams.Contains("DMD") || elementName == "DMD")
						//			DecalBuilder.SetupDMD(fp, importerParams);
						//		else
						//			DecalBuilder.Generate(fp);
						//		break;
						//	}
						//case 29:///Bulb
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_29_ARRAY, validRawData);
						//		var bulb_obj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Lamps/BaseLampIO");

						//		FP_Bulb fpbulb = bulb_obj.AddComponent<FP_Bulb>();
						//		ChunksToFP(chunks, fpbulb, false);

						//		Color mainC = fpbulb.lit_color;
						//		mainC.a = fpbulb.crystal ? 0.3f : 0.6f;

						//		float parent_h = FPUtilsEditor.GetParentTopHeight(fpbulb.surface);
						//		var material = FPUtilsEditor.Set_Material(fpbulb.name + "_Bulb",
						//												fpbulb.crystal ? "BackReflTrans" : "TexBackTrans",// "BackRefl",
						//												mainC,
						//												"",//fpbulb.lens_texture,
						//												.8f,
						//												.8f,
						//												1.5f,
						//												/*3000 +*/ Mathf.RoundToInt(parent_h),
						//												bulb_obj.GetComponentInChildren<Renderer>()?.gameObject);

						//		Bulb.Setup(bulb_obj, fpbulb, importerParams, material);
						//	}
						//	break;
						//case 30: // Gate
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_30_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks);

						//		var fp = obj.AddComponent<FP_Gate>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			Color mainC = fp.color;
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													"TexRefl",
						//													mainC,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(obj, 0),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h), obj);
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material, false, false);

						//		// Move gate down to keep rotation axis up
						//		var mf = obj.GetComponentInChildren<MeshFilter>();
						//		if (mf != null && mf.sharedMesh != null)
						//		{
						//			var bnd = mf.sharedMesh.bounds;
						//			var lp = mf.transform.localPosition;
						//			lp.y -= bnd.size.y * 0.5F;
						//			mf.transform.localPosition = lp;
						//		}

						//		Rigidbody rb = obj.AddComponent<Rigidbody>();
						//		rb.isKinematic = false;
						//		rb.useGravity = true; rb.mass = 0.02f;
						//		rb.angularDrag = 10;

						//		HingeJoint joint = obj.AddComponent<HingeJoint>();
						//		joint.connectedBody = null;
						//		joint.axis = new Vector3(-1f, 0f, 0f);
						//		//joint.anchor=Vector3.zero;	//before LUDOSHAPES & good position
						//		//joint.anchor = new Vector3(0f, -modl.transform.localPosition.y, 0f);
						//		joint.anchor = new Vector3(0f, 0F, 0f);

						//		/*transform.Translate(new Vector3(0f,mf.mesh.bounds.extents.y/2f,0f));*///before good position ms3d
						//																				/////// added since ball mass was divided by 2

						//		joint.useLimits = true;
						//		JointLimits lim = new JointLimits();

						//		if (fp.one_way) { lim.max = 0f; lim.min = 130f; } else { lim.max = 130f; lim.min = -130f; }
						//		joint.limits = lim;

						//	}
						//	break;
						//case 33: // Toys
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_33_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks);

						//		var fp = obj.AddComponent<FP_Toy>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			Color mainC = fp.color;
						//			mainC.a = fp.transparency / 6F;
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													fp.transparency < 6 ? "TexBackReflTrans" : "TexBackRefl",
						//													mainC,
						//													fp.texture,
						//													0.8F,
						//													0.1F,
						//													0.5F,
						//													/*3000 +*/ Mathf.RoundToInt(parent_h), obj);
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material, false, false);
						//	}
						//	break;
						//case 43: // Diverter
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_43_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Diverters/BaseDiverterIO");

						//		var fp = obj.AddComponent<FP_Diverter>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			Color mainC = fp.color;
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													"TexRefl",
						//													mainC,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(obj, 0),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h), obj);
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.start_angle, fp.surface, material);

						//		Behaviours.Diverter diverter = obj.GetComponent<Behaviours.Diverter>();
						//		diverter.swingAngle = fp.swing;

						//		// sound (Seems to have a problem with fpdefs)
						//		if (!string.IsNullOrEmpty(fp.solenoid))
						//		{
						//			var clip = FPUtilsEditor.FindAudioClip(fp.solenoid);
						//			if (clip != null)
						//			{
						//				if (diverter.m_audioOn != null) diverter.m_audioOn.clip = clip;
						//				if (diverter.m_audioOff != null) diverter.m_audioOff.clip = clip;
						//			}
						//		}
						//	}
						//	break;
						//case 46: // Autoplunger
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_46_ARRAY, validRawData);
						//		string prefab = importerParams.Contains("NoRender") ? "Assets/PinAssets/_IO/Plungers/AutoPlungerIO" : "Assets/PinAssets/_IO/Plungers/FPAutoPlunger";
						//		GameObject obj = SetupModelBased(ref chunks, prefab);

						//		var fp = obj.AddComponent<FP_AutoPlunger>();
						//		ChunksToFP(chunks, fp, false);

						//		//Material material = null;
						//		//{
						//		//    Color mainC = fp.color;
						//		//    material = FPUtilsEditor.Set_Material(obj.name,
						//		//                                            "Tex",
						//		//                                            mainC,
						//		//                                            fp.texture,
						//		//                                            1,
						//		//                                            0, obj);
						//		//}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, "", null);

						//		Behaviours.AutoPlunger autoplunger = obj.GetComponent<Behaviours.AutoPlunger>();
						//		autoplunger.m_strength = fp.strength * 0.25F;
						//		// sound (Seems to have a problem with fpdefs)
						//		if (!string.IsNullOrEmpty(fp.solenoid))
						//		{
						//			var clip = FPUtilsEditor.FindAudioClip(fp.solenoid);
						//			if (clip != null)
						//				if (autoplunger.m_audioOn != null) autoplunger.m_audioOn.clip = clip;
						//		}
						//	}
						//	break;
						//case 50: // Popup
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_50_ARRAY, validRawData);
						//		GameObject obj = SetupModelBased(ref chunks, "Assets/PinAssets/_IO/Popups/BasePopupIO");

						//		var fp = obj.AddComponent<FP_Popup>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			Color mainC = fp.color;
						//			mainC.a = fp.crystal ? 0.3f : mainC.a;
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			material = FPUtilsEditor.Set_Material(obj.name,
						//													fp.crystal ? "TexBackReflTrans" : "TexBackRefl",
						//													mainC,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(obj, 0),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h), obj);
						//		}

						//		ModelBased.SetupStandardModelBasedObject(obj, fp.position, fp.rotation, fp.surface, material);
						//		Behaviours.Popup pop = obj.GetComponent<Behaviours.Popup>();
						//		pop.travel = obj.transform.localPosition.y * 2F; // As it is place by default up

						//		// sound
						//		if (!string.IsNullOrEmpty(fp.solenoid))
						//		{
						//			var clip = FPUtilsEditor.FindAudioClip(fp.solenoid);
						//			if (clip != null)
						//			{
						//				if (pop.m_audioOn != null) pop.m_audioOn.clip = clip;
						//				if (pop.m_audioOff != null) pop.m_audioOff.clip = clip;
						//			}
						//		}
						//	}
						//	break;
						//case 51: // ModelRamps
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_51_ARRAY, validRawData);
						//		var modelRamp = SetupModelBased(ref chunks);

						//		var fp = modelRamp.AddComponent<FP_RampModel>();
						//		ChunksToFP(chunks, fp, false);

						//		Material material = null;
						//		{
						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			string matType = "Tex";
						//			if (fp.transparency != 0 && fp.transparency < 6)
						//				matType += "Trans";
						//			if (fp.sphere_mapping != 0)
						//				matType += "Refl";
						//			if (fp.disable_culling != 0)
						//				matType += "Back";
						//			Color col = fp.color;
						//			col.a = fp.transparency == 0 ? 1f : fp.transparency / 6f;
						//			material = FPUtilsEditor.Set_Material(modelRamp.name,
						//													matType,
						//													col,
						//													fp.texture,
						//													FindMaterialTypeFromColliders(modelRamp, 0),
						//													/*3000 +*/ Mathf.RoundToInt(parent_h + fp.offset));
						//		}
						//		ModelBased.SetupStandardModelBasedObject(modelRamp, fp.position, fp.rotation, fp.surface, material, true, false, true);

						//	}
						//	break;

						//case 53: // WireRamps
						//		 //reverseAnanlyseRawData(datavalue, chunks, Descriptors.CHUNKS_ELEMENT_53_ARRAY, validRawData);
						//	analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_53_ARRAY, validRawData);
						//	WireRampBuilder wireRampBuilder = WireRampBuilder.Create(elementName, false);
						//	wireRampBuilder.gameObject = new GameObject(elementName);
						//	wireramp_builders.Add(wireRampBuilder);
						//	FP_WireRamp fpWireRamp = wireRampBuilder.gameObject.AddComponent<FP_WireRamp>();
						//	ChunksToFP(chunks, fpWireRamp, false);
						//	fpWireRamp.ramp_points = GetRampPoints(chunks, false);
						//	wireRampBuilder.Init(fpWireRamp);
						//	//FPUtilsEditor.SetWireGuideMaterial(wgb);
						//	break;

						//case 55: // Ramps
						//	analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_55_ARRAY, validRawData);
						//	RampBuilder rampBuilder = RampBuilder.Create(elementName, false);
						//	rampBuilder.gameObject = new GameObject(elementName);
						//	ramp_builders.Add(rampBuilder);
						//	FP_Ramp fpramp = rampBuilder.gameObject.AddComponent<FP_Ramp>();
						//	//reverseAnanlyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_55_ARRAY, validRawData);
						//	ChunksToFP(chunks, fpramp, false);
						//	fpramp.ramp_points = GetRampPoints(chunks, false);
						//	rampBuilder.Init(fpramp);
						//	//FPUtilsEditor.SetWireGuideMaterial(wgb);
						//	break;

						//case 61: // Opto triggers
						//	{
						//		analyseRawData(chunks, Descriptors.CHUNKS_ELEMENT_61_ARRAY, validRawData);
						//		GameObject obj = FPUtilsEditor.FindAndLoadAsset<GameObject>("BaseOptoTrigger", true, new[] { "Assets/PinAssets/_IO/Switches" });

						//		OptoTrigger opto = obj?.GetComponent<OptoTrigger>();
						//		if (obj != null && opto != null)
						//		{
						//			var fp = obj.AddComponent<FP_TriggerOpto>();
						//			ChunksToFP(chunks, fp, false);
						//			obj.name = elementName;
						//			ModelBased.SetScaleAndRotation(obj, fp.rotation, false);
						//			FPUtils.Attach_GameObject(obj, fp.surface, true);
						//			ModelBased.SetPosition(obj, fp.position);//, false, false, null, true);

						//			var emitter = FPUtilsEditor.FindAndLoadAssetFromStandardPaths<GameObject>(fp.model, true, "Models");
						//			var collector = FPUtilsEditor.FindAndLoadAssetFromStandardPaths<GameObject>(fp.model, true, "Models");

						//			float parent_h = FPUtilsEditor.GetParentTopHeight(fp.surface);
						//			float offset = 0F;
						//			if (emitter != null && opto.emitter != null)
						//			{
						//				var mf = emitter.GetComponentInChildren<MeshFilter>();
						//				var bnd = mf.sharedMesh.bounds;
						//				offset = bnd.size.y * 0.5F;

						//				emitter.transform.localScale = new Vector3(Globals.g_Scale, Globals.g_Scale, Globals.g_Scale);
						//				emitter.transform.parent = opto.emitter;
						//				emitter.transform.localPosition = Vector3.zero;
						//				emitter.transform.localRotation = Quaternion.identity;
						//				Material material = FPUtilsEditor.Set_Material(obj.name + "_emitter", "TexReflBack", fp.color, fp.texture_emitter, 1, Mathf.RoundToInt(parent_h), emitter);
						//				ModelBased.SetupRendererMaterial(emitter, true, true, material);
						//			}
						//			if (collector != null && opto.collector != null)
						//			{
						//				collector.transform.localScale = new Vector3(Globals.g_Scale, Globals.g_Scale, Globals.g_Scale);
						//				collector.transform.parent = opto.collector;
						//				collector.transform.localPosition = Vector3.zero;
						//				collector.transform.localRotation = Quaternion.identity;
						//				Material material = FPUtilsEditor.Set_Material(obj.name + "_collector", "TexReflBack", fp.color, fp.texture_collector, 1, Mathf.RoundToInt(parent_h), collector);
						//				ModelBased.SetupRendererMaterial(collector, true, true, material);
						//			}
						//			opto.beamLength = fp.beam_width * Globals.g_Scale;

						//			if (fp.invert)
						//				obj.transform.Rotate(new Vector3(180F, 0F, 0F), Space.Self);
						//			obj.transform.Translate(Vector3.up * offset * Globals.g_Scale * (fp.invert ? -1F : 1F), Space.Self);
						//		}
						//	}
						//	break;
					}
				}
			}

		}
	}
}
