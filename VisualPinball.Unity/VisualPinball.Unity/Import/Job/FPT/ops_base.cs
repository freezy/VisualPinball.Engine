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
using System.Collections;
using System.Collections.Generic;
using OpenMcdf;
using VisualPinball.Unity.FP.defs;
using VisualPinball.Engine.Math;


namespace VisualPinball.Unity.FP
{
	public static class ChunkTypes
	{
		public static List<VLDescriptor> VL_EMPTY = new List<VLDescriptor> ();
		
		public static ChunkDescriptor CHUNK_CHUNKLIST_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "empty", -1);
		
		public static ChunkDescriptor CHUNK_GENERIC_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_GENERIC, "empty", -1);
		public static ChunkDescriptor CHUNK_PINMODEL = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "pinmodel", -1);
		public static ChunkDescriptor CHUNK_PINMODEL_RAW = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_RAWDATA, "pinmodel_raw", -1);
		public static ChunkDescriptor CHUNK_TABLE = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "table", -1);
			
		public static ChunkDescriptor CHUNK_LIBRARY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "library", -1);
		public static ChunkDescriptor CHUNK_RESOURCE = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "resource", -1);
		public static ChunkDescriptor CHUNK_RESOURCE_NAME = new ChunkDescriptor (0xA4F1B9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "name", -1);
		public static ChunkDescriptor CHUNK_RESOURCE_TYPE = new ChunkDescriptor (0xA4F1B9D1, T_CHUNK_TYPE.T_CHUNK_INT, "type", -1);
		public static ChunkDescriptor CHUNK_RESOURCE_PATH = new ChunkDescriptor (0xA4F1B9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "path", -1);
		public static ChunkDescriptor CHUNK_RESOURCE_FLAD = new ChunkDescriptor (0xA4F1B9D1, T_CHUNK_TYPE.T_CHUNK_RAWDATA, "flad", -1);
		public static ChunkDescriptor CHUNK_RESOURCE_DATA = new ChunkDescriptor (0xA4F1B9D1, T_CHUNK_TYPE.T_CHUNK_RAWDATA, "data", -1);
			
		public static ChunkDescriptor CHUNK_TABLE_DATA = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "table_data", -1);
		public static ChunkDescriptor CHUNK_TABLE_ELEMENT = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "table_element", -1);
			
		public static ChunkDescriptor CHUNK_IMAGE = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "image", -1);
		public static ChunkDescriptor CHUNK_SOUND = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "sound", -1);
		public static ChunkDescriptor CHUNK_MUSIC = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "music", -1);
		public static ChunkDescriptor CHUNK_DMDFONT = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "dmdfont", -1);
		public static ChunkDescriptor CHUNK_IMAGELIST = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "imagelist", -1);
		public static ChunkDescriptor CHUNK_LIGHTLIST = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "lightlist", -1);
		public static ChunkDescriptor CHUNK_TABLE_MAC = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "table_mac", -1);
		public static ChunkDescriptor CHUNK_FILE_VERSION = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "file_version", -1);
			
		public static ChunkDescriptor CHUNK_TABLE_MAC_DATA = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_RAWDATA, "table_mac_data", -1);
		public static ChunkDescriptor CHUNK_FILE_VERSION_DATA = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_RAWDATA, "file_version_data", -1);
		public static ChunkDescriptor CHUNK_ELEMENT_TYPE = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_INT, "element_type", -1);
		public static ChunkDescriptor CHUNK_UNKNOWN = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_RAWDATA, "unknown", -1);
		public static ChunkDescriptor CHUNK_UNKNOWN_TABLE_ELEMENT = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_RAWDATA, "unknown_table_element", -1);
		
		public static ChunkDescriptor CHUNK_RAWDATA_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_RAWDATA, "empty", -1);
		public static ChunkDescriptor CHUNK_INT_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_INT, "empty", -1);
		public static ChunkDescriptor CHUNK_FLOAT_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_FLOAT, "empty", -1);
		public static ChunkDescriptor CHUNK_COLOR_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_COLOR, "empty", -1);
		public static ChunkDescriptor CHUNK_VECTOR2D_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_VECTOR2D, "empty", -1);
		public static ChunkDescriptor CHUNK_STRING_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_STRING, "empty", -1);
		public static ChunkDescriptor CHUNK_WSTRING_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_WSTRING, "empty", -1);
		public static ChunkDescriptor CHUNK_STRINGLIST_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_STRINGLIST, "empty", -1);
		public static ChunkDescriptor CHUNK_VALUELIST_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_VALUELIST, "empty", -1, VL_EMPTY);
		public static ChunkDescriptor CHUNK_COLLISIONDATA_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_COLLISIONDATA, "empty", -1);
		public static ChunkDescriptor CHUNK_SCRIPT_EMPTY = new ChunkDescriptor (0x00000000, T_CHUNK_TYPE.T_CHUNK_SCRIPT, "empty", -1);
	}

	public class ChunkGeneric
	{
		
		public ChunkDescriptor descriptor;
		// Loaded infos
		public uint /*  uint32_t*/ originalLen;
		public uint /*  uint32_t*/ originalChunk;
		// Data len
		public uint /* uint32_t*/ len;

		public ChunkGeneric ()// /*const*/ ChunkDescriptor /*&*/ initDescriptor =ChunkTypes.CHUNK_GENERIC_EMPTY )
		{
			descriptor = ChunkTypes.CHUNK_GENERIC_EMPTY;//initdescriptor;
		}

		public ChunkGeneric (uint initOriginalLen, uint initOriginalChunk, /*const*/ChunkDescriptor /*&*/ initDescriptor)
		{
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			descriptor = initDescriptor;
		}
	}

	public class ChunkChunkList:ChunkGeneric
	{
		public List<ChunkGeneric> _value;
		public ChunkChunkList parent;

		public ChunkChunkList ()
		{
			descriptor = ChunkTypes.CHUNK_CHUNKLIST_EMPTY; 
			originalLen = 0;
			originalChunk = 0;
			len = 0;
			parent = null;
			_value = new List<ChunkGeneric> ();				
		}

		public ChunkChunkList (/*const*/ ChunkDescriptor /*&*/ initDescriptor)
		{
			descriptor = initDescriptor;
			originalLen = 0;
			originalChunk = 0;
			len = 0;
			parent = null;
			_value = new List<ChunkGeneric> ();
		}

		public ChunkChunkList (uint initOriginalLen, uint initOriginalChunk, /*const*/ChunkDescriptor /*&*/ initDescriptor,/* const*/List/* std::vector*/<ChunkGeneric> initValue)
		{
			descriptor = initDescriptor;
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;
			_value = initValue;
			parent = null;
		}
		/*ChunkChunkList::~ChunkChunkList() {
		    // 08/05/12 SK1: typesafe delete of chunk objects
		    uint32_t size = value.size();
		    for (uint32_t i=0; i<size; i++)
		    {
		      ChunkGeneric *chunk = value[i];
		      switch( chunk->descriptor.type )
		      {
		        case T_CHUNK_CHUNKLIST :
		                            delete (ChunkChunkList*) chunk;
		                            break;
		        case T_CHUNK_GENERIC :
		                            delete (ChunkGeneric*) chunk;
		                            break;
		        case T_CHUNK_COLLISIONDATA :
		        case T_CHUNK_RAWDATA :
		                            delete (ChunkRawData*) chunk;
		                            break;
		        case T_CHUNK_SCRIPT :
		                            delete (ChunkScript*) chunk;
		                            break;
		        case T_CHUNK_INT :
		                            delete (ChunkInt*) chunk;
		                            break;
		        case T_CHUNK_COLOR :
		                            delete (ChunkColor*) chunk;
		                            break;
		        case T_CHUNK_VALUELIST :
		                            delete (ChunkValueList*) chunk;
		
		                            break;
		        case T_CHUNK_FLOAT :
		                            delete (ChunkFloat*) chunk;
		                            break;
		        case T_CHUNK_VECTOR2D :
		                            delete (ChunkVector2D*) chunk;
		                            break;
		        case T_CHUNK_STRING :
		                            delete (ChunkString*) chunk;
		                            break;
		        case T_CHUNK_WSTRING :
		                            delete (ChunkWString*) chunk;
		                            break;
		        case T_CHUNK_STRINGLIST :
		                            {
		                              ChunkStringList* list = (ChunkStringList*)chunk;
		                              list->value.clear();
		                            }
		                            delete (ChunkStringList*) chunk;
		                            break;
		        default:
		                            delete chunk;
		                            break;
		      }
		    }
		
		    value.clear();
			}
		*/
		public void Add (ChunkGeneric  child)
		{
			_value.Add/*push_back*/ (child);
		}
    }

	
	public class ChunkInt:ChunkGeneric
	{
		public int _value;
		/*public ChunkInt(ChunkDescriptor  initDescriptor) 
			{
				descriptor = initDescriptor;
				originalLen = 0;
				originalChunk = 0;
				len = 0;
				_value = 0;
			}*/
		public ChunkInt (uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, int initValue)
		{
			descriptor = initDescriptor;		
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;		
			_value = initValue;
		}

        public override string ToString()
        {
            return "" + _value;
        }
	}

	public class ChunkColor:ChunkGeneric
	{
		public /*Color32*/Color _value;

		public ChunkColor (uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, int initValue)
		{
			descriptor = initDescriptor;		
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;

			_value = new Color((uint)initValue, ColorFormat.Argb);// FPBaseHandler.Int32ToColor32 (initValue);
		}
        public override string ToString()
        {
            return "" + _value;
        }
    }

	public class ChunkFloat:ChunkGeneric
	{
		public float _value;

		public ChunkFloat (uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, float initValue)
		{
			descriptor = initDescriptor;		
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;		
			_value = initValue;
		}
        public override string ToString()
        {
            return "" + _value;
        }
    }

	public class ChunkString:ChunkGeneric
	{
		public string _value;

		public ChunkString (uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, string initValue)
		{
			descriptor = initDescriptor;		
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;
			_value = initValue;
		}

        public override string ToString()
        {
            return _value;
        }
    }

    public class ChunkStringList : ChunkGeneric
    {
        public List<string> _items = new List<string>();

        public ChunkStringList(uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, List<string> items)
        {
            descriptor = initDescriptor;
            originalLen = initOriginalLen;
            originalChunk = initOriginalChunk;
            len = originalLen;
            _items = items;
        }
        public override string ToString()
        {
            string s = "";
            foreach (var st in _items)
                s += st + ";";
            return s;
        }
    }

    public class ChunkWString:ChunkGeneric
	{
		public string _value;

		public ChunkWString(ChunkDescriptor  initDescriptor)
		{
			descriptor = initDescriptor;
			originalLen = 0;
			originalChunk = 0;
			len = 0;
			_value = "";
		}
		public ChunkWString(uint initOriginalLen, uint initOriginalChunk,ChunkDescriptor initDescriptor,string initValue)
		{
			descriptor = initDescriptor;
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;
			_value = initValue;
		}
        public override string ToString()
        {
            return _value;
        }
    }

	public class ChunkScript:ChunkGeneric
	{
		public RawData _value;

		public ChunkScript (ChunkDescriptor  initDescriptor)
		{
			descriptor = initDescriptor;
			originalLen = 0;
			originalChunk = 0;
			len = 0;
		}

		public ChunkScript (uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, RawData  initValue)
		{
			descriptor = initDescriptor;
	
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;
			_value = initValue;
		}
        public override string ToString()
        {
            return _value.ToString();
        }
    }

	public class ChunkValueList : ChunkGeneric
	{
		public int _value;

		public ChunkValueList (ChunkDescriptor initDescriptor)
		{
			descriptor = initDescriptor;
			originalLen = 0;
			originalChunk = 0;
			len = 0;
			_value = 0;
		}

		public ChunkValueList (uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, int initValue)
		{
			descriptor = initDescriptor;
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;
			_value = initValue;
		}
	}

	public class ChunkVector2D: ChunkGeneric
	{
		public Vertex2D _value;
		public ChunkVector2D( ChunkDescriptor  initDescriptor) {
			descriptor = initDescriptor;
			originalLen = 0;
			originalChunk = 0;
			len = 0;
		}
		public ChunkVector2D(uint initOriginalLen, uint initOriginalChunk,ChunkDescriptor  initDescriptor, Vertex2D initValue) {
			descriptor = initDescriptor;
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;
			_value = initValue;
		}
	}

	public class ChunkCollisionData : ChunkGeneric
	{
		public List<FPModelCollisionData> _value;
		/*   public ChunkCollisionData(ChunkDescriptor initDescriptor = CHUNK_COLLISIONDATA_EMPTY)
        {

        }
  ChunkCollisionData(uint32_t initOriginalLen, uint32_t initOriginalChunk, const ChunkDescriptor & initDescriptor, const std::vector<FPModelCollisionData> & initValue );*/

		ChunkCollisionData (ChunkDescriptor initDescriptor)
		{
			descriptor = initDescriptor;
			originalLen = 0;
			originalChunk = 0;
			len = 0;
		}

		ChunkCollisionData (uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, List<FPModelCollisionData> initValue)
		{
			descriptor = initDescriptor;
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			len = originalLen;
			_value = initValue;
		}
	};

	public class VLDescriptor
	{
		public int /*32_t*/ code;
		public string label;

		public VLDescriptor (int/*32_t*/ codeP, string /* char **/labelP)
		{
			code = codeP;
			label = labelP;
		}
			
		/*void operator = ( const VLDescriptor &c )
			  {
			    code = c.code;
			    label = c.label;
			  }
			*/
	}



		


	public class ChunkDescriptor
	{
		public uint /*32_t*/ chunk;
		public /*FP_defs.*/T_CHUNK_TYPE type;
		public string label;
		public int /*32_t*/ offset;
		public List<VLDescriptor> valueList = null;

		public ChunkDescriptor ()
		{
			chunk = 0;
			type = (T_CHUNK_TYPE)0;
			label = "";
			offset = 0;
		}

		public ChunkDescriptor (uint chunkP, T_CHUNK_TYPE typeP, string labelP, int offsetP)//, List<VLDescriptor>valueListP = null )
		{
			chunk = chunkP;
			type = typeP;
			label = labelP;
			offset = offsetP;
			valueList = null;// valueListP;
		}

		public ChunkDescriptor (uint chunkP, T_CHUNK_TYPE typeP, string labelP, int offsetP, List<VLDescriptor> valueListP)
		{
			chunk = chunkP;
			type = typeP;
			label = labelP;
			offset = offsetP;
			valueList = valueListP;
		}
		/*  void operator = ( const ChunkDescriptor &c )
			  {
			    chunk = c.chunk;
			    type = c.type;
			    label = c.label;
			    offset = c.offset;
			    valueList = c.valueList;
			  }*/
		
	};

	public class RawData
	{
		public uint /*uint32_t*/ len;
		public byte[] /*uint8_t * */ data;
		//BinaryReader reader;

		public RawData (CFStream cfstream)
		{
			data = cfstream.GetData ();
			len = (uint)data.Length;
			//Stream str=new MemoryStream(data);
			//BinaryReader reader=new BinaryReader(str);
			//str.Close();
			//str=null;
				
		}

		public RawData ()
		{
			len = 0;
			data = null;
		}
		/*	}
		
			RawData::RawData(uint32_t initLen, bool zeroMemory) : len(initLen) {
				if (len > 0) {
					data = new uint8_t[len];
		
					if (zeroMemory) {
						memset(data, 0, len);
					}
				} else {
					data = NULL;
				}
			}
		*/
		public RawData (uint srcLen, byte[] srcData)
		{  
			len = srcLen;
			if (len > 0) {
				data = new byte[len];
				data = srcData;
			} else {
				data = null;
			}
		}
		/*
			RawData::RawData(const RawData & rd) {
				len = rd.len;
				if (len > 0) {
					data = new uint8_t[len];
					memcpy(data, rd.data, len);
				} else {
					data = NULL;
				}
			}
		
			RawData & RawData::operator=(const RawData & rd) {
				if (this != &rd) {  // Check for self-assignment
					if (data != NULL) {
						delete [] data;
						data = NULL;
					}
					len=rd.len;
					if (len > 0) {
						data = new uint8_t[len];
						memcpy(data, rd.data, len);
					} else {
						data = NULL;
					}
				}
				return *this;
			}
		
			RawData::~RawData() {
				if (data != NULL) {
					delete [] data;
				}
			}
				 */
		public bool ispacked ()
		{
			if (len < 4)
				return false;
			if (Convert.ToChar (data [0]) == 'z' && Convert.ToChar (data [1]) == 'L' && Convert.ToChar (data [2]) == 'Z' && Convert.ToChar (data [3]) == 'O')
				//if(reader.ReadChar()=='z' && reader.ReadChar()=='L' && reader.ReadChar()=='Z' && reader.ReadChar()=='O')
			     return true;
			else
				return false;
		}
	

		/*
		    RawData * RawData::packLZO()
		    {
		        if (ispacked()) {
		            // already packed
					return new ops::RawData(len, data);
		        }
		        else
		        {
		            ops::RawData * result = new ops::RawData();
		            // TODO : free not used memory ?
		            result->data = new uint8_t[8 + len * 2];
		            result->len = 0;
		            int r = lzo1x_1_compress(data, len, &result->data[8], (lzo_uint*)&result->len, lzo_wrkmem);
		            result->data[0]='z';
		            result->data[1]='L';
		            result->data[2]='Z';
		            result->data[3]='O';
		            *((uint32_t*)(&result->data[4])) = len;
		            result->len += 8;
		            if (r == LZO_E_OK)
		            {
		                return result;
		            } else {
		                return NULL;
		            }
		        }
		    }
		
			RawData * RawData::unpackLZO()
			{
		        if (ispacked()) {
		            // Decompress if 'zLZO'
					ops::RawData * value = new ops::RawData();
		            uint32_t rawLen;
					value->len =  *((uint32_t *) &data[4]);
					value->data = new uint8_t[value->len];
		            // TODO cast, int
					int r = lzo1x_decompress( (const unsigned char *) &data[8], len - 8, (unsigned char *)value->data, (lzo_uint*)&rawLen, NULL);
					if (r != LZO_E_OK || rawLen != value->len) {
		                // this should NEVER happen
				//		ERROR << " internal error - decompression failed : " << r;
						delete [] value->data;
						value->len = 0;
						value->data = NULL;
					}
					return value;
		        }
		        else
		        {
		            // Plain copy
					return new ops::RawData(len, data);
		        }*/
	}

	public class ChunkRawData : ChunkGeneric
	{
		public RawData _value;

		public  ChunkRawData ()// const ChunkDescriptor & initDescriptor = CHUNK_RAWDATA_EMPTY );
		{
			descriptor = ChunkTypes.CHUNK_RAWDATA_EMPTY;
		}

		public ChunkRawData (uint initOriginalLen, uint initOriginalChunk, ChunkDescriptor initDescriptor, RawData  initValue)
		{
			originalLen = initOriginalLen;
			originalChunk = initOriginalChunk;
			descriptor = initDescriptor;
			_value = initValue;
		}
	}
	
}
