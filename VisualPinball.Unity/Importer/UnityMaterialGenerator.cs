using System;
using UnityEngine;
using VisualPinball.Unity.Importer;
using VisualPinball.Engine.Game;
using VisualPinball.Unity.Extensions;
namespace VisualPinball.Unity.Importer
{
	class UnityMaterialGenerator
	{


		private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

		private static readonly int Color = Shader.PropertyToID("_Color");
		private static readonly int Metallic = Shader.PropertyToID("_Metallic");
		private static readonly int Glossiness = Shader.PropertyToID("_Glossiness");
		private static readonly int Mode = Shader.PropertyToID("_Mode");
		private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
		private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
		private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

		private enum BlendMode
		{
			Opaque,
			Cutout,
			Fade,
			Transparent
		}


		public static UnityEngine.Material ToUnityMaterial(VisualPinball.Engine.VPT.Material vpxMaterial, RenderObject ro) {

			//this overload is called when the RenderObject has a valid Material

			ro.buildInfo = "";
			string newLineValue = "\r\n";			
			var unityMaterial = new UnityEngine.Material(Shader.Find("Standard")) {name = ro.MaterialIdFixed};
			ro.buildInfo +=  "id "+ro.MaterialIdFixed + newLineValue;
			ro.buildInfo += "------ToUnityMaterial called------" + newLineValue;
			ro.buildInfo += "HAS VPX MATERIAL BACKING " + newLineValue;

			// get the color from vpxMaterial
			var col = vpxMaterial.BaseColor.ToUnityColor();
		
			//apply some basic manipulations to the color , this just makes very very white colors be clipped to 0.8204  aka 204/255 is 0.8
			//this is to give room to lighting values. So there is more modulation of brighter colors when beng lit without blow outs too soon. 
			if (vpxMaterial.BaseColor.IsGray() && col.grayscale > 0.8) {
				col.r = col.g = col.b = 0.8f;
				//log manipulation to color
				ro.buildInfo += "color manipulation performed , brightness reduced "+ newLineValue;
			}

			//log color
			ro.buildInfo += "col " + col.ToString() + newLineValue;

			//apply color to unityMaterial
			unityMaterial.SetColor(Color, col);

			// validate IsMetal of vpxMaterial , if true set the Metallic value of unityMaterial;
			if (vpxMaterial.IsMetal) {
				unityMaterial.SetFloat(Metallic, 1f);
			}

			//log IsMetal
			ro.buildInfo += "IsMetal " + vpxMaterial.IsMetal + newLineValue;

			//set Glossiness of unityMaterial to Roughness of vpxMaterial
			unityMaterial.SetFloat(Glossiness, vpxMaterial.Roughness);

			//log vpxMaterial Roughness
			ro.buildInfo += "Roughness " + vpxMaterial.Roughness + newLineValue;

			//log vpxMaterial IsOpacityActive
			ro.buildInfo += "IsOpacityActive " + vpxMaterial.IsOpacityActive + newLineValue;

			//log vpxMaterial Opacity
			ro.buildInfo += "Opacity " + vpxMaterial.Opacity + newLineValue;

			// blend modes

			//this variable will hold the final state the shading of unityMaterial is set to
			//it gets set by conditionals using vpx material values but also can get set by conditionals concerning image stats reports
			var blendMode = BlendMode.Opaque;

			//the final value of image stats reports is stored on this variable. it is used to possibly alter the above final blendMode value
			var blendModePendingFromTextureAnalysis = BlendMode.Opaque;

			//if ro.Map is null set this to false so stats are ignored
			bool useStats = false;

			//get texture analysis values
			if (ro.Map != null) {
				ro.buildInfo += "ro.Map is not null stats could be used" + newLineValue;
				var stats = ro.Map?.GetStats(1000);
				ro.buildInfo += "GetStats called" + newLineValue;
				if (stats != null) {
					ro.buildInfo += "returned value of calling GetStats is not null" + newLineValue;
					ro.buildInfo += "ro.Map.HasTransparentPixels" + ro.Map.HasTransparentPixels + newLineValue;
					if (!stats.IsOpaque) {
						ro.buildInfo += "stats.IsOpaque is false" + newLineValue;

						//log the values return for Translucent and Transparent
						ro.buildInfo += "stats.Translucent " + stats.Translucent + newLineValue;
						ro.buildInfo += "stats.Transparent " + stats.Transparent + newLineValue;
						//set useStats to true so further code knows it can use the stats results
						useStats = true;
						if (stats.Transparent <= 0) {// stats.Transparent is sometimes zero so we cant just assum it is a valid number for division ,  so check this first
							if (stats.Translucent > 0) {
								blendModePendingFromTextureAnalysis = BlendMode.Transparent;
							}

						} else if (stats.Translucent <= 0) {// stats.Translucent is sometimes zero so we cant just assum it is a valid number for division ,  so check this first
							if (stats.Transparent > 0) {
								blendModePendingFromTextureAnalysis = BlendMode.Cutout;
							}
						} else {//stats.Transparent and stats.Translucent are both non zero at this point so the condition using division may be used
							blendModePendingFromTextureAnalysis = stats.Translucent / stats.Transparent > 0.1
							? BlendMode.Transparent
							: BlendMode.Cutout;
						}
						
					} else {
						ro.buildInfo += "stats.IsOpaque is true , meaning the inner conditional that could set blendModePendingFromTextureAnalysis to another value other that the default of Opaque was not reached, stats will be ignored" + newLineValue;
					}
				} else {
					ro.buildInfo += "returned value of calling GetStats is null, stats will be ignored" + newLineValue;
				}
			} else {				
				ro.buildInfo += "ro.Map is null, stats will be ignored" + newLineValue;
			}

			//log the value of blendModePendingFromTextureAnalysis now , nothing will cahange it past this point , it is only set in the conditionals above
			ro.buildInfo += "blendModePendingFromTextureAnalysis " + blendModePendingFromTextureAnalysis + newLineValue;

			//alpha threshold declared
			float alphaThreshold = 0.9f;
			//materialOpacity declared , nothing set here , its just a variable. i saw freezy use this logic in his Javascript build , I guess he encountered non normalized or negative values and clipped them to be valid 0-1 possitive values
			float materialOpacity = Mathf.Min(1, Mathf.Max(0, vpxMaterial.Opacity));

			//LET THE FUN AND GAMES BEGIN

			//first evaluate if IsOpacityActive is true on the vpxMaterial. this is the first place we cannot fully decide what the build should do because
			// 1. This is set for both transparent or cutout
			// 2. This is sometimes incorrectly set, it seems vpx uses the Opacity value and the texture alpha channel regardless of what this value is set to
			// 3. it is possible this is set to true while vpxMaterial.Opacity is 1, VPX does not differenciate betwwen color alpha and texture channel alpha			
			// we cannot do the same , just assuming transparency for everything , because we do not have a static camera and sorting issues will occur.


			if (vpxMaterial.IsOpacityActive) {
				// so IsOpacityActive is true , now evaluate the materialOpacity agaisnt a threshold
				//this is to filter out materials set will almost fully opque values and rather just treat them as opaque.
				//I have seen vpx materials set to 98% Opacity .. honestly you will never notice that unless you inspect it up close, our threshold is set to 90%
				if (materialOpacity < alphaThreshold) {
					ro.buildInfo += "IsOpacityActive is true and alphaThreshold was met" + newLineValue;
					// the threshold for materialOpacity was met , meaning Opacity less than 90% so now apply this to the color value
					//and set the unity material color again to update it to using this Opacity calue.
					//blendMode is changed to Transparent
					col.a = materialOpacity;
					unityMaterial.SetColor(Color, col);
					blendMode = BlendMode.Transparent;
					ro.buildInfo += "blendMode set to Transparent" + newLineValue;
				} else {
					ro.buildInfo += "IsOpacityActive is true but the alphaThreshold was NOT met" + newLineValue;
				}
			}

			//at this point blendMode is either the default opaque or transparent.
			//no try use vpxMaterial.Edge values to determine to rather use cutout

			ro.buildInfo += "vpxMaterial.Edge " + vpxMaterial.Edge + newLineValue;
			if (vpxMaterial.Edge < 1) {
				//has less than one edge value so assume cutout
				blendMode = BlendMode.Cutout;
				ro.buildInfo += "blendMode set to Cutout" + newLineValue;

				//now to reply on some image stats. If the texture does not have transparency , then revert the blendmode to opaque.
				if (ro.Map != null) {					
					if (!ro.Map.HasTransparentPixels) {						
						blendMode = BlendMode.Opaque;
						ro.buildInfo += "blendMode revert to Opaque within Edge conditionals because no transparent pixel were found via stats" + newLineValue;						
					}
				}

			}

			//at this point blendMode could be either of the 3 , opaque , transparent or cutout

			//now for some fallback on the stats. If the stats are valid firstly.
			//Due to the mentioned complexities of the VPX values 
			//the blendMode might still be opaque at this point but it does actually require one of the transparent states.
			// for this we will just use IsOpacityActive of true , regardless of the vpxMaterial.Opacity value.
			ro.buildInfo += "useStats " + useStats +newLineValue;
			if (useStats) {
				if (vpxMaterial.IsOpacityActive && blendMode == BlendMode.Opaque) {
					blendMode = blendModePendingFromTextureAnalysis;					
					ro.buildInfo += "blendMode set to " + blendModePendingFromTextureAnalysis + newLineValue;
				}
			} 

			// normal map
			if (ro.NormalMap != null) {
				ro.buildInfo += "has normal map  = true" + newLineValue;
				unityMaterial.EnableKeyword("_NORMALMAP");
			} else {
				ro.buildInfo += "has normal map  = false" + newLineValue;
			}

			ro.buildInfo += "final blendMode " + blendMode + newLineValue;
			ro.buildInfo += "********************" + newLineValue + newLineValue;

			//since we have two functions that share setting the shading properies of the unityMaterial , this is better in a single function they can both share
			SetMaterialShading(blendMode, unityMaterial);
			return unityMaterial;
		}



		public static UnityEngine.Material ToUnityMaterial(RenderObject ro) {

			//this overload is called when the RenderObject DOES NOT have a valid Material

			ro.buildInfo = "";
			string newLineValue = "\r\n";
			var unityMaterial = new UnityEngine.Material(Shader.Find("Standard")) {
				name = ro.MaterialIdFixed
			};

			ro.buildInfo += "id " + ro.MaterialIdFixed + newLineValue;
			ro.buildInfo += "------ToUnityMaterial called------" + newLineValue;
			ro.buildInfo += "NO VPX MATERIAL BACKING " + newLineValue;
			// blend modes
			var blendMode = BlendMode.Opaque;


			//get texture analysis values , this is different from the other overload as it directly sets the blendMode value
			//not stored because we have no VPXMaterial , we have no other data to evaluate besides this.
			if (ro.Map != null) {
				ro.buildInfo += "ro.Map is not null stats could be used" + newLineValue;
				var stats = ro.Map?.GetStats(1000);
				ro.buildInfo += "GetStats called" + newLineValue;
				if (stats != null) {
					ro.buildInfo += "returned value of calling GetStats is not null" + newLineValue;
					ro.buildInfo += "ro.Map.HasTransparentPixels" + ro.Map.HasTransparentPixels + newLineValue;
					if (!stats.IsOpaque) {
						ro.buildInfo += "stats.IsOpaque is false" + newLineValue;

						//log the values return for Translucent and Transparent
						ro.buildInfo += "stats.Translucent " + stats.Translucent + newLineValue;
						ro.buildInfo += "stats.Transparent " + stats.Transparent + newLineValue;
						//set useStats to true so further code knows it can use the stats results
						
						if (stats.Transparent <= 0) {// stats.Transparent is sometimes zero so we cant just assum it is a valid number for division ,  so check this first
							if (stats.Translucent > 0) {
								blendMode = BlendMode.Transparent;
							}

						} else if (stats.Translucent <= 0) {// stats.Translucent is sometimes zero so we cant just assum it is a valid number for division ,  so check this first
							if (stats.Transparent > 0) {
								blendMode = BlendMode.Cutout;
							}
						} else {//stats.Transparent and stats.Translucent are both non zero at this point so the condition using division may be used
							blendMode = stats.Translucent / stats.Transparent > 0.1
							? BlendMode.Transparent
							: BlendMode.Cutout;
						}

					} else {
						ro.buildInfo += "stats.IsOpaque is true , meaning the inner conditional that could set blendModePendingFromTextureAnalysis to another value other that the default of Opaque was not reached, stats will be ignored" + newLineValue;
					}
				} else {
					ro.buildInfo += "returned value of calling GetStats is null, stats will be ignored" + newLineValue;
				}
			} else {
				ro.buildInfo += "ro.Map is null, stats will be ignored" + newLineValue;
			}


			// normal map
			if (ro.NormalMap != null) {
				ro.buildInfo += "has normal map  = true" + newLineValue;
				unityMaterial.EnableKeyword("_NORMALMAP");
			} else {
				ro.buildInfo += "has normal map  = false" + newLineValue;
			}

			ro.buildInfo += "final blendMode " + blendMode + newLineValue;
			ro.buildInfo += "********************" + newLineValue + newLineValue;

			//since we have two functions that share setting the shading properies of the unityMaterial , this is better in a single function they can both share
			SetMaterialShading(blendMode, unityMaterial);
			return unityMaterial;
		}

		private static void SetMaterialShading(BlendMode mode, UnityEngine.Material unityMaterial) {

			// blend mode
			switch (mode) {
				case BlendMode.Opaque:
					unityMaterial.SetFloat(Mode, 0);
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
					unityMaterial.SetInt(ZWrite, 1);
					unityMaterial.DisableKeyword("_ALPHATEST_ON");
					unityMaterial.DisableKeyword("_ALPHABLEND_ON");
					unityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					unityMaterial.renderQueue = -1;
					break;

				case BlendMode.Cutout:
					unityMaterial.SetFloat(Mode, 1);
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.Zero);
					unityMaterial.SetInt(ZWrite, 1);
					unityMaterial.EnableKeyword("_ALPHATEST_ON");
					unityMaterial.DisableKeyword("_ALPHABLEND_ON");
					unityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					unityMaterial.renderQueue = 2450;
					break;

				case BlendMode.Fade:
					unityMaterial.SetFloat(Mode, 2);
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					unityMaterial.SetInt(ZWrite, 0);
					unityMaterial.DisableKeyword("_ALPHATEST_ON");
					unityMaterial.EnableKeyword("_ALPHABLEND_ON");
					unityMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
					unityMaterial.renderQueue = 3000;
					break;

				case BlendMode.Transparent:
					unityMaterial.SetFloat(Mode, 3);
					unityMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.One);
					unityMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
					//!!!!!!! this is normally switched off but somehow enabling it seems to resolve so many issues.. keep an eye out for weirld opacity issues
					//unityMaterial.SetInt("_ZWrite", 0);
					unityMaterial.SetInt(ZWrite, 1);
					unityMaterial.DisableKeyword("_ALPHATEST_ON");
					unityMaterial.DisableKeyword("_ALPHABLEND_ON");
					unityMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
					unityMaterial.renderQueue = 3000;
					break;

				default:
					throw new ArgumentOutOfRangeException();
			}
		}

	}


}
