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
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace VisualPinball.Unity.FP
{
    namespace defs
    {
        public enum T_CHUNK_TYPE
        {
            // Container
            T_CHUNK_CHUNKLIST,
            // Generic types
            T_CHUNK_GENERIC,
            T_CHUNK_RAWDATA,
            T_CHUNK_INT,
            T_CHUNK_FLOAT,
            T_CHUNK_COLOR,
            T_CHUNK_VECTOR2D,
            T_CHUNK_STRING,
            T_CHUNK_WSTRING,
            T_CHUNK_STRINGLIST,
            T_CHUNK_VALUELIST,
            // Specific types
            T_CHUNK_COLLISIONDATA,
            T_CHUNK_RAWDATALZO, // compressed
            T_CHUNK_SCRIPT // SK1: Anomalie for table script
        }

        [System.Serializable]
        [StructLayout(LayoutKind.Explicit, Size = 44, Pack = 0)]
        public struct FPModelCollisionData
        {
            [MarshalAs(UnmanagedType.U4)]
            [FieldOffset(0)]
            public uint type;

            [MarshalAs(UnmanagedType.U4)]
            [FieldOffset(4)]
            public uint generateHit;

            [MarshalAs(UnmanagedType.U4)]
            [FieldOffset(8)]
            public uint effectBall;

            [MarshalAs(UnmanagedType.U4)]
            [FieldOffset(12)]
            public uint eventID;

            [MarshalAs(UnmanagedType.R4)]// UnmanagedType..U4)]
            [FieldOffset(16)]
            public float x;

            [MarshalAs(UnmanagedType.R4)]// UnmanagedType..U4)]
            [FieldOffset(20)]
            public float y;

            [MarshalAs(UnmanagedType.R4)]// UnmanagedType..U4)]
            [FieldOffset(24)]
            public float z;

            [MarshalAs(UnmanagedType.R4)]// UnmanagedType..U4)]
            [FieldOffset(28)]
            public float value1;

            [MarshalAs(UnmanagedType.R4)]// UnmanagedType..U4)]
            [FieldOffset(32)]
            public float value2;

            [MarshalAs(UnmanagedType.R4)]// UnmanagedType..U4)]
            [FieldOffset(36)]
            public float value3;

            [MarshalAs(UnmanagedType.R4)]// UnmanagedType..U4)]
            [FieldOffset(40)]
            public float value4;
            //	  float value1, value2, value3, value4;

            //FPModelCollisionData();
            public override string ToString()
            {
                return String.Format("type={0}, generateHit={1}, effectBall={2}, eventID={3}, x={4}, y={5}, z={6}", this.type, this.generateHit, this.effectBall, this.eventID, this.x, this.y, this.z);
            }
        };

        public static class Descriptors
        {
            public static ChunkDescriptor[] CHUNKS_DEFAULTNAME_ARRAY = new ChunkDescriptor[]
            {
              new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 )
            };

            public static ChunkDescriptor[] CHUNKS_RESOURCE_ARRAY = new ChunkDescriptor[12]
            {
              new ChunkDescriptor( 0xA4F1B9D1, T_CHUNK_TYPE.T_CHUNK_INT,    "type", -1 ),
              new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_STRING,  "name", -1 ),
              new ChunkDescriptor( 0xA4F4C4DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "id", -1 ),
              new ChunkDescriptor( 0xA1EDD1D5, T_CHUNK_TYPE.T_CHUNK_STRING,  "path", -1 ),
              new ChunkDescriptor( 0x9EF3C6D9, T_CHUNK_TYPE.T_CHUNK_INT,    "linked", -1 ),
              new ChunkDescriptor( 0xA6E9BEE4, T_CHUNK_TYPE.T_CHUNK_INT,    "s3tc_compression", -1 ),
              new ChunkDescriptor( 0x95F5CCE1, T_CHUNK_TYPE.T_CHUNK_INT,    "disable_filtering", -1 ),
              new ChunkDescriptor( 0x96F3C0D1, T_CHUNK_TYPE.T_CHUNK_COLOR,    "transparent_color", -1 ),
              new ChunkDescriptor( 0xA4E7C9D2, T_CHUNK_TYPE.T_CHUNK_INT,    "data_len", -1 ),
              new ChunkDescriptor( 0xA8EDD1E1, T_CHUNK_TYPE.T_CHUNK_RAWDATA,  "data", -1 ),
              new ChunkDescriptor( 0x95EFBBD9, T_CHUNK_TYPE.T_CHUNK_RAWDATA,  "unknown_1", -1 ),
              new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            public static ChunkDescriptor[] CHUNKS_TABLE_ARRAY = new ChunkDescriptor[71]
            {
              new ChunkDescriptor( 0xA5F8BBD1, T_CHUNK_TYPE.T_CHUNK_INT,    "width", -1 ),
              new ChunkDescriptor( 0x9BFCC6D1, T_CHUNK_TYPE.T_CHUNK_INT,    "length", -1 ),
              new ChunkDescriptor( 0xA1FACCD1, T_CHUNK_TYPE.T_CHUNK_INT,    "front_glass_height", -1 ),
              new ChunkDescriptor( 0xA1FAC0D1, T_CHUNK_TYPE.T_CHUNK_INT,    "rear_glass_height", -1 ),
              new ChunkDescriptor( 0x9AF5BFD1, T_CHUNK_TYPE.T_CHUNK_FLOAT,  "slope", -1 ),
              new ChunkDescriptor( 0x9DF2CFD5, T_CHUNK_TYPE.T_CHUNK_COLOR,  "playfield_color", -1 ),
              new ChunkDescriptor( 0xA2F4C9D5, T_CHUNK_TYPE.T_CHUNK_STRING, "playfield_texture", -1 ),
              new ChunkDescriptor( 0x9DF2CFE2, T_CHUNK_TYPE.T_CHUNK_COLOR,  "cabinet_wood_color", -1 ),
              new ChunkDescriptor( 0x9DF2CFE3, T_CHUNK_TYPE.T_CHUNK_COLOR,  "button_color", -1 ),
              new ChunkDescriptor( 0x9AFECBE3, T_CHUNK_TYPE.T_CHUNK_COLOR,  "translite_color", -1 ),
              new ChunkDescriptor( 0xA2F4C9E3, T_CHUNK_TYPE.T_CHUNK_STRING, "translite_image", -1 ),
              new ChunkDescriptor( 0x96F2C6DE, T_CHUNK_TYPE.T_CHUNK_INT,    "glossiness", -1 ),
              new ChunkDescriptor( 0x95F5C9D1, T_CHUNK_TYPE.T_CHUNK_INT,    "warnings_before_tilt", -1 ),
              new ChunkDescriptor( 0xA5F8C0DE, T_CHUNK_TYPE.T_CHUNK_INT,    "display_grid_in_editor", -1 ),
              new ChunkDescriptor( 0x96FDC0DE, T_CHUNK_TYPE.T_CHUNK_INT,    "grid_size", -1 ),
              new ChunkDescriptor( 0xA0FBC2E1, T_CHUNK_TYPE.T_CHUNK_INT,    "display_playfield_in_editor", -1 ),
              new ChunkDescriptor( 0xA0FAD0E1, T_CHUNK_TYPE.T_CHUNK_INT,    "display_translite_in_editor", -1 ),
              new ChunkDescriptor( 0xA0EACBE3, T_CHUNK_TYPE.T_CHUNK_INT,    "translite_width", -1 ),
              new ChunkDescriptor( 0xA4F9CBE3, T_CHUNK_TYPE.T_CHUNK_INT,    "translite_height", -1 ),
              new ChunkDescriptor( 0x99E8BED8, T_CHUNK_TYPE.T_CHUNK_INT,    "machine_type", -1 ),
              new ChunkDescriptor( 0xA2F4C9E2, T_CHUNK_TYPE.T_CHUNK_STRING, "cabinet_texture", -1 ),
              new ChunkDescriptor( 0x95EEC3D5, T_CHUNK_TYPE.T_CHUNK_STRING, "poster_image", -1 ),
              new ChunkDescriptor( 0x9BF5CFD1, T_CHUNK_TYPE.T_CHUNK_FLOAT,  "table_center_line", -1 ),
              new ChunkDescriptor( 0x9BF5CCD1, T_CHUNK_TYPE.T_CHUNK_FLOAT,  "table_flipper_line", -1 ),
              new ChunkDescriptor( 0xA0FCBFE1, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_3", -1 ),
              new ChunkDescriptor( 0x9AFECDD2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_4_color", -1 ),
              new ChunkDescriptor( 0xA1FACCD1, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_5", -1 ),
              new ChunkDescriptor( 0x9BEDC9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "table_name", -1 ),
              new ChunkDescriptor( 0xA4EBC9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "version", -1 ),
              new ChunkDescriptor( 0x9500C9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "table_authors", -1 ),
              new ChunkDescriptor( 0xA5EFC9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "release_date", -1 ),
              new ChunkDescriptor( 0x9CFCC9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "mail", -1 ),
              new ChunkDescriptor( 0x96EAC9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "web_page", -1 ),
              new ChunkDescriptor( 0xA4FDC9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "description", -1 ),
              new ChunkDescriptor( 0x96EFC9D1, T_CHUNK_TYPE.T_CHUNK_INT,    "rules_len", -1 ),
              new ChunkDescriptor( 0x94EFC9D1, T_CHUNK_TYPE.T_CHUNK_RAWDATA,"rules", -1 ),
              new ChunkDescriptor( 0x99F5C9D1, T_CHUNK_TYPE.T_CHUNK_STRING, "loading_picture", -1 ),
              new ChunkDescriptor( 0x95FEC9D1, T_CHUNK_TYPE.T_CHUNK_COLOR,  "loading_color", -1 ),
              new ChunkDescriptor( 0xA2F1D0DC, T_CHUNK_TYPE.T_CHUNK_INT,    "ball_per_game", -1 ),
              new ChunkDescriptor( 0xA700C8DC, T_CHUNK_TYPE.T_CHUNK_INT,    "initial_jackpot", -1 ),
              new ChunkDescriptor( 0x9C10CADC, T_CHUNK_TYPE.T_CHUNK_STRING, "high_scores_default_initial_1", -1 ),
              new ChunkDescriptor( 0x9710CADC, T_CHUNK_TYPE.T_CHUNK_INT,    "high_scores_default_score_1", -1 ),
              new ChunkDescriptor( 0x9C0FCADC, T_CHUNK_TYPE.T_CHUNK_STRING, "high_scores_default_initial_2", -1 ),
              new ChunkDescriptor( 0x970FCADC, T_CHUNK_TYPE.T_CHUNK_INT,    "high_scores_default_score_2", -1 ),
              new ChunkDescriptor( 0x9C0ECADC, T_CHUNK_TYPE.T_CHUNK_STRING, "high_scores_default_initial_3", -1 ),
              new ChunkDescriptor( 0x970ECADC, T_CHUNK_TYPE.T_CHUNK_INT,    "high_scores_default_score_3", -1 ),
              new ChunkDescriptor( 0x9C0DCADC, T_CHUNK_TYPE.T_CHUNK_STRING, "high_scores_default_initial_4", -1 ),
              new ChunkDescriptor( 0x970DCADC, T_CHUNK_TYPE.T_CHUNK_INT,    "high_scores_default_score_4", -1 ),

              new ChunkDescriptor( 0x9BFBBFDC, T_CHUNK_TYPE.T_CHUNK_STRING, "special_score_title", -1 ),
              new ChunkDescriptor( 0x93FBBFDC, T_CHUNK_TYPE.T_CHUNK_INT,    "special_score_value", -1 ),

              new ChunkDescriptor( 0x96ECCFE2, T_CHUNK_TYPE.T_CHUNK_RAWDATA,"unknown_6", -1 ),

              new ChunkDescriptor( 0xA4FBBFDC, T_CHUNK_TYPE.T_CHUNK_STRING, "special_score_text", -1 ),

              new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,"table_element_name", -1 ),
              new ChunkDescriptor( 0x95FDCDD2, T_CHUNK_TYPE.T_CHUNK_INT,    "count_table_elements", -1 ),
              new ChunkDescriptor( 0xA2F4C9D2, T_CHUNK_TYPE.T_CHUNK_INT,    "count_images", -1 ),
              new ChunkDescriptor( 0xA5F3BFD2, T_CHUNK_TYPE.T_CHUNK_INT,    "count_sounds", -1 ),
              new ChunkDescriptor( 0x96ECC5D2, T_CHUNK_TYPE.T_CHUNK_INT,    "count_musics", -1 ),
              new ChunkDescriptor( 0xA5F2C5D2, T_CHUNK_TYPE.T_CHUNK_INT,    "count_pin_models", -1 ),
              new ChunkDescriptor( 0x95F5C9D2, T_CHUNK_TYPE.T_CHUNK_INT,    "count_image_lists", -1 ),
              new ChunkDescriptor( 0x95F5C6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "count_light_lists", -1 ),
              new ChunkDescriptor( 0x9BFBCED2, T_CHUNK_TYPE.T_CHUNK_INT,    "count_dmd_fonts", -1 ),
			//SK1  new ChunkDescriptor( 0xA4FDC3E2, T_CHUNK_GENERIC,  "unknown_15", -1 ),
			  new ChunkDescriptor( 0xA4FDC3E2, T_CHUNK_TYPE.T_CHUNK_SCRIPT, "script", -1 ),   //
			
			//SK1  new ChunkDescriptor( 0x4F5A4C7A, T_CHUNK_RAWDATA,  "script", -1 ),
			
			  new ChunkDescriptor( 0x91FBCCD6, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "translate_x", -1 ),
              new ChunkDescriptor( 0x90FBCCD6, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "translate_y", -1 ),
              new ChunkDescriptor( 0x9200CFD2, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "scale_x", -1 ),
              new ChunkDescriptor( 0x9100CFD2, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "scale_y", -1 ),
              new ChunkDescriptor( 0x91EECCD6, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "translite_translate_x", -1 ),
              new ChunkDescriptor( 0x90EECCD6, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "translite_translate_y", -1 ),
              new ChunkDescriptor( 0x91EECFD2, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "translite_scale_x", -1 ),
              new ChunkDescriptor( 0x90EECFD2, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "translite_scale_y", -1 ),

              new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC, "end", -1 )
            };

            public static List<VLDescriptor> VL_MATERIAL_ARRAY = new List<VLDescriptor>
            {
                  new VLDescriptor( 0, "metal" ),
                  new VLDescriptor( 1, "wood" ),
                  new VLDescriptor( 2, "plastic" ),
                  new VLDescriptor( 3, "rubber" )
            };
            // static ARRAY2VECTOR(VLDescriptor, VL_MATERIAL );

            public static ChunkDescriptor[] CHUNKS_PINMODEL_ARRAY = new ChunkDescriptor[34]
            {
              new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_STRING,  "name",-1),//Marshal.OffsetOf(typeof(FPModel),"name" )),
			  new ChunkDescriptor( 0xA4F4C4DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "id", -1),//offsetof( FPModel, id )),
			  new ChunkDescriptor( 0x9EF3C6D9, T_CHUNK_TYPE.T_CHUNK_INT,    "linked", -1),//offsetof( FPModel, linked )),
			
			  new ChunkDescriptor( 0xA4F1B9D1, T_CHUNK_TYPE.T_CHUNK_INT,    "type", -1),//offsetof( FPModel, type )),
			  new ChunkDescriptor( 0x99E8BED8, T_CHUNK_TYPE.T_CHUNK_VALUELIST,  "material_type",-1,VL_MATERIAL_ARRAY),//offsetof( FPModel, materialType ), &VL_MATERIAL ),
			
			  new ChunkDescriptor( 0x9D00C4DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "preview_path",-1),// offsetof( ops::fp::FPModel, previewPath )),
			  new ChunkDescriptor( 0x8FF8BFDC, T_CHUNK_TYPE.T_CHUNK_INT,    "preview_data_len", -1 -1),//),
			  new ChunkDescriptor( 0x9600CEDC, T_CHUNK_TYPE.T_CHUNK_RAWDATA,  "preview_data",-1),// offsetof( ops::fp::FPModel, previewData )),
			
			  new ChunkDescriptor( 0x9AFEC2D5, T_CHUNK_TYPE.T_CHUNK_INT,    "per_polygon_collision", -1),//offsetof( ops::fp::FPModel, collisionPerPolygon )),
			  new ChunkDescriptor( 0xA5F2C6E0, T_CHUNK_TYPE.T_CHUNK_INT,    "secondary_model_enabled", -1 ),
              new ChunkDescriptor( 0xA5FDC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "secondary_model_z_distance", -1),//offsetof( ops::fp::FPModel, secondaryModelZDistance )),
			  new ChunkDescriptor( 0xA8EFCBD3, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "special_value", -1),//offsetof( ops::fp::FPModel, specialValue )),
			
			  new ChunkDescriptor( 0xA5F2C5D5, T_CHUNK_TYPE.T_CHUNK_INT,    "primary_model_enabled", -1 ),
              new ChunkDescriptor( 0x9D00C4D5, T_CHUNK_TYPE.T_CHUNK_STRING,  "primary_model_path", -1),//offsetof( ops::fp::FPModel, primaryModelPath )),
			  new ChunkDescriptor( 0x8FF8BFD5, T_CHUNK_TYPE.T_CHUNK_INT,    "primary_model_data_len", -1 ),
              new ChunkDescriptor( 0x9600CED5, T_CHUNK_TYPE.T_CHUNK_RAWDATA,  "primary_model_data", -1),//offsetof( ops::fp::FPModel, primaryModelData )),
			
			  new ChunkDescriptor( 0xA5F2C5D2, T_CHUNK_TYPE.T_CHUNK_INT,    "secondary_model_enabled_at_z_distance", -1 ),
              new ChunkDescriptor( 0x9D00C4D2, T_CHUNK_TYPE.T_CHUNK_STRING,  "secondary_model_path", -1),//offsetof( ops::fp::FPModel, secondaryModelPath )),
			  new ChunkDescriptor( 0x8FF8BFD2, T_CHUNK_TYPE.T_CHUNK_INT,    "secondary_model_data_len", -1 ),
              new ChunkDescriptor( 0x9600CED2, T_CHUNK_TYPE.T_CHUNK_RAWDATA,  "secondary_model_data", -1),//offsetof( ops::fp::FPModel, secondaryModelData )),
			
			  new ChunkDescriptor( 0xA5F2C5D1, T_CHUNK_TYPE.T_CHUNK_INT,    "mask_model_enabled", -1 ),
              new ChunkDescriptor( 0x9D00C4D1, T_CHUNK_TYPE.T_CHUNK_STRING,  "mask_model_path", -1),//offsetof( ops::fp::FPModel, tertiaryModelPath )),
			  new ChunkDescriptor( 0x8FF8BFD1, T_CHUNK_TYPE.T_CHUNK_INT,    "mask_model_data_len", -1 ),
              new ChunkDescriptor( 0x9600CED1, T_CHUNK_TYPE.T_CHUNK_RAWDATA,  "mask_model_data", -1),//offsetof( ops::fp::FPModel, tertiaryModelData )),
			
			  new ChunkDescriptor( 0x9CF1BDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflection_use_primary_model", -1 ),
              new ChunkDescriptor( 0xA5F2C5D3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflection_model_enabled", -1 ),
              new ChunkDescriptor( 0x9D00C4D3, T_CHUNK_TYPE.T_CHUNK_STRING,  "reflection_model_path", -1),//offsetof( ops::fp::FPModel, reflectionModelPath )),
			  new ChunkDescriptor( 0x8FF8BFD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflection_model_data_len", -1 ),
              new ChunkDescriptor( 0x9600CED3, T_CHUNK_TYPE.T_CHUNK_RAWDATA,  "reflection_model_data", -1),//offsetof( ops::fp::FPModel, reflectionModelData )),
			
			  new ChunkDescriptor( 0x8FEEC3E2, T_CHUNK_TYPE.T_CHUNK_INT,    "nb_collision_shapes", -1 ),
              new ChunkDescriptor( 0x93FBC3E2, T_CHUNK_TYPE.T_CHUNK_INT,    "collision_shapes_enabled", -1 ),
              new ChunkDescriptor( 0x9DFCC3E2, T_CHUNK_TYPE.T_CHUNK_COLLISIONDATA, "collision_shapes_data",-1),// offsetof( ops::fp::FPModel, collisionElements )),
			
			  new ChunkDescriptor( 0xA1EDD1D5, T_CHUNK_TYPE.T_CHUNK_STRING,  "linked_path", -1),//offsetof( ops::fp::FPModel, linkedPath )),
			  new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )

            };

            public static ChunkDescriptor CHUNK_COLLISIONDATA_EMPTY = new ChunkDescriptor(0x00000000, T_CHUNK_TYPE.T_CHUNK_COLLISIONDATA, "empty", -1);

            public static ChunkDescriptor[] CHUNKS_LIST_ARRAY = new ChunkDescriptor[3]
            {

                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_STRING, "name", -1 ),
                new ChunkDescriptor( 0xA8EDD1E1, T_CHUNK_TYPE.T_CHUNK_STRINGLIST, "items", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Surfaces
            /// </summary>
			public static ChunkDescriptor[] CHUNKS_ELEMENT_2_ARRAY = new ChunkDescriptor[33]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING, "name", -1 ),
                new ChunkDescriptor( 0x91EDBEE1, T_CHUNK_TYPE.T_CHUNK_INT,    "display_image_in_editor", -1 ),
                new ChunkDescriptor( 0x9DF2CFD1, T_CHUNK_TYPE.T_CHUNK_COLOR,    "top_color", -1 ),
                new ChunkDescriptor( 0xA2F4C9D1, T_CHUNK_TYPE.T_CHUNK_STRING,  "top_texture", -1 ),
                new ChunkDescriptor( 0x91EDCFE2, T_CHUNK_TYPE.T_CHUNK_INT,    "cookie_cut", -1 ),
                new ChunkDescriptor( 0x95F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_map_the_top", -1 ),
                new ChunkDescriptor( 0x9DF2CFD2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "side_color", -1 ),
                new ChunkDescriptor( 0xA2F4C9D2, T_CHUNK_TYPE.T_CHUNK_STRING,  "side_texture", -1 ),
                new ChunkDescriptor( 0x9C00C0D1, T_CHUNK_TYPE.T_CHUNK_INT,    "transparency", -1 ),
                new ChunkDescriptor( 0x99F2BEDD, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "top_height", -1 ),
                new ChunkDescriptor( 0x95F2D0DD, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "bottom_height", -1 ),
                new ChunkDescriptor( 0x99E8BED8, T_CHUNK_TYPE.T_CHUNK_VALUELIST,  "material_type", -1,VL_MATERIAL_ARRAY),// &VL_MATERIAL ),
				new ChunkDescriptor( 0x96F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_map_the_side", -1 ),
                new ChunkDescriptor( 0x97F2C4DF, T_CHUNK_TYPE.T_CHUNK_INT,    "flat_shading", -1 ),
                new ChunkDescriptor( 0x9100C6D5, T_CHUNK_TYPE.T_CHUNK_INT,    "surface_is_a_playfield", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x9A00C5E0, T_CHUNK_TYPE.T_CHUNK_STRING,  "enamel_map", -1 ),
                new ChunkDescriptor( 0x95E9BED3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflect_texture", -1 ),
                new ChunkDescriptor( 0x97ECBFD3, T_CHUNK_TYPE.T_CHUNK_STRING,  "playfield", -1 ),
                new ChunkDescriptor( 0x99F2C0E1, T_CHUNK_TYPE.T_CHUNK_INT,    "dropped", -1 ),
                new ChunkDescriptor( 0x9DF5C3E2, T_CHUNK_TYPE.T_CHUNK_INT,    "collidable", -1 ),
                new ChunkDescriptor( 0x97FDC4D3, T_CHUNK_TYPE.T_CHUNK_INT,    "render_object", -1 ),
                new ChunkDescriptor( 0x95EBCDDD, T_CHUNK_TYPE.T_CHUNK_INT,    "generate_hit_event", -1 ),
                new ChunkDescriptor( 0x95F3C2D2, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "shape_point", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0xA1EDC5D2, T_CHUNK_TYPE.T_CHUNK_INT,    "smooth", -1 ),
                new ChunkDescriptor( 0xA2F3C6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_1", -1 ),
                new ChunkDescriptor( 0xA8FCC6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_2", -1 ),
                new ChunkDescriptor( 0x99E9BEE4, T_CHUNK_TYPE.T_CHUNK_INT,    "automatic_texture_coordinate", -1 ),
                new ChunkDescriptor( 0x9AFEBAD1, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "texture_coordinate", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Round shapedLamp
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_3_ARRAY = new ChunkDescriptor[20]
            {
                  new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                  new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                  new ChunkDescriptor( 0x9BFCCFDD, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "glow_center", -1 ),
                  new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                  new ChunkDescriptor( 0x96F3CDD9, T_CHUNK_TYPE.T_CHUNK_STRING,  "lens_texture", -1 ),
                  new ChunkDescriptor( 0x9D00C9E1, T_CHUNK_TYPE.T_CHUNK_INT,    "diameter", -1 ),
                  new ChunkDescriptor( 0x9DF2CFD9, T_CHUNK_TYPE.T_CHUNK_COLOR,    "lit_color", -1 ),
                  new ChunkDescriptor( 0xA6ECBFE4, T_CHUNK_TYPE.T_CHUNK_INT,    "auto_set_unlit_color", -1 ),
                  new ChunkDescriptor( 0x9DF2CFD0, T_CHUNK_TYPE.T_CHUNK_COLOR,    "unlit_color", -1 ),
                  new ChunkDescriptor( 0xA5F8BBE3, T_CHUNK_TYPE.T_CHUNK_INT,    "border_width", -1 ),
                  new ChunkDescriptor( 0x9DF2CFE3, T_CHUNK_TYPE.T_CHUNK_COLOR,    "border_color", -1 ),
                  new ChunkDescriptor( 0x91EDCFE2, T_CHUNK_TYPE.T_CHUNK_INT,    "cookie_cut", -1 ),
                  new ChunkDescriptor( 0x96FDD1D3, T_CHUNK_TYPE.T_CHUNK_INT,    "glow_radius", -1 ),
                  new ChunkDescriptor( 0x9600BED2, T_CHUNK_TYPE.T_CHUNK_INT,    "state", -1 ),
                  new ChunkDescriptor( 0x95F3C9E3, T_CHUNK_TYPE.T_CHUNK_INT,    "blink_interval", -1 ),
                  new ChunkDescriptor( 0x9600C2E3, T_CHUNK_TYPE.T_CHUNK_STRING,  "blink_pattern", -1 ),
                  new ChunkDescriptor( 0xA6F2C6D3, T_CHUNK_TYPE.T_CHUNK_INT, "object_appers_on", -1 ),
                  new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                  new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                  new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
                };

            /// <summary>
            /// shapedLamp
            /// </summary>
			public static ChunkDescriptor[] CHUNKS_ELEMENT_4_ARRAY = new ChunkDescriptor[24]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "texture_position", -1 ),
                new ChunkDescriptor( 0x9BFCCFDD, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "halo_position", -1 ),
                new ChunkDescriptor( 0x96F3CDD9, T_CHUNK_TYPE.T_CHUNK_STRING,  "lens_texture", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0x9DF2CFD9, T_CHUNK_TYPE.T_CHUNK_COLOR,    "lit_color", -1 ),
                new ChunkDescriptor( 0xA6ECBFE4, T_CHUNK_TYPE.T_CHUNK_INT,    "auto_set_unlit_color", -1 ),
                new ChunkDescriptor( 0x9DF2CFD0, T_CHUNK_TYPE.T_CHUNK_COLOR,    "unlit_color", -1 ),
                new ChunkDescriptor( 0xA5F8BBE3, T_CHUNK_TYPE.T_CHUNK_INT,    "border_width", -1 ),
                new ChunkDescriptor( 0x9DF2CFE3, T_CHUNK_TYPE.T_CHUNK_COLOR,    "border_color", -1 ),
                new ChunkDescriptor( 0x91EDCFE2, T_CHUNK_TYPE.T_CHUNK_INT,    "cookie_cut", -1 ),
                new ChunkDescriptor( 0x96FDD1D3, T_CHUNK_TYPE.T_CHUNK_INT,    "glow_radius", -1 ),
                new ChunkDescriptor( 0x9600BED2, T_CHUNK_TYPE.T_CHUNK_INT,    "state", -1 ),
                new ChunkDescriptor( 0x95F3C9E3, T_CHUNK_TYPE.T_CHUNK_INT,    "blink_interval", -1 ),
                new ChunkDescriptor( 0x9600C2E3, T_CHUNK_TYPE.T_CHUNK_STRING,  "blink_pattern", -1 ),
                new ChunkDescriptor( 0xA6F2C6D3, T_CHUNK_TYPE.T_CHUNK_INT, "object_appers_on", -1 ),
                new ChunkDescriptor( 0x95F3C2D2, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "shape_point", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0xA1EDC5D2, T_CHUNK_TYPE.T_CHUNK_INT,    "smooth", -1 ),
                new ChunkDescriptor( 0xA2F3C6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_2", -1 ),
                new ChunkDescriptor( 0xA8FCC6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_3", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Pegs
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_6_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8F3C0D6, T_CHUNK_TYPE.T_CHUNK_INT,    "mask_as_ornamental", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x96E8C0E2, T_CHUNK_TYPE.T_CHUNK_INT,    "crystal", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0xA5F3BFDD, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_hit", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC, "end", -1 )
            };
            /// <summary>
            /// Flippers
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_7_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA900BED2, T_CHUNK_TYPE.T_CHUNK_INT,    "start_angle", -1 ),
                new ChunkDescriptor( 0xA1FABED2, T_CHUNK_TYPE.T_CHUNK_INT,    "strength", -1 ),
                new ChunkDescriptor( 0xA2EABFE4, T_CHUNK_TYPE.T_CHUNK_INT,    "swing", -1 ),
                new ChunkDescriptor( 0x9700C6E0, T_CHUNK_TYPE.T_CHUNK_INT,    "elasticity", -1 ),
                new ChunkDescriptor( 0x9BEEBDDF, T_CHUNK_TYPE.T_CHUNK_STRING,  "flipper_up_sound", -1 ),
                new ChunkDescriptor( 0x9BEECEDF, T_CHUNK_TYPE.T_CHUNK_STRING,  "flipper_down_sound", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Leaf Targets
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_8_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0xA5F2C5E2, T_CHUNK_TYPE.T_CHUNK_STRING,  "cap_model", -1 ),
                new ChunkDescriptor( 0xA5F2C5E3, T_CHUNK_TYPE.T_CHUNK_STRING,  "base_model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0x96F3CDD9, T_CHUNK_TYPE.T_CHUNK_STRING,  "cap_texture", -1 ),
                new ChunkDescriptor( 0xA0EED1D5, T_CHUNK_TYPE.T_CHUNK_INT,    "passive", -1 ),
                new ChunkDescriptor( 0x9EEED1DD, T_CHUNK_TYPE.T_CHUNK_INT,    "trigger_skirt", -1 ),
                new ChunkDescriptor( 0x9AFEC7D2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "skirt_color", -1 ),
                new ChunkDescriptor( 0x9DF2CFD9, T_CHUNK_TYPE.T_CHUNK_COLOR,    "lit_color", -1 ),
                new ChunkDescriptor( 0xA6ECBFE4, T_CHUNK_TYPE.T_CHUNK_INT,    "auto_set_unlit_color", -1 ),
                new ChunkDescriptor( 0xA1FDC0D6, T_CHUNK_TYPE.T_CHUNK_INT,    "ordered_halo_glow", -1 ),
                new ChunkDescriptor( 0x9DF2CFD0, T_CHUNK_TYPE.T_CHUNK_COLOR,    "unlit_color", -1 ),
                new ChunkDescriptor( 0x9DF2CFE3, T_CHUNK_TYPE.T_CHUNK_COLOR,    "base_color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_1", -1 ),
                new ChunkDescriptor( 0x96E8C0E2, T_CHUNK_TYPE.T_CHUNK_INT,    "crystal", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0xA1FABED2, T_CHUNK_TYPE.T_CHUNK_INT,    "strength", -1 ),
                new ChunkDescriptor( 0xA5F3BFE3, T_CHUNK_TYPE.T_CHUNK_STRING,  "solenoid_sound", -1 ),
                new ChunkDescriptor( 0x95F9BBDF, T_CHUNK_TYPE.T_CHUNK_INT,    "flash_when_hit", -1 ),
                new ChunkDescriptor( 0x9600BED2, T_CHUNK_TYPE.T_CHUNK_INT,    "state", -1 ),
                new ChunkDescriptor( 0x95F3C9E3, T_CHUNK_TYPE.T_CHUNK_INT,    "blink_interval", -1 ),
                new ChunkDescriptor( 0x9600C2E3, T_CHUNK_TYPE.T_CHUNK_STRING,  "blink_pattern", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };


            /// <summary>
            /// Leaf Targets
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_10_ARRAY = new ChunkDescriptor[]
            {
              new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
              new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
              new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
              new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
              new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
              new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
              new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
              new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
              new ChunkDescriptor( 0xA5F3BFD1, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_hit", -1 ),
              new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
              new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
              new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Drop Targets Bank
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_11_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x9035D306, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_hit", -1 ),
                new ChunkDescriptor( 0x9035D2F8, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_reset", -1 ),
                new ChunkDescriptor( 0x8035E308, T_CHUNK_TYPE.T_CHUNK_INT,    "bank_count", -1 ),
                new ChunkDescriptor( 0x9133D308, T_CHUNK_TYPE.T_CHUNK_INT,    "bank_spacing", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Plungers
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_12_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "plunger_color", -1 ),
                new ChunkDescriptor( 0x9DF2CFE2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "face_plate_color", -1 ),
                new ChunkDescriptor( 0xA1FABED2, T_CHUNK_TYPE.T_CHUNK_INT,    "strength", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x95ECCFCF, T_CHUNK_TYPE.T_CHUNK_INT,    "include_v_cut", -1 ),
                new ChunkDescriptor( 0x90E9CFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "v_cut_position", -1 ),
                new ChunkDescriptor( 0x9BFCC6CF, T_CHUNK_TYPE.T_CHUNK_INT,    "v_cut_lenght", -1 ),
                new ChunkDescriptor( 0xA2F4C9CF, T_CHUNK_TYPE.T_CHUNK_STRING,  "v_cut_texture", -1 ),
                new ChunkDescriptor( 0x9DF2CFCF, T_CHUNK_TYPE.T_CHUNK_COLOR,    "v_cut_color", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Rubbers
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_13_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xBAB2BEB2, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0xB1BABCAA, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0xBAAFA6AC, T_CHUNK_TYPE.T_CHUNK_INT,    "subtype", -1 ),
                new ChunkDescriptor( 0xACB9B9B1, T_CHUNK_TYPE.T_CHUNK_INT,    "offset", -1 ),
                new ChunkDescriptor( 0xADB3B0BD, T_CHUNK_TYPE.T_CHUNK_COLOR,  "color", -1 ),
                new ChunkDescriptor( 0xACBEB3BB, T_CHUNK_TYPE.T_CHUNK_INT,    "elasticity", -1 ),
                new ChunkDescriptor( 0xB3B9BAAE, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0xB4BCB0B4, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0xA6BEB3BF, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xBDBBB1BB, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Shapeable Rubbers
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_14_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xBAB2BEB2, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0x9DF2CFD9, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0x96FBCCD6, T_CHUNK_TYPE.T_CHUNK_INT,    "offset", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0xA1FABED2, T_CHUNK_TYPE.T_CHUNK_INT,    "strength", -1 ),
                new ChunkDescriptor( 0x9700C6E0, T_CHUNK_TYPE.T_CHUNK_INT,    "elasticity", -1 ),
                new ChunkDescriptor( 0xA5F3BFD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_slingshot", -1 ),
                new ChunkDescriptor( 0x95F3C2D2, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST,  "shape_point", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0xA1EDC5D2, T_CHUNK_TYPE.T_CHUNK_INT,    "smooth", -1 ),
                new ChunkDescriptor( 0xA2F3C6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "slingshot", -1 ),
                new ChunkDescriptor( 0xA8FCC6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "single_leaf", -1 ),
                new ChunkDescriptor( 0x95F3BCE0, T_CHUNK_TYPE.T_CHUNK_INT,    "event_id", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };



            /// </summary>
            /// Ornament
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_15_ARRAY = new ChunkDescriptor[]
            {
              new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
              new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
              new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
              new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
              new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
              new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
              new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
              new ChunkDescriptor( 0xA8F3C0D6, T_CHUNK_TYPE.T_CHUNK_INT,    "mask_as_ornamental", -1 ),
              new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
              new ChunkDescriptor( 0x96FBCCD6, T_CHUNK_TYPE.T_CHUNK_INT,    "offset", -1 ),
              new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
              new ChunkDescriptor( 0x97ECBFD3, T_CHUNK_TYPE.T_CHUNK_STRING,  "playfield", -1 ),
              new ChunkDescriptor( 0x9A00C5E0, T_CHUNK_TYPE.T_CHUNK_STRING,  "unknown_1", -1 ),
              new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
              new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
              new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Walls
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_16_ARRAY = new ChunkDescriptor[28]
            {
                  new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                  new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                  new ChunkDescriptor( 0x91EDBEE1, T_CHUNK_TYPE.T_CHUNK_INT,    "display_image_in_editor", -1 ),
                  new ChunkDescriptor( 0x9DF2CFD1, T_CHUNK_TYPE.T_CHUNK_COLOR,    "top_color", -1 ),
                  new ChunkDescriptor( 0xA2F4C9D1, T_CHUNK_TYPE.T_CHUNK_STRING,  "top_texture", -1 ),
                  new ChunkDescriptor( 0x91EDCFE2, T_CHUNK_TYPE.T_CHUNK_INT,    "cookie_cut", -1 ),
                  new ChunkDescriptor( 0x95F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_map_the_top", -1 ),
                  new ChunkDescriptor( 0x9DF2CFD2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "side_color", -1 ),
                  new ChunkDescriptor( 0xA2F4C9D2, T_CHUNK_TYPE.T_CHUNK_STRING,  "side_texture", -1 ),
                  new ChunkDescriptor( 0x96F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_map_the_side", -1 ),
                  new ChunkDescriptor( 0x97F2C4DF, T_CHUNK_TYPE.T_CHUNK_INT,    "flat_shading", -1 ),
                  new ChunkDescriptor( 0x9C00C0D1, T_CHUNK_TYPE.T_CHUNK_INT,    "transparency", -1 ),
                  new ChunkDescriptor( 0xA2F8CDDD, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "height", -1 ),
                  new ChunkDescriptor( 0x95FDC9CE, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "width", -1 ),
                  new ChunkDescriptor( 0x99E8BED8, T_CHUNK_TYPE.T_CHUNK_VALUELIST,  "material_type", -1,VL_MATERIAL_ARRAY ),
                  new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                  new ChunkDescriptor( 0x99F2C0E1, T_CHUNK_TYPE.T_CHUNK_INT,    "dropped", -1 ),
                  new ChunkDescriptor( 0x9DF5C3E2, T_CHUNK_TYPE.T_CHUNK_INT,    "collidable", -1 ),
                  new ChunkDescriptor( 0x97FDC4D3, T_CHUNK_TYPE.T_CHUNK_INT,    "render_object", -1 ),
                  new ChunkDescriptor( 0x95EBCDDD, T_CHUNK_TYPE.T_CHUNK_INT,    "generate_hit_event", -1 ),
                  new ChunkDescriptor( 0x95F3C2D2, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "shape_point", -1 ),
                  new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                    new ChunkDescriptor( 0xA1EDC5D2, T_CHUNK_TYPE.T_CHUNK_INT,    "smooth", -1 ),
                  new ChunkDescriptor( 0xA2F3C6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_1", -1 ),
                  new ChunkDescriptor( 0xA8FCC6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_2", -1 ),
                  new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                  new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                  new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };


            /// </summary>
            /// Decal
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_18_ARRAY = new ChunkDescriptor[13]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x95FDC9CE, T_CHUNK_TYPE.T_CHUNK_INT,    "width", -1 ),
                new ChunkDescriptor( 0xA2F8CDDD, T_CHUNK_TYPE.T_CHUNK_INT,    "height", -1 ),
                new ChunkDescriptor( 0x9C00C0D1, T_CHUNK_TYPE.T_CHUNK_INT,    "transparency", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA6F2C6D3, T_CHUNK_TYPE.T_CHUNK_INT, "object_appers_on", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// </summary>
            /// Kicker
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_19_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xBAB2BEB2, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0xB1BABCAA, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0xB3BBB0B3, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xB8BEB2B7, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0xADB3B0BD, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xBEABB0AE, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0xAFA6ABB5, T_CHUNK_TYPE.T_CHUNK_INT,    "type", -1 ),
                new ChunkDescriptor( 0xB7B8ABAD, T_CHUNK_TYPE.T_CHUNK_INT,    "strength", -1 ),
                new ChunkDescriptor( 0xBBB0B2AE, T_CHUNK_TYPE.T_CHUNK_INT,    "render_model", -1 ),
                new ChunkDescriptor( 0xBBB1ACB5, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_hit", -1 ),
                new ChunkDescriptor( 0xB4BCB0B4, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0xA6BEB3BF, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xBDBBB1BB, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// </summary>
            /// Lane Guides
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_20_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x96FBCCD6, T_CHUNK_TYPE.T_CHUNK_INT,    "offset", -1 ),
                new ChunkDescriptor( 0x96E8C0E2, T_CHUNK_TYPE.T_CHUNK_INT,    "crystal", -1 ),
                new ChunkDescriptor( 0x9A00C5E0, T_CHUNK_TYPE.T_CHUNK_STRING,  "unknown_1", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC, "end", -1 )
            };

            /// </summary>
            /// Model Rubbers
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_21_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x96FBCCD6, T_CHUNK_TYPE.T_CHUNK_INT,    "offset", -1 ),
                new ChunkDescriptor( 0x9700C6E0, T_CHUNK_TYPE.T_CHUNK_INT,    "elasticity", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC, "end", -1 )
            };
            

            /// </summary>
            /// Triggers
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_22_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0xA3F1C3D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sits_on_playfield", -1 ),
                new ChunkDescriptor( 0xA5F2C5D3, T_CHUNK_TYPE.T_CHUNK_INT,    "render_model", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0xA5F3BFD1, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_hit", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// </summary>
            /// Flashers
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_23_ARRAY = new ChunkDescriptor[]
            {
              new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
              new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
              new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
              new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
              new ChunkDescriptor( 0x9DF2CFD9, T_CHUNK_TYPE.T_CHUNK_COLOR,    "lit_color", -1 ),
              new ChunkDescriptor( 0xA6ECBFE4, T_CHUNK_TYPE.T_CHUNK_INT,    "auto_set_unlit_color", -1 ),
              new ChunkDescriptor( 0x9DF2CFD0, T_CHUNK_TYPE.T_CHUNK_COLOR,    "unlit_color", -1 ),
              new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
              new ChunkDescriptor( 0xA1FDC0D6, T_CHUNK_TYPE.T_CHUNK_INT,    "ordered_halo_glow", -1 ),
              new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
              new ChunkDescriptor( 0x97ECBFD3, T_CHUNK_TYPE.T_CHUNK_STRING,  "playfield", -1 ),
              new ChunkDescriptor( 0x9600BED2, T_CHUNK_TYPE.T_CHUNK_INT,    "state", -1 ),
              new ChunkDescriptor( 0x95F3C9E3, T_CHUNK_TYPE.T_CHUNK_INT,    "blink_interval", -1 ),
              new ChunkDescriptor( 0x9600C2E3, T_CHUNK_TYPE.T_CHUNK_STRING,  "blink_pattern", -1 ),
              new ChunkDescriptor( 0x9134D9F8, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_1", -1 ),
              new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
              new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
              new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// </summary>
            /// Wire Guide
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_24_ARRAY = new ChunkDescriptor[17]
            {
              new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
              new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
              new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
              new ChunkDescriptor( 0xA4FAC5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
              new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_map", -1 ),
              new ChunkDescriptor( 0xA2F8CDDD, T_CHUNK_TYPE.T_CHUNK_INT,    "height", -1 ),
              new ChunkDescriptor( 0x95FDC9CE, T_CHUNK_TYPE.T_CHUNK_INT,    "width", -1 ),
              new ChunkDescriptor( 0xA8F3C0D6, T_CHUNK_TYPE.T_CHUNK_INT,    "mark_as_ornamental", -1 ),
              new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
              new ChunkDescriptor( 0x95F3C2D2, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "shape_point", -1 ),
              new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
              new ChunkDescriptor( 0xA1EDC5D2, T_CHUNK_TYPE.T_CHUNK_INT,    "smooth", -1 ),
              new ChunkDescriptor( 0xA2F3C6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_1", -1 ),
              new ChunkDescriptor( 0xA8FCC6D2, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_2", -1 ),
              new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
              new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
              new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// </summary>
            /// Overlays
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_27_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BEFBED3, T_CHUNK_TYPE.T_CHUNK_INT,    "render_onto_translite", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA2F8CDDD, T_CHUNK_TYPE.T_CHUNK_INT,    "height", -1 ),
                new ChunkDescriptor( 0x95FDC9CE, T_CHUNK_TYPE.T_CHUNK_INT,    "width", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x96F5C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "image_list", -1 ),
                new ChunkDescriptor( 0x95F3C9D0, T_CHUNK_TYPE.T_CHUNK_INT,    "update_interval", -1 ),
                new ChunkDescriptor( 0xA6F2C6D3, T_CHUNK_TYPE.T_CHUNK_INT, "object_appers_on", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Bulb
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_29_ARRAY = new ChunkDescriptor[21]
                {
                  new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                  new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                  new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                  new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                  new ChunkDescriptor( 0xA5F2C5D3, T_CHUNK_TYPE.T_CHUNK_INT,    "render_model", -1 ),
                  new ChunkDescriptor( 0x96F3CDD9, T_CHUNK_TYPE.T_CHUNK_STRING,  "lens_texture", -1 ),
                  new ChunkDescriptor( 0x96FDD1D3, T_CHUNK_TYPE.T_CHUNK_INT,    "glow_radius", -1 ),
                  new ChunkDescriptor( 0xA1FDC0D6, T_CHUNK_TYPE.T_CHUNK_INT,    "ordered_halo_glow", -1 ),
                  new ChunkDescriptor( 0x9DF2CFD9, T_CHUNK_TYPE.T_CHUNK_COLOR,    "lit_color", -1 ),
                  new ChunkDescriptor( 0xA6ECBFE4, T_CHUNK_TYPE.T_CHUNK_INT,    "auto_set_unlit_color", -1 ),
                  new ChunkDescriptor( 0x9DF2CFD0, T_CHUNK_TYPE.T_CHUNK_COLOR,    "unlit_color", -1 ),
                  new ChunkDescriptor( 0x96E8C0E2, T_CHUNK_TYPE.T_CHUNK_INT,    "crystal", -1 ),
                  new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                  new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                  new ChunkDescriptor( 0x9600BED2, T_CHUNK_TYPE.T_CHUNK_INT,    "state", -1 ),
                  new ChunkDescriptor( 0x95F3C9E3, T_CHUNK_TYPE.T_CHUNK_INT,    "blink_interval", -1 ),
                  new ChunkDescriptor( 0x9600C2E3, T_CHUNK_TYPE.T_CHUNK_STRING,  "blink_pattern", -1 ),
                  new ChunkDescriptor( 0xA6F2C6D3, T_CHUNK_TYPE.T_CHUNK_INT, "object_appers_on", -1 ),
                  new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                  new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                  new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC, "end", -1 )
                };

            /// <summary>
            /// Gates
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_30_ARRAY = new ChunkDescriptor[14]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x9100BBD6, T_CHUNK_TYPE.T_CHUNK_INT,    "one_way", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x97ECBFD3, T_CHUNK_TYPE.T_CHUNK_STRING,  "playfield", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };



            /// <summary>
            /// Toys
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_33_ARRAY = new ChunkDescriptor[20]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0x9C00C0D1, T_CHUNK_TYPE.T_CHUNK_INT,    "transparency", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x96FBCCD6, T_CHUNK_TYPE.T_CHUNK_INT,    "offset", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x95F3C9D0, T_CHUNK_TYPE.T_CHUNK_INT,    "update_interval", -1 ),
                new ChunkDescriptor( 0x97ECBFD3, T_CHUNK_TYPE.T_CHUNK_STRING,  "playfield", -1 ),
                new ChunkDescriptor( 0x9035D306, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_hit", -1 ),
                new ChunkDescriptor( 0x9035D2F8, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_reset", -1 ),
                new ChunkDescriptor( 0x8035E308, T_CHUNK_TYPE.T_CHUNK_INT,    "bank_count", -1 ),
                new ChunkDescriptor( 0x9133D308, T_CHUNK_TYPE.T_CHUNK_INT,    "bank_spacing", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Diverters
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_43_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA900BED2, T_CHUNK_TYPE.T_CHUNK_INT,    "start_angle", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0xA2EABFE4, T_CHUNK_TYPE.T_CHUNK_INT,    "swing", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x9035D306, T_CHUNK_TYPE.T_CHUNK_STRING,  "solenoid", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// AutoPlunger
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_46_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0xA1FABED2, T_CHUNK_TYPE.T_CHUNK_INT,    "strength", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x97ECBFD3, T_CHUNK_TYPE.T_CHUNK_STRING,  "playfield", -1 ),
                new ChunkDescriptor( 0x95ECCFCF, T_CHUNK_TYPE.T_CHUNK_INT,    "include_v_cut", -1 ),
                new ChunkDescriptor( 0x90E9CFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "v_cut_position", -1 ),
                new ChunkDescriptor( 0x9BFCC6CF, T_CHUNK_TYPE.T_CHUNK_INT,    "v_cut_lenght", -1 ),
                new ChunkDescriptor( 0xA2F4C9CF, T_CHUNK_TYPE.T_CHUNK_STRING,  "v_cut_texture", -1 ),
                new ChunkDescriptor( 0x9DF2CFCF, T_CHUNK_TYPE.T_CHUNK_COLOR,    "v_cut_color", -1 ),
                new ChunkDescriptor( 0xA5F3BFE4, T_CHUNK_TYPE.T_CHUNK_STRING,  "solenoid", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Popups
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_50_ARRAY = new ChunkDescriptor[]
            {
                  new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                  new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                  new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                  new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                  new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                  new ChunkDescriptor( 0x96E8C0E2, T_CHUNK_TYPE.T_CHUNK_INT,    "crystal", -1 ),
                  new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
                  new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                  new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                  new ChunkDescriptor( 0x96FBCCD6, T_CHUNK_TYPE.T_CHUNK_INT,    "offset", -1 ),
                  new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                  new ChunkDescriptor( 0x97ECBFD3, T_CHUNK_TYPE.T_CHUNK_STRING,  "playfield", -1 ),
                  new ChunkDescriptor( 0xA5F3BFD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "solenoid", -1 ),
                  new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                  new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                  new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// ModelRamps
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_51_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA300C5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0x99F4C2D2, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_FLOAT,    "rotation", -1 ),
                new ChunkDescriptor( 0x96FBCCD6, T_CHUNK_TYPE.T_CHUNK_INT,    "offset", -1 ),
                new ChunkDescriptor( 0x9C00C0D1, T_CHUNK_TYPE.T_CHUNK_INT,    "transparency", -1 ),
                new ChunkDescriptor( 0x9DECCFE1, T_CHUNK_TYPE.T_CHUNK_INT,    "disable_culling", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0x97ECBFD3, T_CHUNK_TYPE.T_CHUNK_STRING,  "playfield", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Wire Ramp?
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_53_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xBAB2BEB2, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0xADB3B0BD, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xBAB8B2B7, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0xB8B6B7AD, T_CHUNK_TYPE.T_CHUNK_INT,  "start_height", -1 ),
                new ChunkDescriptor( 0xB8B6B7BB, T_CHUNK_TYPE.T_CHUNK_INT,  "end_height", -1 ),
                new ChunkDescriptor( 0xACBAA8AE, T_CHUNK_TYPE.T_CHUNK_STRING,  "start_model", -1 ),
                new ChunkDescriptor( 0xBABAA8AE, T_CHUNK_TYPE.T_CHUNK_STRING,  "end_model", -1 ),
                new ChunkDescriptor( 0xB3B9BAAE, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0xB4BCB0B4, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0xA6BEB3BF, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xABB1AFAD, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "ramp_point", -1 ),
                    new ChunkDescriptor( 0xB1BABCAA, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                    new ChunkDescriptor( 0xB7ABB2AD, T_CHUNK_TYPE.T_CHUNK_INT,    "smooth", -1 ),
                    new ChunkDescriptor( 0xBAB3B6B3, T_CHUNK_TYPE.T_CHUNK_INT,    "left_guide", -1 ),
                    new ChunkDescriptor( 0xBAB3AFAB, T_CHUNK_TYPE.T_CHUNK_INT,    "left_upper_guide", -1 ),
                    new ChunkDescriptor( 0xB6ADB6B3, T_CHUNK_TYPE.T_CHUNK_INT,    "right_guide", -1 ),
                    new ChunkDescriptor( 0xB6ADAFAB, T_CHUNK_TYPE.T_CHUNK_INT,    "right_upper_guide", -1 ),
                    new ChunkDescriptor( 0xABAAB0AC, T_CHUNK_TYPE.T_CHUNK_INT,    "top_wire", -1 ),
                    new ChunkDescriptor( 0xB0AFB7BB, T_CHUNK_TYPE.T_CHUNK_INT,    "mark_as_ramp_end_point", -1 ),
                    new ChunkDescriptor( 0xB8B1B6AE, T_CHUNK_TYPE.T_CHUNK_INT,    "ring_type", -1 ),
                    new ChunkDescriptor( 0xBDBBB1BB, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Ramp?
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_55_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xBAB2BEB2, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0xADB3B0BD, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xB9B0ADB0, T_CHUNK_TYPE.T_CHUNK_INT,  "ramp_profile", -1 ),
                new ChunkDescriptor( 0xBAB8B2B7, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture", -1 ),
                new ChunkDescriptor( 0xB8B6B7AD, T_CHUNK_TYPE.T_CHUNK_INT,  "start_height", -1 ),
                new ChunkDescriptor( 0xBBB6A8AD, T_CHUNK_TYPE.T_CHUNK_INT,  "start_width", -1 ),
                new ChunkDescriptor( 0xB8B6B7BB, T_CHUNK_TYPE.T_CHUNK_INT,  "end_height", -1 ),
                new ChunkDescriptor( 0xBBB6A8BB, T_CHUNK_TYPE.T_CHUNK_INT,  "end_width", -1 ),
                new ChunkDescriptor( 0xB8B6B7B4, T_CHUNK_TYPE.T_CHUNK_INT,  "left_side_height", -1 ),
                new ChunkDescriptor( 0xB8B6B7AE, T_CHUNK_TYPE.T_CHUNK_INT,  "right_side_height", -1 ),
                new ChunkDescriptor( 0xB3B9BAAE, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0xAFB2AFAD, T_CHUNK_TYPE.T_CHUNK_INT,    "sphere_mapping", -1 ),
                new ChunkDescriptor( 0xB1BEADAC, T_CHUNK_TYPE.T_CHUNK_INT,    "transparency", -1 ),
                new ChunkDescriptor( 0xB4BCB0B4, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0xA6BEB3BF, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xABB1AFAD, T_CHUNK_TYPE.T_CHUNK_CHUNKLIST, "ramp_point", -1 ),
                    new ChunkDescriptor( 0xB1BABCAA, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                    new ChunkDescriptor( 0xB7ABB2AD, T_CHUNK_TYPE.T_CHUNK_INT,    "smooth", -1 ),
                    new ChunkDescriptor( 0xBAB3B6B3, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_2", -1 ),
                    new ChunkDescriptor( 0xBAB3AFAB, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_3", -1 ),
                    new ChunkDescriptor( 0xB6ADB6B3, T_CHUNK_TYPE.T_CHUNK_INT,    "unknown_4", -1 ),
                    new ChunkDescriptor( 0xBDBBB1BB, T_CHUNK_TYPE.T_CHUNK_GENERIC,  "end", -1 )
            };

            /// <summary>
            /// Trigger Optos
            /// </summary>
            public static ChunkDescriptor[] CHUNKS_ELEMENT_61_ARRAY = new ChunkDescriptor[]
            {
                new ChunkDescriptor( 0xA4F4D1D7, T_CHUNK_TYPE.T_CHUNK_WSTRING,  "name", -1 ),
                new ChunkDescriptor( 0x9BFCCFCF, T_CHUNK_TYPE.T_CHUNK_VECTOR2D,  "position", -1 ),
                new ChunkDescriptor( 0x9DFDC3D8, T_CHUNK_TYPE.T_CHUNK_STRING,  "model", -1 ),
                new ChunkDescriptor( 0xA3EFBDD2, T_CHUNK_TYPE.T_CHUNK_STRING,  "surface", -1 ),
                new ChunkDescriptor( 0xA6FAC5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture_collector", -1 ),
                new ChunkDescriptor( 0xA4FAC5DC, T_CHUNK_TYPE.T_CHUNK_STRING,  "texture_emitter", -1 ),
                new ChunkDescriptor( 0x9CEBC4DC, T_CHUNK_TYPE.T_CHUNK_INT,    "invert", -1 ),
                new ChunkDescriptor( 0x97F5C3E2, T_CHUNK_TYPE.T_CHUNK_COLOR,    "color", -1 ),
                new ChunkDescriptor( 0xA8EDC3D3, T_CHUNK_TYPE.T_CHUNK_INT,    "rotation", -1 ),
                new ChunkDescriptor( 0x95FDC9CE, T_CHUNK_TYPE.T_CHUNK_INT,    "beam_width", -1 ),
                new ChunkDescriptor( 0x9DFBCDD3, T_CHUNK_TYPE.T_CHUNK_INT,    "reflects_off_playfield", -1 ),
                new ChunkDescriptor( 0xA5F3BFD1, T_CHUNK_TYPE.T_CHUNK_STRING,  "sound_when_hit", -1 ),
                new ChunkDescriptor( 0x9EFEC3D9, T_CHUNK_TYPE.T_CHUNK_INT,    "locked", -1 ),
                new ChunkDescriptor( 0x9100C6E4, T_CHUNK_TYPE.T_CHUNK_INT,    "layer", -1 ),
                new ChunkDescriptor( 0xA7FDC4E0, T_CHUNK_TYPE.T_CHUNK_GENERIC, "end", -1 )
            };
            
        };
    }

}
