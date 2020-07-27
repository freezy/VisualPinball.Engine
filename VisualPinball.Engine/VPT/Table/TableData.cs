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
		public override string GetName() => Name;
		public override void SetName(string name) { Name = name; }


		[BiffString("NAME", IsWideString = true, Pos = 112)]
		public string Name;

		[BiffFloat("LEFT", Pos = 1)]
		public float Left;

		[BiffFloat("RGHT", Pos = 3)]
		public float Right;

		[BiffFloat("TOPX", Pos = 2)]
		public float Top;

		[BiffFloat("BOTM", Pos = 4)]
		public float Bottom;

		[BiffBool("EFSS", Pos = 15)]
		public bool BgEnableFss;

		[BiffInt("ORRP", Pos = 36)]
		public int OverridePhysics;

		[BiffBool("ORPF", Pos = 37)]
		public bool OverridePhysicsFlipper;

		[BiffFloat("GAVT", Pos = 38)]
		public float Gravity;

		[BiffFloat("FRCT", Pos = 39)]
		public float Friction;

		[BiffFloat("ELAS", Pos = 40)]
		public float Elasticity;

		[BiffFloat("ELFA", Pos = 41)]
		public float ElasticityFalloff;

		[BiffFloat("PFSC", Pos = 42)]
		public float Scatter;

		[BiffFloat("SCAT", Pos = 43)]
		public float DefaultScatter;

		[BiffFloat("NDGT", Pos = 44)]
		public float NudgeTime;

		[BiffInt("MPGC", Pos = 45)]
		public int PlungerNormalize;

		[BiffBool("MPDF", Pos = 46)]
		public bool PlungerFilter;

		[BiffInt("PHML", Pos = 47)]
		public int PhysicsMaxLoops;

		[BiffBool("DECL", Pos = 49)]
		public bool RenderDecals;

		[BiffBool("REEL", Pos = 48)]
		public bool RenderEmReels;

		[BiffFloat("OFFX", Index = 0, Pos = 50)]
		[BiffFloat("OFFY", Index = 1, Pos = 51)]
		public float[] Offset = new float[2];

		[BiffFloat("ZOOM", Pos = 52)]
		public float Zoom;

		[BiffFloat("MAXS", Pos = 55)]
		public float StereoMaxSeparation;

		[BiffFloat("ZPD", Pos = 56)]
		public float StereoZeroParallaxDisplacement;

		[BiffFloat("STO", Pos = 57)]
		public float StereoOffset;

		[BiffBool("OGST", Pos = 58)]
		public bool OverwriteGlobalStereo3D;

		[BiffFloat("SLPX", Pos = 53)]
		public float AngleTiltMax;

		[BiffFloat("SLOP", Pos = 54)]
		public float AngleTiltMin;

		[BiffFloat("GLAS", Pos = 70)]
		public float GlassHeight;

		[BiffFloat("TBLH", Pos = 71)]
		public float TableHeight;

		[BiffString("IMAG", Pos = 59)]
		public string Image;

		[BiffString("BLIM", Pos = 65)]
		public string BallImage;

		[BiffString("BLIF", Pos = 66)]
		public string BallImageFront;

		[BiffString("SSHT", Pos = 68)]
		public string ScreenShot;

		[BiffBool("FBCK", Pos = 69)]
		public bool DisplayBackdrop;

		[BiffInt("SEDT", Pos = 107)]
		public int NumGameItems;

		[BiffInt("SSND", Pos = 108)]
		public int NumSounds;

		[BiffInt("SIMG", Pos = 109)]
		public int NumTextures;

		[BiffInt("SFNT", Pos = 110)]
		public int NumFonts;

		[BiffInt("SCOL", Pos = 111)]
		public int NumCollections;

		[BiffBool("BIMN", Pos = 63)]
		public bool ImageBackdropNightDay;

		[BiffString("IMCG", Pos = 64)]
		public string ImageColorGrade;

		[BiffString("EIMG", Pos = 67)]
		public string EnvImage;

		[MaterialReference]
		[BiffString("PLMA", Pos = 72)]
		public string PlayfieldMaterial;

		[BiffColor("LZAM", Pos = 75)]
		public Color LightAmbient;

		[BiffInt("LZDI", Pos = 76)]
		public int Light0Emission {
			set => Light[0].Emission = new Color(value, ColorFormat.Bgr);
			get => Light[0].Emission.Bgr;
		}
		public LightSource[] Light = { new LightSource() };

		[BiffFloat("LZHI", Pos = 77)]
		public float LightHeight;

		[BiffFloat("LZRA", Pos = 78)]
		public float LightRange;

		[BiffFloat("LIES", Pos = 79)]
		public float LightEmissionScale;

		[BiffFloat("ENES", Pos = 80)]
		public float EnvEmissionScale;

		[BiffFloat("GLES", Pos = 81)]
		public float GlobalEmissionScale;

		[BiffFloat("AOSC", Pos = 82)]
		public float AoScale;

		[BiffFloat("SSSC", Pos = 83)]
		public float SsrScale;

		[BiffInt("BREF", Pos = 87)]
		public int UseReflectionForBalls;

		[BiffFloat("PLST", QuantizedUnsignedBits = 8, Pos = 88)]
		public float PlayfieldReflectionStrength;

		[BiffInt("BTRA", Pos = 89)]
		public int UseTrailForBalls;

		[BiffFloat("BTST", QuantizedUnsignedBits = 8, Pos = 93)]
		public float BallTrailStrength;

		[BiffFloat("BPRS", Pos = 91)]
		public float BallPlayfieldReflectionStrength;

		[BiffFloat("DBIS", Pos = 92)]
		public float DefaultBulbIntensityScaleOnBall;

		[BiffInt("UAAL", Pos = 99)]
		public int UseAA;

		[BiffInt("UAOC", Pos = 101)]
		public int UseAO;

		[BiffInt("USSR", Pos = 102)]
		public int UseSSR;

		[BiffInt("UFXA", Pos = 100)]
		public int UseFXAA;

		[BiffFloat("BLST", Pos = 103)]
		public float BloomStrength;

		[BiffColor("BCLR", ColorFormat = ColorFormat.Bgr, Pos = 73)]
		public Color ColorBackdrop;

		[BiffColor("CCUS", Count = 16, Pos = 113)]
		public Color[] CustomColors = new Color[16];

		[BiffFloat("TDFT", Pos = 74)]
		public float GlobalDifficulty;

		[BiffFloat("SVOL", Pos = 84)]
		public float TableSoundVolume;

		[BiffBool("BDMO", Pos = 90)]
		public bool BallDecalMode;

		[BiffFloat("MVOL", Pos = 85)]
		public float TableMusicVolume;

		[BiffInt("AVSY", Pos = 86)]
		public int TableAdaptiveVSync;

		[BiffBool("OGAC", Pos = 95)]
		public bool OverwriteGlobalDetailLevel;

		[BiffBool("OGDN", Pos = 96)]
		public bool OverwriteGlobalDayNight;

		[BiffBool("GDAC", Pos = 97)]
		public bool ShowGrid;

		[BiffBool("REOP", Pos = 98)]
		public bool ReflectElementsOnPlayfield;

		[BiffInt("ARAC", Pos = 94)]
		public int UserDetailLevel;

		[BiffInt("MASI", Pos = 104)]
		public int NumMaterials;

		[BiffString("CODE", LengthAfterTag = true, Pos = 114)]
		public string Code;

		[BiffFloat("ROTA", Index = BackglassIndex.Desktop, Pos = 5)]
		[BiffFloat("ROTF", Index = BackglassIndex.Fullscreen, Pos = 16)]
		[BiffFloat("ROFS", Index = BackglassIndex.FullSingleScreen, Pos = 26)]
		public float[] BgRotation = new float[3];

		[BiffFloat("LAYB", Index = BackglassIndex.Desktop, Pos = 7)]
		[BiffFloat("LAYF", Index = BackglassIndex.Fullscreen, Pos = 18)]
		[BiffFloat("LAFS", Index = BackglassIndex.FullSingleScreen, Pos = 28)]
		public float[] BgLayback = new float[3];

		[BiffFloat("INCL", Index = BackglassIndex.Desktop, Pos = 6)]
		[BiffFloat("INCF", Index = BackglassIndex.Fullscreen, Pos = 17)]
		[BiffFloat("INFS", Index = BackglassIndex.FullSingleScreen, Pos = 27)]
		public float[] BgInclination = new float[3];

		[BiffFloat("FOVX", Index = BackglassIndex.Desktop, Pos = 8)]
		[BiffFloat("FOVF", Index = BackglassIndex.Fullscreen, Pos = 19)]
		[BiffFloat("FOFS", Index = BackglassIndex.FullSingleScreen, Pos = 29)]
		public float[] BgFov = new float[3];

		[BiffFloat("SCLX", Index = BackglassIndex.Desktop, Pos = 12)]
		[BiffFloat("SCFX", Index = BackglassIndex.Fullscreen, Pos = 23)]
		[BiffFloat("SCXS", Index = BackglassIndex.FullSingleScreen, Pos = 33)]
		public float[] BgScaleX = new float[3];

		[BiffFloat("SCLY", Index = BackglassIndex.Desktop, Pos = 13)]
		[BiffFloat("SCFY", Index = BackglassIndex.Fullscreen, Pos = 24)]
		[BiffFloat("SCYS", Index = BackglassIndex.FullSingleScreen, Pos = 34)]
		public float[] BgScaleY = new float[3];

		[BiffFloat("SCLZ", Index = BackglassIndex.Desktop, Pos = 14)]
		[BiffFloat("SCFZ", Index = BackglassIndex.Fullscreen, Pos = 25)]
		[BiffFloat("SCZS", Index = BackglassIndex.FullSingleScreen, Pos = 35)]
		public float[] BgScaleZ = new float[3];

		[BiffFloat("XLTX", Index = BackglassIndex.Desktop, Pos = 9)]
		[BiffFloat("XLFX", Index = BackglassIndex.Fullscreen, Pos = 20)]
		[BiffFloat("XLXS", Index = BackglassIndex.FullSingleScreen, Pos = 30)]
		public float[] BgOffsetX = new float[3];

		[BiffFloat("XLTY", Index = BackglassIndex.Desktop, Pos = 10)]
		[BiffFloat("XLFY", Index = BackglassIndex.Fullscreen, Pos = 21)]
		[BiffFloat("XLYS", Index = BackglassIndex.FullSingleScreen, Pos = 31)]
		public float[] BgOffsetY = new float[3];

		[BiffFloat("XLTZ", Index = BackglassIndex.Desktop, Pos = 11)]
		[BiffFloat("XLFZ", Index = BackglassIndex.Fullscreen, Pos = 22)]
		[BiffFloat("XLZS", Index = BackglassIndex.FullSingleScreen, Pos = 32)]
		public float[] BgOffsetZ = new float[3];

		[BiffString("BIMG", Index = BackglassIndex.Desktop, Pos = 60)]
		[BiffString("BIMF", Index = BackglassIndex.Fullscreen, Pos = 61)]
		[BiffString("BIMS", Index = BackglassIndex.FullSingleScreen, Pos = 62)]
		public string[] BgImage = new string[3];

		[BiffMaterials("MATE", Pos = 105)]
		[BiffMaterials("PHMA", IsPhysics = true, Pos = 106)]
		public Material[] Materials;

		// other stuff
		public int BgCurrentSet = BackglassIndex.Desktop;

		public const float OverrideContactFriction = 0.075f;
		public const float OverrideElasticity = 0.25f;
		public const float OverrideElasticityFalloff = 0f;
		public const float OverrideScatterAngle = 0f;

		public Rect3D BoundingBox => new Rect3D(Left, Right, Top, Bottom, TableHeight, GlassHeight);

		public float GetFriction() => OverridePhysics != 0 ? OverrideContactFriction : Friction;
		public float GetElasticity() => OverridePhysics != 0 ? OverrideElasticity : Elasticity;
		public float GetElasticityFalloff() => OverridePhysics != 0 ? OverrideElasticityFalloff : ElasticityFalloff;
		public float GetScatter() => OverridePhysics != 0 ? OverrideScatterAngle : Scatter;

		protected override bool SkipWrite(BiffAttribute attr)
		{
			switch (attr.Name) {
				case "LOCK":
				case "LAYR":
				case "LANR":
				case "LVIS":
					return true;
			}
			return false;
		}

		#region BIFF

		static TableData()
		{
			Init(typeof(TableData), Attributes);
		}

		public TableData() : base("GameData")
		{
		}

		public TableData(BinaryReader reader) : this()
		{
			Load(this, reader, Attributes);
		}

		public override void Write(BinaryWriter writer, HashWriter hashWriter)
		{
			WriteRecord(writer, Attributes, hashWriter);
			WriteEnd(writer, hashWriter);
		}

		private static readonly Dictionary<string, List<BiffAttribute>> Attributes = new Dictionary<string, List<BiffAttribute>>();

		#endregion
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
				throw new InvalidOperationException();
			}
		}

		public override void Write<TItem>(TItem obj, BinaryWriter writer, HashWriter hashWriter)
		{
			if (!(GetValue(obj) is Material[] materials)) {
				return;
			}
			using (var stream = new MemoryStream())
			using (var dataWriter = new BinaryWriter(stream)) {
				foreach (var material in materials) {
					if (IsPhysics) {
						material.PhysicsMaterialData.Write(dataWriter);
					} else {
						material.UpdateData();
						material.MaterialData.Write(dataWriter);
					}
				}

				var data = stream.ToArray();
				WriteStart(writer, data.Length, hashWriter);
				writer.Write(data);
				hashWriter?.Write(data);
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

	[Serializable]
	public class LightSource {
		public Color Emission;
		public Vertex3D Pos;
	}
}
