using System;
using System.Text;
using UnityEngine;
using VisualPinball.Engine.VPT;
using VisualPinball.Unity.Extensions;
using VisualPinball.Unity.VPT.Table;

namespace VisualPinball.Unity.Import.Material
{
    public class HdrpMaterialConverter : IMaterialConverter
    {
        private readonly int SurfaceType = Shader.PropertyToID("_SurfaceType");
        private readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
        private readonly int NormalMap = Shader.PropertyToID("_NormalMap");
        private readonly int Metallic = Shader.PropertyToID("_Metallic");
        private readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        private readonly int BlendMode = Shader.PropertyToID("_BlendMode");
        private readonly int TransparentSortPriority = Shader.PropertyToID("_TransparentSortPriority");
        private readonly int AlphaCutoffEnable = Shader.PropertyToID("_AlphaCutoffEnable");
        private readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        private readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        private readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        public Shader GetShader()
        {
            return Shader.Find("HDRP/Lit");
        }

        public UnityEngine.Material CreateMaterial(PbrMaterial vpxMaterial, TableBehavior table, StringBuilder debug = null)
        {
            var unityMaterial = new UnityEngine.Material(GetShader())
            {
                name = vpxMaterial.Id
            };

            // apply some basic manipulations to the color. this just makes very
            // very white colors be clipped to 0.8204 aka 204/255 is 0.8
            // this is to give room to lighting values. so there is more modulation
            // of brighter colors when being lit without blow outs too soon.
            var col = vpxMaterial.Color.ToUnityColor();
            if (vpxMaterial.Color.IsGray() && col.grayscale > 0.8)
            {
                debug?.AppendLine("Color manipulation performed, brightness reduced.");
                col.r = col.g = col.b = 0.8f;
            }
            unityMaterial.SetColor(BaseColor, col);

            // validate IsMetal. if true, set the metallic value.
            // found VPX authors setting metallic as well as translucent at the
            // same time, which does not render correctly in unity so we have
            // to check if this value is true and also if opacity <= 1.
            if (vpxMaterial.IsMetal && (!vpxMaterial.IsOpacityActive || vpxMaterial.Opacity >= 1))
            {
                unityMaterial.SetFloat(Metallic, 1f);
                debug?.AppendLine("Metallic set to 1.");
            }

            // roughness / glossiness
            unityMaterial.SetFloat(Smoothness, vpxMaterial.Roughness);

            // blend mode
            ApplyBlendMode(unityMaterial, vpxMaterial.MapBlendMode);
            if (vpxMaterial.MapBlendMode == Engine.VPT.BlendMode.Translucent)
            {
                col.a = Mathf.Min(1, Mathf.Max(0, vpxMaterial.Opacity));
                unityMaterial.SetColor(BaseColor, col);
            }

            // map
            if (table != null && vpxMaterial.HasMap)
            {
                unityMaterial.SetTexture(
                    BaseColorMap,
                    table.GetTexture(vpxMaterial.Map.Name)
                );
            }

            // normal map
            if (table != null && vpxMaterial.HasNormalMap)
            {
                unityMaterial.SetTexture(
                    NormalMap,
                    table.GetTexture(vpxMaterial.NormalMap.Name)
                );
            }

            return unityMaterial;
        }

        private void ApplyBlendMode(UnityEngine.Material unityMaterial, BlendMode blendMode)
        {
            switch (blendMode)
            {
                case Engine.VPT.BlendMode.Opaque:

                    unityMaterial.SetFloat(SurfaceType, 0); // 0 = Opaque; 1 = Transparent

                    // render queue
                    unityMaterial.renderQueue = -1;

                    break;

                case Engine.VPT.BlendMode.Cutout:

                    unityMaterial.SetFloat(SurfaceType, 0); // 0 = Opaque; 1 = Transparent
                    unityMaterial.SetInt(AlphaCutoffEnable, 1);

                    // render queue
                    unityMaterial.renderQueue = 2450;

                    unityMaterial.EnableKeyword("_ALPHATEST_ON");
                    unityMaterial.EnableKeyword("_NORMALMAP_TANGENT_SPACE");

                    break;

                case Engine.VPT.BlendMode.Translucent:

                    unityMaterial.SetFloat(SurfaceType, 1); // 0 = Opaque; 1 = Transparent
                    unityMaterial.SetFloat(BlendMode, 4); // 0 = Alpha, 4 = PreMultiply

                    // render queue
                    int transparentRenderQueueBase = 3000;
                    int transparentSortingPriority = 0;
                    unityMaterial.SetInt(TransparentSortPriority, transparentSortingPriority);
                    unityMaterial.renderQueue = transparentRenderQueueBase + transparentSortingPriority;

                    unityMaterial.EnableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
                    unityMaterial.EnableKeyword("_BLENDMODE_PRE_MULTIPLY");
                    unityMaterial.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                    unityMaterial.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                    unityMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
