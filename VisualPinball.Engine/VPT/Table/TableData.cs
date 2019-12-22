#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;

namespace VisualPinball.Engine.VPT.Table
{
	public class TableData : ItemData
	{
		[Biff("LEFT")]
		public float Left;

		[Biff("RGHT")]
		public float Right;

		[Biff("TOPX")]
		public float Top;

		[Biff("BOTM")]
		public float Bottom;

		[Biff("EFSS")]
		public bool BgEnableFss;
		
		[Biff("ORRP")]
		public bool OverridePhysics;
		
		[Biff("ORPF")]
		public bool OverridePhysicsFlipper;
		
		[Biff("GAVT")]
		public float Gravity;
		
		[Biff("FRCT")]
		public float Friction;
		
		[Biff("ELAS")]
		public float Elasticity;
		
		[Biff("ELFA")]
		public float ElasticityFalloff;
	
		[Biff("PFSC")]
		public float Scatter;
	
		[Biff("SCAT")]
		public float DefaultScatter;

		[Biff("NDGT")]
		public float NudgeTime;

		[Biff("MPGC")]
		public int PlungerNormalize;

		[Biff("MPDF")]
		public bool PlungerFilter;

		[Biff("PHML")]
		public int PhysicsMaxLoops;

		[Biff("DECL")]
		public bool RenderDecals;

		[Biff("REEL")]
		public bool RenderEmReels;

		[Biff("OFFX", Index = 0)]
		[Biff("OFFY", Index = 1)]
		public float[] Offset = new float[2];

		[Biff("ZOOM")]
		public float Zoom;

		[Biff("MAXS")]
		public float _3DmaxSeparation;

		[Biff("ZPD")]
		public float _3DZPD;

		[Biff("STO")]
		public float _3DOffset;

		[Biff("OGST")]
		public bool OverwriteGlobalStereo3D;

		[Biff("SLPX")]
		public float AngleTiltMax;

		[Biff("SLOP")]
		public float AngletiltMin;

		[Biff("GLAS")]
		public float GlassHeight;

		[Biff("TBLH")]
		public float TableHeight;

		[Biff("IMAG")]
		public string Image;

		[Biff("BLIM")]
		public string BallImage;

		[Biff("BLIF")]
		public string BallImageFront;

		[Biff("SSHT")]
		public string ScreenShot;

		[Biff("FBCK")]
		public bool DisplayBackdrop;

		[Biff("SEDT")]
		public int NumGameItems;

		[Biff("SSND")]
		public int NumSounds;

		[Biff("SIMG")]
		public int NumTextures;

		[Biff("SFNT")]
		public int NumFonts;

		[Biff("SCOL")]
		public int NumCollections;

		[Biff("BIMN")]
		public bool ImageBackdropNightDay;

		[Biff("IMCG")]
		public string ImageColorGrade;

		[Biff("EIMG")]
		public string EnvImage;

		[Biff("PLMA")]
		public string PlayfieldMaterial;

		[Biff("LZAM")]
		public int LightAmbient;

		[Biff("LZDI")]
		public int Light0Emission { set => Light[0].Emission = new Color(value, ColorFormat.Bgr); }

		public readonly LightSource[] Light = { new LightSource() };

		[Biff("LZHI")]
		public float LightHeight;

		[Biff("LZRA")]
		public float LightRange;

		[Biff("LIES")]
		public float LightEmissionScale;

		[Biff("ENES")]
		public float EnvEmissionScale;

		[Biff("GLES")]
		public float GlobalEmissionScale;

		[Biff("AOSC")]
		public float AoScale;

		[Biff("SSSC")]
		public float SsrScale;

		[Biff("BREF")]
		public int UseReflectionForBalls;

		[Biff("PLST")]
		public int PlayfieldReflectionStrength; // m_playfieldReflectionStrength = dequantizeUnsigned<8>(tmp);

		[Biff("BTRA")]
		public int UseTrailForBalls;

		[Biff("BTST")]
		public int BallTrailStrength; // m_ballTrailStrength = dequantizeUnsigned<8>(tmp);

		[Biff("BPRS")]
		public float BallPlayfieldReflectionStrength;

		[Biff("DBIS")]
		public float DefaultBulbIntensityScaleOnBall;

		[Biff("UAAL")]
		public int UseAA;

		[Biff("UAOC")]
		public int UseAO;

		[Biff("USSR")]
		public int UseSSR;

		[Biff("UFXA")]
		public float UseFXAA; // TODO getting NaN here

		[Biff("BLST")]
		public float BloomStrength;

		[Biff("BCLR", ColorFormat = ColorFormat.Bgr)]
		public Color ColorBackdrop;

		[Biff("CCUS", Count = 16)]
		public uint[] Rgcolorcustom = new uint[16];

		[Biff("TDFT")]
		public float GlobalDifficulty;

		[Biff("SVOL")]
		public float TableSoundVolume;

		[Biff("BDMO")]
		public bool BallDecalMode;

		[Biff("MVOL")]
		public float TableMusicVolume;

		[Biff("AVSY")]
		public int TableAdaptiveVSync;

		[Biff("OGAC")]
		public bool OverwriteGlobalDetailLevel;

		[Biff("OGDN")]
		public bool OverwriteGlobalDayNight;

		[Biff("GDAC")]
		public bool ShowGrid;

		[Biff("REOP")]
		public bool ReflectElementsOnPlayfield;

		[Biff("ARAC")]
		public int UserDetailLevel;

		[Biff("MASI")]
		public int NumMaterials;

		[Biff("CODE", IsStreaming = true)]
		public string Code;
		
		[Biff("ROTA", Index = BackglassIndex.Desktop)]
		[Biff("ROTF", Index = BackglassIndex.Fullscreen)]
		[Biff("ROFS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgRotation = new float[3];
		
		[Biff("LAYB", Index = BackglassIndex.Desktop)]
		[Biff("LAYF", Index = BackglassIndex.Fullscreen)]
		[Biff("LAFS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgLayback = new float[3];
		
		[Biff("INCL", Index = BackglassIndex.Desktop)]
		[Biff("INCF", Index = BackglassIndex.Fullscreen)]
		[Biff("INFS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgInclination = new float[3];

		[Biff("FOVX", Index = BackglassIndex.Desktop)]
		[Biff("FOVF", Index = BackglassIndex.Fullscreen)]
		[Biff("FOFS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgFov = new float[3];

		[Biff("SCLX", Index = BackglassIndex.Desktop)]
		[Biff("SCFX", Index = BackglassIndex.Fullscreen)]
		[Biff("SCXS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgScaleX = new float[3];
		
		[Biff("SCLY", Index = BackglassIndex.Desktop)]
		[Biff("SCFY", Index = BackglassIndex.Fullscreen)]
		[Biff("SCYS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgScaleY = new float[3];

		[Biff("SCLZ", Index = BackglassIndex.Desktop)]
		[Biff("SCFZ", Index = BackglassIndex.Fullscreen)]
		[Biff("SCZS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgScaleZ = new float[3];

		[Biff("XLTX", Index = BackglassIndex.Desktop)]
		[Biff("XLFX", Index = BackglassIndex.Fullscreen)]
		[Biff("XLXS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgXlateX = new float[3];

		[Biff("XLTY", Index = BackglassIndex.Desktop)]
		[Biff("XLFY", Index = BackglassIndex.Fullscreen)]
		[Biff("XLYS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgXlateY = new float[3];

		[Biff("XLTZ", Index = BackglassIndex.Desktop)]
		[Biff("XLFZ", Index = BackglassIndex.Fullscreen)]
		[Biff("XLZS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgXlateZ = new float[3];
		
		[Biff("BIMG", Index = BackglassIndex.Desktop)]
		[Biff("BIMF", Index = BackglassIndex.Fullscreen)]
		[Biff("BIMS", Index = BackglassIndex.FullSingleScreen)]
		public readonly string[] BgImage = new string[3];

		[BiffMaterials("MATE")]
		[BiffMaterials("PHMA", IsPhysics = true)]
		public Material[] Materials;

		// other stuff
		public int BgCurrentSet = BackglassIndex.Desktop;

		static TableData()
		{
			Init(typeof(TableData), Attributes);
		}

		public TableData(BinaryReader reader) : base ("GameData")
		{
			Load(this, reader, Attributes);
		}

		private static readonly Dictionary<string, BiffAttribute> Attributes = new Dictionary<string, BiffAttribute>();
	}

	/// <summary>
	/// Parses material data.<p/>
	///
	/// Since we additionally need <see cref="TableData.NumMaterials"/> in
	/// order to know how many materials to parse, we can't use the standard
	/// BiffAttribute.
	/// </summary>
	public class BiffMaterialsAttribute : BiffAttribute
	{
		public bool IsPhysics;

		public BiffMaterialsAttribute(string name) : base(name) { }

		public override void Parse<T>(T obj, BinaryReader reader, int len)
		{
			if (obj is TableData tableData) {
				if (IsPhysics) {
					ParsePhysicsMaterial(tableData, reader, len);
				} else {
					ParseMaterial(tableData, reader, len);
				}
			} else {
				base.Parse(obj, reader, len);
			}
		}

		private void ParseMaterial(TableData tableData, BinaryReader reader, int len)
		{
			if (len < tableData.NumMaterials * SaveMaterial.Size) {
				throw new ArgumentOutOfRangeException($"Cannot parse {tableData.NumMaterials} of {tableData.NumMaterials * SaveMaterial.Size} bytes from a {len} bytes buffer.");
			}
			var materials = new Material[tableData.NumMaterials];
			for (var i = 0; i < tableData.NumMaterials; i++) {
				materials[i] = new Material(reader);
			}
			SetValue(tableData, materials);
		}

		private void ParsePhysicsMaterial(TableData tableData, BinaryReader reader, int len)
		{
			if (len < tableData.NumMaterials * SavePhysicsMaterial.Size) {
				throw new ArgumentOutOfRangeException($"Cannot parse {tableData.NumMaterials} physics materials of {tableData.NumMaterials * SavePhysicsMaterial.Size} bytes from a {len} bytes buffer.");
			}

			if (!(GetValue(tableData) is Material[] materials)) {
				throw new ArgumentException("Materials must be loaded before physics properties!");
			}
			for (var i = 0; i < tableData.NumMaterials; i++) {
				var savePhysMat = new SavePhysicsMaterial(reader);
				var material = materials.First(m => m.Name == savePhysMat.Name);
				if (material == null) {
					throw new Exception($"Cannot find material \"{savePhysMat.Name}\" in [{ string.Join(", ", tableData.Materials.Select(m => m.Name))} ] for updating physics.");
				}
				material.UpdatePhysics(savePhysMat);
			}
		}
	}

	public class LightSource {
		public Color Emission;
		public dynamic Pos; //: Vertex3D;
	}
}
