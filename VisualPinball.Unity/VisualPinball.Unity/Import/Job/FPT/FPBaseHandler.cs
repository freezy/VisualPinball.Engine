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

using System;
using System.IO;
using System.Collections.Generic;
using VisualPinball.Unity.FP.defs;
using OpenMcdf;
using VisualPinball.Engine.Math;
using NLog;

namespace VisualPinball.Unity.FP
{
	public static class Globals
	{
		public static float g_Scale = 0.001F;
	}
    public static class FPBaseHandler
    {
		private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

		public static bool debug = false;

        public static ChunkGeneric getChunkImpl(ChunkGeneric chunk, List<string> pathList)
        {
            if (pathList.Count > 1 && chunk.descriptor.type == T_CHUNK_TYPE.T_CHUNK_CHUNKLIST && pathList[0] == chunk.descriptor.label)
            {
                ChunkChunkList chunkList = (ChunkChunkList)chunk;
                for (int i = 0; i < chunkList._value.Count; i++)
                {
                    if (chunkList._value[i].descriptor.label == pathList.ToArray()[1])
                    {
                        pathList.RemoveAt(0);// .erase(pathList.begin());
                        return getChunkImpl(chunkList._value[i], pathList);
                    }
                }
                return null;
            }
            else if (pathList.Count == 1 && pathList.ToArray()[0] == chunk.descriptor.label)
            {
                return chunk;
            }
            else
            {
                return null;
            }
        }

        public static ChunkGeneric getChunkByLabel(ChunkChunkList parentList, string label)
        {
            for (int i = 0; i < parentList._value.Count; i++)
            {
                // Logger.Debug("label:" + label);
                ChunkGeneric current = parentList._value[i];
                //Logger.Debug("current:" + current.descriptor.label);
                if (current.descriptor.label == label)
                {
                    return current;
                }
            }
            return null;
        }
        public static ChunkChunkList getChunksByLabel(ChunkChunkList parentList, string label)
        {
            ChunkChunkList chunks = new ChunkChunkList();
            for (int i = 0; i < parentList._value.Count; i++)
            {
                ChunkGeneric current = parentList._value[i];
                //Logger.Debug("current:" + current.descriptor.label);
                if (current.descriptor.label == label)
                {
                    chunks._value.Add(current);
                }
            }
            return chunks;
        }
        public static RawData getRawData(byte[] data, uint len, bool uncompress)
        {
            if (uncompress && Convert.ToChar(data[0]) == 'z' && Convert.ToChar(data[1]) == 'L' && Convert.ToChar(data[2]) == 'Z' && Convert.ToChar(data[3]) == 'O')
            {
                RawData _value = new RawData();
                _value.len = BitConverter.ToUInt32(data, 4);
                _value.data = new byte[_value.len];
                byte[] datain = new byte[(int)len - 8];
                Array.Copy(data, 8, datain, 0, (int)len - 8);
                try
                {
                    ManagedLZO.MiniLZO.Decompress(datain, _value.data);
                }
                catch
                {
                    Logger.Error("ERROR <<  internal error - decompression failed : ");
                }
                return _value;

            }
            else
            {
                return new RawData(len, data);
            }

        }

        public static bool writeRawDataChunk(string path, ChunkRawData dataChunk)
        {
            bool needed = false;
            RawData data = getRawData(dataChunk._value.data, dataChunk._value.len, true);
            if (System.IO.File.Exists(path))
            {
                if (new FileInfo(path).Length != data.data.Length)
                    needed = true;
            }
            else needed = true;
            if (needed)
            {
                File.WriteAllBytes(path, data.data);
                //AssetDatabase.Refresh ();
            }
            return needed;
        }

		/*
        ops::RawData * FPBaseHandler::compress(ops::RawData * data) {
            ops::RawData * result = new ops::RawData();
            // TODO : free not used memory ?
            result->data = new uint8_t[data->len * 2];
            result->len = 0;
            int r = lzo1x_1_compress(data->data, data->len, result->data, (lzo_uint*)&result->len, wrkmem);
            if (r == LZO_E_OK)
            {
                return result;
            } else {
                return NULL;
            }
        }



         */
		public static ChunkDescriptor findChunkDescriptor(ChunkDescriptor[] chunkList, uint chunk)
        {
            for (uint i = 0; i < chunkList.Length; i++)
            {
                if (chunk == chunkList[i].chunk)
                {
                    return chunkList[i];
                }
            }
            //Logger.Debug("findChunkDescriptor returned null"+chunk);
            return null;
        }
        /*

    const std::string * FPBaseHandler::findValue(const List<VLDescriptor> & valueList, int32_t code) {
        for (uint32_t i=0; i<valueList.size(); i++) {
            if (code == valueList[i].code) {
                return &valueList[i].label;
            }
        }
        return new std::string("unknown");
    }

         */


        /*

    ops::fp::ChunkGeneric * FPBaseHandler::getChunkByLabel(ops::fp::ChunkChunkList * parentList, std::string label)
    {
        for (uint32_t i=0; i<parentList->value.size(); i++) {
            ops::fp::ChunkGeneric * current = parentList->value[i];
            if (current->descriptor.label == label)
            {
                return current;
            }
        }
        return NULL;
    }
    ops::fp::ChunkChunkList * FPBaseHandler::getChunksByLabel(ops::fp::ChunkChunkList * parentList, std::string label)
    {
        ops::fp::ChunkChunkList * chunks = new ops::fp::ChunkChunkList();
        for (uint32_t i=0; i<parentList->value.size(); i++) {
            ops::fp::ChunkGeneric * current = parentList->value[i];
            if (current->descriptor.label == label)
            {
                chunks->add(current);
            }
        }
        return chunks;
    }


    ChunkGeneric * FPBaseHandler::getChunk(ChunkGeneric * baseChunk, std::string path) {
        return getChunkImpl(baseChunk, ops::tools::split(path, '.'));
    }


         */
        public static ChunkGeneric analyseChunk(ChunkChunkList result, ChunkDescriptor[] chunkList, uint chunk, uint originalChunk, uint sectionLen, byte[] data, uint retry)
        {
            ChunkChunkList r = result;
            return analyseChunk(ref r, chunkList, chunk, originalChunk, sectionLen, data, retry);
        }
        public static ChunkGeneric analyseChunk(ref ChunkChunkList result, ChunkDescriptor[] chunkList, uint chunk, uint originalChunk, uint sectionLen, byte[] data, uint retry, bool debug = false)
        {
            ChunkGeneric chunkBlock = null;
            // Search for known chunk
            ChunkDescriptor descriptor = findChunkDescriptor(chunkList, chunk);

            if (descriptor != null)
            {
                if (debug) Logger.Debug("analyse chunk: " + descriptor.type.ToString() + " => " + descriptor.label + " " + descriptor.chunk + " Orig:" + chunk);
                switch (descriptor.type)
                {
                    case T_CHUNK_TYPE.T_CHUNK_CHUNKLIST:
                        {
                            List<ChunkGeneric> initValue = new List<ChunkGeneric>();
                            ChunkChunkList newChunkList = new ChunkChunkList(sectionLen, originalChunk, descriptor, initValue);
                            chunkBlock = (ChunkGeneric)newChunkList;
                            newChunkList.parent = result;
                            result._value.Add(chunkBlock);//)->add(chunkBlock);
                            result = newChunkList;
                            return chunkBlock;
                        }
                    case T_CHUNK_TYPE.T_CHUNK_GENERIC:
                        {
                            chunkBlock = (ChunkGeneric)new ChunkGeneric(sectionLen, originalChunk, descriptor);
                            if (descriptor.label == "end")
                            {
                                result._value.Add(chunkBlock);// (*result)->add(chunkBlock);
                                if (result.parent != null)
                                {
                                    result = result.parent;
                                }
                                return chunkBlock;
                            }
                        }
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_INT:
                        chunkBlock = (ChunkGeneric)new ChunkInt(sectionLen, originalChunk, descriptor, BitConverter.ToInt32(data, 0));
                        if (debug) Logger.Debug(descriptor.type.ToString() + " => " + descriptor.label + "=" + ((ChunkInt)chunkBlock)._value);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_COLOR:
                        chunkBlock = (ChunkGeneric)new ChunkColor(sectionLen, originalChunk, descriptor, BitConverter.ToInt32(data, 0));
                        if (debug) Logger.Debug(descriptor.type.ToString() + " => " + descriptor.label + "=" + ((ChunkColor)chunkBlock)._value);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_VALUELIST:
                        chunkBlock = (ChunkGeneric)new ChunkValueList(sectionLen, originalChunk, descriptor, BitConverter.ToInt32(data, 0));
                        //if(debug)Logger.Warn("TODO T_CHUNK_VALUELIST");
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_FLOAT:
                        chunkBlock = (ChunkGeneric)new ChunkFloat(sectionLen, originalChunk, descriptor, BitConverter.ToSingle(data, 0));
                        if (debug) Logger.Debug(descriptor.type.ToString() + " => " + descriptor.label + "=" + ((ChunkFloat)chunkBlock)._value);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_VECTOR2D:

                        chunkBlock = (ChunkGeneric)new ChunkVector2D(sectionLen, originalChunk, descriptor,
                                        new Vertex2D(BitConverter.ToSingle(data, 0), BitConverter.ToSingle(data, 4)));
                        //Logger.Debug(descriptor.type.ToString()+" => "+descriptor.label+"="+((ChunkVector2D)chunkBlock)._value);
                        //	if(debug)
                        //Logger.Warn("TODO T_CHUNK_VECTOR2D");
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_STRING:
                        //chunkBlock = (ChunkGeneric *)new ChunkString(sectionLen, originalChunk, *descriptor, std::string((const char*)(data+4), *((uint32_t*)data)) );
                        chunkBlock = (ChunkGeneric)new ChunkString(sectionLen, originalChunk, descriptor, ConvertHex(BitConverter.ToString(data, 4).Replace("-", "")));
                        if (debug) Logger.Debug(descriptor.type.ToString() + " => " + descriptor.label + "=" + ((ChunkString)chunkBlock)._value);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_WSTRING:
                        {
                            uint dataLen = ((uint)data.Length);
                            byte[] newdata = new byte[dataLen - 4];
                            Array.Copy(data, 4, newdata, 0, dataLen - 4);
                            string datavalue = System.Text.Encoding.Unicode.GetString(newdata);

                            chunkBlock = (ChunkGeneric)new ChunkWString(sectionLen, originalChunk, descriptor, datavalue);//ConvertHex(BitConverter.ToString(data,4).Replace("-","")));
                            if (debug) Logger.Debug(descriptor.type.ToString() + " => " + descriptor.label + "=" + ((ChunkWString)chunkBlock)._value);
                            break;
                        }

                    case T_CHUNK_TYPE.T_CHUNK_STRINGLIST:
                        /*{
                        uint32_t nbItems = *((uint32_t *)data);
                        List<string> items;
                        uint offset = 4;
                        for (uint i=0; i< nbItems; i++) {
                            uint strLen = ((uint)(data+offset));
                            items.push_back(std::string((const char*)(data+offset+4), strLen) ) ;
                            offset+= 4 + strLen;
                        }
                        chunkBlock = (ChunkGeneric *)new ChunkStringList(sectionLen, originalChunk, *descriptor, items );
                        chunkBlock->len = offset;
                        }*/
                        {
                            uint nbItems = BitConverter.ToUInt32(data, 0);
                            List<string> items = new List<string>();
                            int os = 4;
                            for (uint i = 0; i < nbItems; i++)
                            {
                                uint strLen = BitConverter.ToUInt32(data, os);
                                string s = ConvertHex(BitConverter.ToString(data, os + 4, (int)strLen).Replace("-", "").Trim());
                                items.Add(s);
                                os += 4 + (int)strLen;
                            }
                            chunkBlock = (ChunkGeneric)new ChunkStringList(sectionLen, originalChunk, descriptor, items);
                            chunkBlock.len = (uint)(os);
                            if (debug)
                                Logger.Debug(descriptor.type.ToString() + " => " + descriptor.label + "=" + ((ChunkStringList)chunkBlock)._items);
                        }
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_RAWDATA:
                    case T_CHUNK_TYPE.T_CHUNK_COLLISIONDATA:
                        {
                            //Logger.Debug("TODO T_CHUNK_COLLISIONDATA");
                            RawData rd = getRawData(data, sectionLen, false);
                            chunkBlock = (ChunkGeneric)new ChunkRawData(sectionLen, originalChunk, descriptor, rd);

                            rd = null;//delete rd;*/
                            break;
                        }
                    case T_CHUNK_TYPE.T_CHUNK_SCRIPT:
                        {
                            //if(debug)Logger.Warn("TODO T_CHUNK_SCRIPT");
                            /*int len = *(int *) data + 4; // Anomalie
                            ops::RawData *rd = getRawData(data, len, false);
                                   chunkBlock = (ChunkGeneric *)new ChunkScript(len, originalChunk, *descriptor, *rd);
                            delete rd;*/
                            int len = data.Length;//+4;
                            RawData rd = getRawData(data, (uint)len, false);


                            chunkBlock = (ChunkGeneric)new ChunkScript((uint)len, originalChunk, descriptor, rd);
                            break;
                        }
                    default:
                        {
                            if (debug) Logger.Debug("TODO default");
                            RawData rd = getRawData(data, sectionLen, false);
                            chunkBlock = (ChunkGeneric)new ChunkRawData(sectionLen, originalChunk, descriptor, rd);
                            rd = null;//delete rd;
                            break;
                        }
                }
            }
            if (chunkBlock == null)
            {
                // Unkown chunk, try to check for older FP chunk:
                if (retry == 0)
                {
                    // diff [17,18,19] <-> 12
                    //Logger.Debug("re-trying chunk: "+chunk);
                    return analyseChunk(result, chunkList, chunk - 0x15BDECDB, chunk, sectionLen, data, retry + 1);
                }
                else
                {
                    // Unknown section
                    //		WARNING << "unkown chunk : " << "0x" << std::setw(8) << std::setfill('0') << std::right << std::hex << std::uppercase << chunk << " ["  << "0x" << originalChunk << "]";
                    // if(debug) Logger.Warn("unknown chunk"+chunk);
                    RawData rd = getRawData(data, sectionLen, false);
                    chunkBlock = (ChunkGeneric)new ChunkRawData(sectionLen, originalChunk, ChunkTypes.CHUNK_UNKNOWN, rd);
                    rd = null;// delete rd;
                }
            }

            result._value.Add(chunkBlock);// (*result)->add(chunkBlock);
            return /*result;//*/ chunkBlock;
        }




        public static void reverseAnanlyseRawData(int datatype, ChunkChunkList result, ChunkDescriptor[] chunkList, RawData rawData)
        {
            string dbg = "ReverseAnalysing data type:" + datatype + "\n";

            ChunkChunkList currentChunks = result;
            uint offset = 0;
            while (offset < rawData.len)
            {
                uint sectionLen = BitConverter.ToUInt32(rawData.data, (int)offset);
                offset += 4;
                uint sectionChunk = BitConverter.ToUInt32(rawData.data, (int)offset);
                offset += 4;
                sectionLen -= 4;
                // Logger.Debug("sectionLen: "+sectionLen);
                try
                {
                    byte[] data = new byte[sectionLen];
                    Array.Copy(rawData.data, offset, data, 0, sectionLen);


                    ChunkDescriptor descriptor = findChunkDescriptor(chunkList, sectionChunk);
                    if (descriptor != null)
                        dbg += ("analyse chunk: " + descriptor.type.ToString() + " => " + descriptor.label + " 0x" + descriptor.chunk.ToString("X") + " Orig: 0x" + sectionChunk.ToString("X")) + "\n";
                    else
                        dbg += ("unknow chunk: 0x" + sectionChunk.ToString("X")) + "\n";


                    ChunkGeneric chunkBlock = analyseChunk(ref currentChunks, chunkList, sectionChunk, sectionChunk, sectionLen, data, 0, debug);
                    // Get calculated len because of light/image list anomaly ...
                    offset += chunkBlock.len < sectionLen ? sectionLen : chunkBlock.len;

                    if (sectionChunk != chunkBlock.descriptor.chunk)
                        dbg += ("sectionChunk: 0x" + sectionChunk.ToString("X") + " Second try:: 0x" + chunkBlock.descriptor.chunk.ToString("X") + " -> " + chunkBlock.descriptor.label + " -> " + chunkBlock.descriptor.type.ToString() + " -> " + sectionLen + " -> " + chunkBlock.len) + "\n";
                }
                catch (OverflowException e) { Logger.Warn("overflow exception: " + currentChunks.descriptor.type.ToString() + " -> " + currentChunks.descriptor.label + " : " + e.Message); }
            }
            Logger.Debug("<color=orange>"+dbg+"</color>");

            StreamWriter writer = new StreamWriter("ReverseAnalseItem.txt", true);
            writer.WriteLine(dbg);
            writer.Close();
        }


        public static void analyseRawData(ChunkChunkList result, ChunkDescriptor[] chunkList, RawData rawData, bool debug = false)
        {
            ChunkChunkList currentChunks = result;
            uint offset = 0;
            while (offset < rawData.len)
            {
                uint sectionLen = BitConverter.ToUInt32(rawData.data, (int)offset);
                offset += 4;
                uint sectionChunk = BitConverter.ToUInt32(rawData.data, (int)offset);
                offset += 4;
                sectionLen -= 4;
                // Logger.Debug("sectionLen: "+sectionLen);
                try
                {
                    uint tocopy = sectionLen;
                    // Patch for lights/images lists bug fix
                    if (chunkList == Descriptors.CHUNKS_LIST_ARRAY && sectionChunk == 0xBEABBEBC)
                        tocopy = rawData.len - offset;

                    byte[] data = new byte[tocopy];
                    Array.Copy(rawData.data, offset, data, 0, tocopy);
                    ChunkGeneric chunkBlock = analyseChunk(ref currentChunks, chunkList, sectionChunk, sectionChunk, sectionLen, data, 0, debug);
                    // Get calculated len because of light/image list anomaly ...
                    offset += chunkBlock.len < sectionLen ? sectionLen : chunkBlock.len;
                    if (debug)
                        Logger.Debug("sectionChunk: 0x" + sectionChunk.ToString("X") + " wrong:: 0x" + chunkBlock.descriptor.chunk.ToString("X") + " -> " + chunkBlock.descriptor.label + " -> " + chunkBlock.descriptor.type.ToString() + " -> " + sectionLen + " -> " + chunkBlock.len);
                }
                catch (OverflowException e) { Logger.Warn("overflow exception: " + currentChunks.descriptor.type.ToString() + " -> " + currentChunks.descriptor.label + " : " + e.Message); }
            }
        }
		
        public static string ConvertHex(String hexString)
        {
            string ascii = string.Empty;

            for (int i = 0; i < hexString.Length; i += 2)
            {
                String hs = string.Empty;

                hs = hexString.Substring(i, 2);
                uint decval = System.Convert.ToUInt32(hs, 16);
                char character = System.Convert.ToChar(decval);
                ascii += character;

            }

            return ascii;
        }


        public static void ChunksToFP(ChunkChunkList chunks, object obj, bool debug)
        {
            //analyseRawData(chunks, Descriptors.CHUNKS_TABLE_ARRAY, rawData);
            foreach (ChunkGeneric chunk in chunks._value)
            {
                string field = chunk.descriptor.label;
                switch (chunk.descriptor.type)
                {
                    case T_CHUNK_TYPE.T_CHUNK_INT:
                        if (obj.GetType().GetField(field) != null)
                        {
                            if (obj.GetType().GetField(field).FieldType == typeof(int))
                                obj.GetType().GetField(field).SetValue(obj, ((ChunkInt)chunk)._value);
                            else
                                if (obj.GetType().GetField(field).FieldType == typeof(bool))
                                obj.GetType().GetField(field).SetValue(obj, ((ChunkInt)chunk)._value == 1);
                        }
                        else
                            if (debug) Logger.Debug(" (" + obj.GetType().ToString() + ") field not found=> " + field);

                        break;
                    case T_CHUNK_TYPE.T_CHUNK_FLOAT:
                        if (obj.GetType().GetField(field) != null)
                            obj.GetType().GetField(field).SetValue(obj, ((ChunkFloat)chunk)._value);
                        else
                            if (debug) Logger.Debug(" (" + obj.GetType().ToString() + ") field not found=> " + field);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_STRING:
                        if (obj.GetType().GetField(field) != null)
                            obj.GetType().GetField(field).SetValue(obj, ((ChunkString)chunk)._value);
                        else
                            if (debug) Logger.Debug(" (" + obj.GetType().ToString() + ") field not found=> " + field);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_WSTRING:
                        if (obj.GetType().GetField(field) != null)
                            obj.GetType().GetField(field).SetValue(obj, ((ChunkWString)chunk)._value);
                        else
                            if (debug) Logger.Debug(" (" + obj.GetType().ToString() + ") field not found=> " + field);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_COLOR:
                        if (obj.GetType().GetField(field) != null)
                            obj.GetType().GetField(field).SetValue(obj, ((ChunkColor)chunk)._value);
                        else
                            if (debug) Logger.Debug(" (" + obj.GetType().ToString() + ") field not found=> " + field);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_VECTOR2D:
                        if (obj.GetType().GetField(field) != null)
                            obj.GetType().GetField(field).SetValue(obj, ((ChunkVector2D)chunk)._value);
                        else
                            if (debug) Logger.Debug(" (" + obj.GetType().ToString() + ") field not found=> " + field);
                        break;
                    case T_CHUNK_TYPE.T_CHUNK_VALUELIST:
                        if (obj.GetType().GetField(field) != null)
                            obj.GetType().GetField(field).SetValue(obj, ((ChunkValueList)chunk)._value);
                        else
                            if (debug) Logger.Debug(" (" + obj.GetType().ToString() + ") field not found=> " + field);
                        break;
                    default:
                        if (debug) Logger.Debug(" (" + obj.GetType().ToString() + ") field " + chunk.descriptor.type.ToString() + " not found=> " + field);
                        break;
                }
            }
        }

        public static List<FPShapePoint> GetShapePoints(ChunkChunkList chunks, bool debug = false)
        {
            List<FPShapePoint> points = new List<FPShapePoint>();
            ChunkChunkList positions = (ChunkChunkList)getChunksByLabel(chunks, "position");
            for (int c = 0; c < positions._value.Count; c++)
            {
                ChunkVector2D pt = (ChunkVector2D)positions._value[c];
                points.Add(new FPShapePoint());
                points[c].position = new Vertex3D(pt._value.X, pt._value.Y, 0F);
            }
            ChunkChunkList smooths = (ChunkChunkList)getChunksByLabel(chunks, "smooth");
            for (int c = 0; c < smooths._value.Count; c++)
            {
                ChunkInt smooth = (ChunkInt)smooths._value[c];
                points[c].smooth = smooth._value == 0 ? false : true;
            }
            ChunkChunkList autos = (ChunkChunkList)getChunksByLabel(chunks, "automatic_texture_coordinate");
            for (int c = 0; c < autos._value.Count; c++)
            {
                ChunkInt auto = (ChunkInt)autos._value[c];
                points[c].automatic_texture_coordinate = auto._value == 0 ? false : true;
            }
            ChunkChunkList tcoords = (ChunkChunkList)getChunksByLabel(chunks, "texture_coordinate");
            for (int c = 0; c < tcoords._value.Count; c++)
            {
                ChunkFloat tcoord = (ChunkFloat)tcoords._value[c];
                points[c].texture_coordinate = tcoord._value;
            }
            return points;
        }

        public static List<FPShapePoint> GetShapeableRubberPoints(ChunkChunkList chunks, ref bool hasSlingshot, ref bool hasLeaf, bool debug = false)
        {
            List<FPShapePoint> points = new List<FPShapePoint>();
            ChunkChunkList positions = (ChunkChunkList)getChunksByLabel(chunks, "position");
            for (int c = 0; c < positions._value.Count; c++)
            {
                ChunkVector2D pt = (ChunkVector2D)positions._value[c];
                points.Add(new FPShapePoint());
                points[c].position = new Vertex3D(pt._value.X * Globals.g_Scale, pt._value.Y * Globals.g_Scale, 0F);
            }
            ChunkChunkList smooths = (ChunkChunkList)getChunksByLabel(chunks, "smooth");
            for (int c = 0; c < smooths._value.Count; c++)
            {
                ChunkInt smooth = (ChunkInt)smooths._value[c];
                points[c].smooth = smooth._value == 0 ? false : true;
            }
            ChunkChunkList autos = (ChunkChunkList)getChunksByLabel(chunks, "slingshot");
            for (int c = 0; c < autos._value.Count; c++)
            {
                ChunkInt auto = (ChunkInt)autos._value[c];
                points[c].slingshot = auto._value;
                if (points[c].slingshot > 0)
                {
                    hasSlingshot = true;
                    hasLeaf = true;
                }
            }
            ChunkChunkList sl = (ChunkChunkList)getChunksByLabel(chunks, "single_leaf");
            for (int c = 0; c < sl._value.Count; c++)
            {
                ChunkInt auto = (ChunkInt)sl._value[c];
                points[c].single_leaf = auto._value;
                if (points[c].single_leaf > 0)
                    hasLeaf = true;
            }

            ChunkChunkList ei = (ChunkChunkList)getChunksByLabel(chunks, "event_id");
            for (int c = 0; c < ei._value.Count; c++)
            {
                ChunkInt auto = (ChunkInt)ei._value[c];
                points[c].event_id = auto._value;
            }
            

            return points;
        }

        public static List<FPShapePoint> GetRampPoints(ChunkChunkList chunks, bool debug = false)
        {
            List<FPShapePoint> points = new List<FPShapePoint>();

            ChunkChunkList rampPoints = (ChunkChunkList)getChunksByLabel(chunks, "ramp_point");
            for (int rpi = 0; rpi < rampPoints._value.Count; rpi++)
            {
                var rp = (ChunkChunkList)rampPoints._value[rpi];
                var pt = getChunkByLabel(rp, "position") as ChunkVector2D;
                if (pt == null)
                    continue;

                FPShapePoint fp = new FPShapePoint();
                fp.position = new Vertex3D(pt._value.X * Globals.g_Scale, pt._value.Y * Globals.g_Scale, 0F);
                {
                    var ch = getChunkByLabel(rp, "smooth") as ChunkInt;
                    if (ch != null)
                        fp.smooth = ch._value == 0 ? false : true;
                }
                {
                    var ch = getChunkByLabel(rp, "left_guide") as ChunkInt;
                    if (ch != null)
                        fp.left_guide = ch._value;
                }
                {
                    var ch = getChunkByLabel(rp, "left_upper_guide") as ChunkInt;
                    if (ch != null)
                        fp.left_upper_guide = ch._value;
                }
                {
                    var ch = getChunkByLabel(rp, "right_guide") as ChunkInt;
                    if (ch != null)
                        fp.right_guide = ch._value;
                }
                {
                    var ch = getChunkByLabel(rp, "right_upper_guide") as ChunkInt;
                    if (ch != null)
                        fp.right_upper_guide = ch._value;
                }
                {
                    var ch = getChunkByLabel(rp, "top_wire") as ChunkInt;
                    if (ch != null)
                        fp.top_wire = ch._value;
                }
                {
                    var ch = getChunkByLabel(rp, "ring_type") as ChunkInt;
                    if (ch != null)
                        fp.ring_type = ch._value;
                }


                points.Add(fp);
            }
            return points;
        }

        /// <summary>
        /// Special version of the GetShapePoints function  for shapelamps
        /// Normally we take "position" chunks to create our points, but shapedlamps define "texture_position"
        /// chunks instead
        /// </summary>
        /// <param name="chunks"></param>
        /// <returns></returns>
        public static List<FPShapePoint> GetShapePointsLight(ChunkChunkList chunks)
        {
            List<FPShapePoint> points = new List<FPShapePoint>();
            ChunkChunkList positions = (ChunkChunkList)getChunksByLabel(chunks, "texture_position");
            for (int c = 1; c < positions._value.Count; c++)
            {
                ChunkVector2D pt = (ChunkVector2D)positions._value[c];
                points.Add(new FPShapePoint());
                points[c - 1].position = new Vertex3D(pt._value.X * Globals.g_Scale, pt._value.Y * Globals.g_Scale, 0F);
            }
            ChunkChunkList smooths = (ChunkChunkList)getChunksByLabel(chunks, "smooth");
            for (int c = 0; c < smooths._value.Count; c++)
            {
                ChunkInt smooth = (ChunkInt)smooths._value[c];
                points[c].smooth = smooth._value == 0 ? false : true;
            }
            ChunkChunkList autos = (ChunkChunkList)getChunksByLabel(chunks, "automatic_texture_coordinate");
            for (int c = 0; c < autos._value.Count; c++)
            {
                ChunkInt auto = (ChunkInt)smooths._value[c];
                points[c].automatic_texture_coordinate = auto._value == 0 ? false : true;
            }
            ChunkChunkList tcoords = (ChunkChunkList)getChunksByLabel(chunks, "texture_coordinate");
            for (int c = 0; c < autos._value.Count; c++)
            {
                ChunkFloat tcoord = (ChunkFloat)tcoords._value[c];
                points[c].texture_coordinate = tcoord._value;
            }
            return points;
        }

        public static FP_TableData GetTableData(CFStream stream)
        {
            FP_TableData td = new FP_TableData();
            RawData rawData = new RawData(stream);
            ChunkChunkList chunks = new ChunkChunkList(ChunkTypes.CHUNK_TABLE_DATA);
            analyseRawData(chunks, Descriptors.CHUNKS_TABLE_ARRAY, rawData);
            ChunksToFP(chunks, td, false);
            return td;
        }
    }
}
