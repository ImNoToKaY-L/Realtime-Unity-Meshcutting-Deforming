#if UNITY_2019_2_OR_NEWER && SRP_LIGHTWEIGHT
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;

namespace Obi
{

    public class ObiFluidRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class FluidRendererSettings
        {
            [Range(0, 0.1f)]
            public float blurRadius = 0.02f;

            [Range(0.01f, 2)]
            public float thicknessCutoff = 1.2f;

            public Material colorMaterial;
            public Material fluidMaterial;
        }

        public FluidRendererSettings settings = new FluidRendererSettings();

        private RenderFluidPass m_RenderFluidPass;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            m_RenderFluidPass.Setup(renderer.cameraColorTarget, settings.colorMaterial, settings.fluidMaterial, settings.blurRadius, settings.thicknessCutoff);
            renderer.EnqueuePass(m_RenderFluidPass);
        }

        public override void Create()
        {
            m_RenderFluidPass = new RenderFluidPass(RenderTargetHandle.CameraTarget);
            m_RenderFluidPass.renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
        }
    }

    public class RenderFluidPass : ScriptableRenderPass
    {
        const string k_RenderGrabPassTag = "RenderFluidPass";

        private float blurRadius;
        private float thicknessCutoff;

        private Material depth_BlurMaterial;
        private Material normal_ReconstructMaterial;
        private Material thickness_Material;
        private Material colorMaterial;
        private Material fluidMaterial;

        private FluidRenderingUtils.FluidRenderTargets renderTargets;
        private RenderTargetIdentifier target;

        public RenderFluidPass(RenderTargetHandle colorHandle)
        {
        }

        public void Setup(RenderTargetIdentifier colorSource, Material colorMaterial, Material fluidMaterial, float blurRadius, float thicknessCutoff)
        {
            // Copy settings;
            this.colorMaterial = colorMaterial;
            this.fluidMaterial = fluidMaterial;
            this.blurRadius = blurRadius;
            this.thicknessCutoff = thicknessCutoff;

            // Setup render targets:
            target = colorSource;
            renderTargets = new FluidRenderingUtils.FluidRenderTargets();
            renderTargets.refraction = Shader.PropertyToID("_Refraction");
            renderTargets.foam = Shader.PropertyToID("_Foam");
            renderTargets.depth = Shader.PropertyToID("_FluidDepthTexture");
            renderTargets.thickness1 = Shader.PropertyToID("_FluidThickness1");
            renderTargets.thickness2 = Shader.PropertyToID("_FluidThickness2");
            renderTargets.smoothDepth = Shader.PropertyToID("_FluidSurface");
            renderTargets.normals = Shader.PropertyToID("_FluidNormals");

            depth_BlurMaterial = CreateMaterial(Shader.Find("Hidden/ScreenSpaceCurvatureFlow"));
            normal_ReconstructMaterial = CreateMaterial(Shader.Find("Hidden/NormalReconstruction"));
            thickness_Material = CreateMaterial(Shader.Find("Hidden/FluidThickness"));

            bool shadersSupported = depth_BlurMaterial && normal_ReconstructMaterial && thickness_Material;

            if (!shadersSupported ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Depth) ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat) ||
                !SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                Debug.LogWarning("Obi Fluid Renderer not supported in this platform.");
                return;
            }
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // refraction (background), foam and fluid depth buffers:
            cmd.GetTemporaryRT(renderTargets.refraction, cameraTextureDescriptor.width, cameraTextureDescriptor.height, 0, FilterMode.Bilinear);
            cmd.GetTemporaryRT(renderTargets.foam, cameraTextureDescriptor.width, cameraTextureDescriptor.height, 0, FilterMode.Bilinear);
            cmd.GetTemporaryRT(renderTargets.depth, cameraTextureDescriptor.width, cameraTextureDescriptor.height, 24, FilterMode.Point, RenderTextureFormat.Depth);

            // thickness/color, surface depth and normals buffers:
            cmd.GetTemporaryRT(renderTargets.thickness1, (int)(cameraTextureDescriptor.width * 0.5f), (int)(cameraTextureDescriptor.height * 0.5f), 24, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            cmd.GetTemporaryRT(renderTargets.thickness2, (int)(cameraTextureDescriptor.width * 0.5f), (int)(cameraTextureDescriptor.height * 0.5f), 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
            cmd.GetTemporaryRT(renderTargets.smoothDepth, cameraTextureDescriptor.width, cameraTextureDescriptor.height, 0, FilterMode.Point, RenderTextureFormat.RFloat);
            cmd.GetTemporaryRT(renderTargets.normals, cameraTextureDescriptor.width, cameraTextureDescriptor.height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGBHalf);
        }

        protected Material CreateMaterial(Shader shader)
        {
            if (!shader || !shader.isSupported)
                return null;
            Material m = new Material(shader);
            m.hideFlags = HideFlags.HideAndDontSave;
            return m;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (colorMaterial != null && fluidMaterial != null)
            {

                float blurScale = FluidRenderingUtils.SetupFluidCamera(renderingData.cameraData.camera);

                depth_BlurMaterial.SetFloat("_BlurScale", blurScale);
                depth_BlurMaterial.SetFloat("_BlurRadiusWorldspace", blurRadius);

                if (fluidMaterial != null)
                {
                    fluidMaterial.SetFloat("_ThicknessCutoff", thicknessCutoff);
                }

                CommandBuffer cmd = CommandBufferPool.Get(k_RenderGrabPassTag);
                using (new ProfilingSample(cmd, k_RenderGrabPassTag))
                {
                    ObiParticleRenderer[] particleRenderers = GameObject.FindObjectsOfType<ObiParticleRenderer>();
                    FluidRenderingUtils.SetupCommandBuffer(cmd, target, renderTargets, depth_BlurMaterial, normal_ReconstructMaterial, thickness_Material, colorMaterial, fluidMaterial, particleRenderers);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
        }
    }
}


#endif
