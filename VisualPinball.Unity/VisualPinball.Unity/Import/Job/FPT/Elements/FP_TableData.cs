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
using System.Collections.Generic;
using VisualPinball.Engine.Math;

namespace VisualPinball.Unity.FP
{
    public class FP_TableData
    {
        public int width;
        public int length;
        public int front_glass_height;
        public int rear_glass_height;
        public float slope;
        public Color playfield_color;
        public string playfield_texture;
        public Color cabinet_wood_color;
        public Color button_color;
        public Color translite_color;
        public string translite_image;
        public int glossiness;
        public int translite_width;
        public int translite_height;
        public int machine_type;
        public string cabinet_texture;
        public string poster_image;
        public float table_center_line;
        public float table_flipper_line;
        public int warnings_before_tilt;
        //public int display_grid_in_editor;
        public int grid_size;
        //public int display_playfield_in_editor;
        //public int display_translite_in_editor;
        public int unknown_3;
        public int unknown_4_color;
        public int unknown_5;
        public string table_name;
        public string version;
        public string table_authors;
        public string release_date;
        public string mail;
        public string web_page;
        public string description;
        public int rules_len;
        //new ChunkDescriptor( , T_CHUNK_TYPE.T_CHUNK_RAWDATA,"rules;			
        public string loading_picture;
        //new ChunkDescriptor( , T_CHUNK_TYPE.T_CHUNK_COLOR,  "loading_color;
        public int ball_per_game;
        public int initial_jackpot;
        public string high_scores_default_initial_1;
        public int high_scores_default_score_1;
        public string high_scores_default_initial_2;
        public int high_scores_default_score_2;
        public string high_scores_default_initial_3;
        public int high_scores_default_score_3;
        public string high_scores_default_initial_4;
        public int high_scores_default_score_4;

        public string special_score_title;
        public int special_score_value;

        //new ChunkDescriptor( , T_CHUNK_TYPE.T_CHUNK_RAWDATA,"unknown_6;

        public string special_score_text;

        public string table_element_name;

        public int count_table_elements;
        public int count_images;
        public int count_sounds;
        public int count_musics;
        public int count_pin_models;
        public int count_image_lists;
        public int count_light_lists;
        public int count_dmd_fonts;
        //SK1  new ChunkDescriptor( 0xA4FDC3E2, T_CHUNK_GENERIC,  "unknown_15;
        //new ChunkDescriptor( , T_CHUNK_TYPE.T_CHUNK_SCRIPT, "script;   //
        //SK1  new ChunkDescriptor( 0x4F5A4C7A, T_CHUNK_RAWDATA,  "script;

        public float translate_x;
        public float translate_y;
        public float scale_x;
        public float scale_y;
        public float translite_translate_x;
        public float translite_translate_y;
        public float translite_scale_x;
        public float translite_scale_y;


		public Engine.VPT.Table.TableData ToVpx()
		{
			var td = new Engine.VPT.Table.TableData();

			td.Name = table_name;
			td.Left = 0F;
			td.Right = FptUtils.mm2VpUnits(width);
			td.Top = 0F;
			td.Bottom = FptUtils.mm2VpUnits(length);

			//td.Offset = new float[] { translate_x, translate_y };

			td.AngleTiltMin = slope;
			td.AngleTiltMax = slope;

			td.GlassHeight = rear_glass_height; // ? or front?

			td.Image = playfield_texture; //?
			
			// TODO
			//td.Notes

			td.NumGameItems = count_table_elements;
			td.NumSounds = count_sounds; // + count_musics;
			td.NumTextures = count_images;
			td.NumFonts = count_dmd_fonts;
			td.NumCollections = count_image_lists; //?

			// Todo: depends on machine type??
			// td.PlayfieldMaterial

			// TODO:add default?
			// td.NumMaterials
			// td.Materials


			return td;
		}

		public Dictionary<string, string> PopulateTableInfo()
		{
			Dictionary<string, string> infos = new Dictionary<string, string>();

			infos.Add("TableName", table_name);
			infos.Add("AuthorName", table_authors);
			infos.Add("ReleaseDate", release_date);
			infos.Add("AuthorEmail", mail);
			infos.Add("AuthorWebSite", web_page);
			infos.Add("TableVersion", version);
			infos.Add("TableDescription", description);
		
			// TODO: rules

			return infos;
		}
	}
}
