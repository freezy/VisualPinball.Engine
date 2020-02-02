#region ReSharper
// ReSharper disable UnassignedField.Global
// ReSharper disable StringLiteralTypo
// ReSharper disable FieldCanBeMadeReadOnly.Global
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VisualPinball.Engine.IO;
using VisualPinball.Engine.Math;


namespace VisualPinball.Engine.VPT.Table
{
	[Serializable]
	public class TableData : ItemData
	{
		[BiffString("NAME", IsWideString = true)]
		public override string Name { get; set; }

		[BiffFloat("LEFT")]
		public float Left;

		[BiffFloat("RGHT")]
		public float Right;

		[BiffFloat("TOPX")]
		public float Top;

		[BiffFloat("BOTM")]
		public float Bottom;

		[BiffBool("EFSS")]
		public bool BgEnableFss;

		[BiffBool("ORRP")]
		public bool OverridePhysics;

		[BiffBool("ORPF")]
		public bool OverridePhysicsFlipper;

		[BiffFloat("GAVT")]
		public float Gravity;

		[BiffFloat("FRCT")]
		public float Friction;

		[BiffFloat("ELAS")]
		public float Elasticity;

		[BiffFloat("ELFA")]
		public float ElasticityFalloff;

		[BiffFloat("PFSC")]
		public float Scatter;

		[BiffFloat("SCAT")]
		public float DefaultScatter;

		[BiffFloat("NDGT")]
		public float NudgeTime;

		[BiffInt("MPGC")]
		public int PlungerNormalize;

		[BiffBool("MPDF")]
		public bool PlungerFilter;

		[BiffInt("PHML")]
		public int PhysicsMaxLoops;

		[BiffBool("DECL")]
		public bool RenderDecals;

		[BiffBool("REEL")]
		public bool RenderEmReels;

		[BiffFloat("OFFX", Index = 0)]
		[BiffFloat("OFFY", Index = 1)]
		public float[] Offset = new float[2];

		[BiffFloat("ZOOM")]
		public float Zoom;

		[BiffFloat("MAXS")]
		public float StereoMaxSeparation;

		[BiffFloat("ZPD")]
		public float StereoZeroParallaxDisplacement;

		[BiffFloat("STO")]
		public float StereoOffset;

		[BiffBool("OGST")]
		public bool OverwriteGlobalStereo3D;

		[BiffFloat("SLPX")]
		public float AngleTiltMax;

		[BiffFloat("SLOP")]
		public float AngleTiltMin;

		[BiffFloat("GLAS")]
		public float GlassHeight;

		[BiffFloat("TBLH")]
		public float TableHeight;

		[BiffString("IMAG")]
		public string Image;

		[BiffString("BLIM")]
		public string BallImage;

		[BiffString("BLIF")]
		public string BallImageFront;

		[BiffString("SSHT")]
		public string ScreenShot;

		[BiffBool("FBCK")]
		public bool DisplayBackdrop;

		[BiffInt("SEDT")]
		public int NumGameItems;

		[BiffInt("SSND")]
		public int NumSounds;

		[BiffInt("SIMG")]
		public int NumTextures;

		[BiffInt("SFNT")]
		public int NumFonts;

		[BiffInt("SCOL")]
		public int NumCollections;

		[BiffBool("BIMN")]
		public bool ImageBackdropNightDay;

		[BiffString("IMCG")]
		public string ImageColorGrade;

		[BiffString("EIMG")]
		public string EnvImage;

		[BiffString("PLMA")]
		public string PlayfieldMaterial;

		[BiffColor("LZAM")]
		public Color LightAmbient;

		[BiffInt("LZDI")]
		public int Light0Emission { set => Light[0].Emission = new Color(value, ColorFormat.Bgr); }

		public readonly LightSource[] Light = { new LightSource() };

		[BiffFloat("LZHI")]
		public float LightHeight;

		[BiffFloat("LZRA")]
		public float LightRange;

		[BiffFloat("LIES")]
		public float LightEmissionScale;

		[BiffFloat("ENES")]
		public float EnvEmissionScale;

		[BiffFloat("GLES")]
		public float GlobalEmissionScale;

		[BiffFloat("AOSC")]
		public float AoScale;

		[BiffFloat("SSSC")]
		public float SsrScale;

		[BiffInt("BREF")]
		public int UseReflectionForBalls;

		[BiffFloat("PLST", QuantizedUnsignedBits = 8)]
		public float PlayfieldReflectionStrength;

		[BiffInt("BTRA")]
		public int UseTrailForBalls;

		[BiffFloat("BTST", QuantizedUnsignedBits = 8)]
		public float BallTrailStrength;

		[BiffFloat("BPRS")]
		public float BallPlayfieldReflectionStrength;

		[BiffFloat("DBIS")]
		public float DefaultBulbIntensityScaleOnBall;

		[BiffInt("UAAL")]
		public int UseAA;

		[BiffInt("UAOC")]
		public int UseAO;

		[BiffInt("USSR")]
		public int UseSSR;

		[BiffInt("UFXA")]
		public int UseFXAA;

		[BiffFloat("BLST")]
		public float BloomStrength;

		[BiffColor("BCLR", ColorFormat = ColorFormat.Bgr)]
		public Color ColorBackdrop;

		[BiffColor("CCUS", Count = 16)]
		public Color[] CustomColors = new Color[16];

		[BiffFloat("TDFT")]
		public float GlobalDifficulty;

		[BiffFloat("SVOL")]
		public float TableSoundVolume;

		[BiffBool("BDMO")]
		public bool BallDecalMode;

		[BiffFloat("MVOL")]
		public float TableMusicVolume;

		[BiffInt("AVSY")]
		public int TableAdaptiveVSync;

		[BiffBool("OGAC")]
		public bool OverwriteGlobalDetailLevel;

		[BiffBool("OGDN")]
		public bool OverwriteGlobalDayNight;

		[BiffBool("GDAC")]
		public bool ShowGrid;

		[BiffBool("REOP")]
		public bool ReflectElementsOnPlayfield;

		[BiffInt("ARAC")]
		public int UserDetailLevel;

		[BiffInt("MASI")]
		public int NumMaterials;

		[BiffString("CODE", IsStreaming = true)]
		public string Code;

		[BiffFloat("ROTA", Index = BackglassIndex.Desktop)]
		[BiffFloat("ROTF", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("ROFS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgRotation = new float[3];

		[BiffFloat("LAYB", Index = BackglassIndex.Desktop)]
		[BiffFloat("LAYF", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("LAFS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgLayback = new float[3];

		[BiffFloat("INCL", Index = BackglassIndex.Desktop)]
		[BiffFloat("INCF", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("INFS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgInclination = new float[3];

		[BiffFloat("FOVX", Index = BackglassIndex.Desktop)]
		[BiffFloat("FOVF", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("FOFS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgFov = new float[3];

		[BiffFloat("SCLX", Index = BackglassIndex.Desktop)]
		[BiffFloat("SCFX", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("SCXS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgScaleX = new float[3];

		[BiffFloat("SCLY", Index = BackglassIndex.Desktop)]
		[BiffFloat("SCFY", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("SCYS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgScaleY = new float[3];

		[BiffFloat("SCLZ", Index = BackglassIndex.Desktop)]
		[BiffFloat("SCFZ", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("SCZS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgScaleZ = new float[3];

		[BiffFloat("XLTX", Index = BackglassIndex.Desktop)]
		[BiffFloat("XLFX", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("XLXS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgOffsetX = new float[3];

		[BiffFloat("XLTY", Index = BackglassIndex.Desktop)]
		[BiffFloat("XLFY", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("XLYS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgOffsetY = new float[3];

		[BiffFloat("XLTZ", Index = BackglassIndex.Desktop)]
		[BiffFloat("XLFZ", Index = BackglassIndex.Fullscreen)]
		[BiffFloat("XLZS", Index = BackglassIndex.FullSingleScreen)]
		public readonly float[] BgOffsetZ = new float[3];

		[BiffString("BIMG", Index = BackglassIndex.Desktop)]
		[BiffString("BIMF", Index = BackglassIndex.Fullscreen)]
		[BiffString("BIMS", Index = BackglassIndex.FullSingleScreen)]
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

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();
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
			}
		}

		private void ParseMaterial(TableData tableData, BinaryReader reader, int len)
		{
			if (len < tableData.NumMaterials * MaterialData.Size) {
				throw new ArgumentOutOfRangeException($"Cannot parse {tableData.NumMaterials} of {tableData.NumMaterials * MaterialData.Size} bytes from a {len} bytes buffer.");
			}
			var materials = new Material[tableData.NumMaterials];
			for (var i = 0; i < tableData.NumMaterials; i++) {
				materials[i] = new Material(reader);
			}
			SetValue(tableData, materials);
		}

		private void ParsePhysicsMaterial(TableData tableData, BinaryReader reader, int len)
		{
			if (len < tableData.NumMaterials * PhysicsMaterialData.Size) {
				throw new ArgumentOutOfRangeException($"Cannot parse {tableData.NumMaterials} physics materials of {tableData.NumMaterials * PhysicsMaterialData.Size} bytes from a {len} bytes buffer.");
			}

			if (!(GetValue(tableData) is Material[] materials)) {
				throw new ArgumentException("Materials must be loaded before physics properties!");
			}
			for (var i = 0; i < tableData.NumMaterials; i++) {
				var savePhysMat = new PhysicsMaterialData(reader);
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
		public Vertex3D Pos;
	}
}
