using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

namespace Obi
{
    public static class FluidRenderingUtils
    {
        public struct FluidRenderTargets
        {
            public int refraction;
            public int foam;
            public int depth;
            public int thickness1;
            public int thickness2;
            public int smoothDepth;
            public int normals;
        }

        private static Color thicknessBufferClear = new Color(1, 1, 1, 0);

        public static float SetupFluidCamera(Camera cam)
        {
            Shader.SetGlobalMatrix("_Camera_to_World", cam.cameraToWorldMatrix);
            Shader.SetGlobalMatrix("_World_to_Camera", cam.worldToCameraMatrix);
            Shader.SetGlobalMatrix("_InvProj", cam.projectionMatrix.inverse);

            float fovY = cam.fieldOfView;
            float far = cam.farClipPlane;
            float y = cam.orthographic ? 2 * cam.orthographicSize : 2 * Mathf.Tan(fovY * Mathf.Deg2Rad * 0.5f) * far;
            float x = y * cam.aspect;
            Shader.SetGlobalVector("_FarCorner", new Vector3(x, y, far));

            return cam.orthographic ? 1 : cam.pixelWidth / cam.aspect * (1.0f / Mathf.Tan(fovY * Mathf.Deg2Rad * 0.5f));
        }

        // Update is called once per frame
        public static void SetupCommandBuffer(CommandBuffer cmd, RenderTargetIdentifier cameraTarget, FluidRenderTargets renderTargets,
                                Material depth_BlurMaterial,
                                Material normal_ReconstructMaterial,
                                Material thickness_Material,
                                Material colorMaterial,
                                Material fluidMaterial,
                                ObiParticleRenderer[] renderers)
        {
            // Copy screen contents to refract them later.
            cmd.Blit(cameraTarget, renderTargets.refraction);

            cmd.SetRenderTarget(renderTargets.depth); // fluid depth
            cmd.ClearRenderTarget(true, true, Color.clear); //clear


            // draw fluid depth texture:
            foreach (ObiParticleRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    foreach (Mesh mesh in renderer.ParticleMeshes)
                    {
                        if (renderer.ParticleMaterial != null)
                            cmd.DrawMesh(mesh, Matrix4x4.identity, renderer.ParticleMaterial, 0, 0);
                    }
                }
            }

            // draw fluid thickness and color:
            cmd.SetRenderTarget(renderTargets.thickness1);
            cmd.ClearRenderTarget(true, true, thicknessBufferClear);

            foreach (ObiParticleRenderer renderer in renderers)
            {
                if (renderer != null)
                {

                    cmd.SetGlobalColor("_ParticleColor", renderer.particleColor);
                    cmd.SetGlobalFloat("_RadiusScale", renderer.radiusScale);

                    foreach (Mesh mesh in renderer.ParticleMeshes)
                    {
                        cmd.DrawMesh(mesh, Matrix4x4.identity, thickness_Material, 0, 0);
                        cmd.DrawMesh(mesh, Matrix4x4.identity, colorMaterial, 0, 0);
                    }
                }
            }

            // blur fluid thickness:
            cmd.Blit(renderTargets.thickness1, renderTargets.thickness2, thickness_Material, 1);
            cmd.Blit(renderTargets.thickness2, renderTargets.thickness1, thickness_Material, 2);

            // draw foam: 
            cmd.SetRenderTarget(renderTargets.foam);
            cmd.ClearRenderTarget(true, true, Color.clear);

            foreach (ObiParticleRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    ObiFoamGenerator foamGenerator = renderer.GetComponent<ObiFoamGenerator>();
                    if (foamGenerator != null && foamGenerator.advector != null && foamGenerator.advector.Particles != null)
                    {
                        ParticleSystemRenderer psRenderer = foamGenerator.advector.Particles.GetComponent<ParticleSystemRenderer>();
                        if (psRenderer != null)
                            cmd.DrawRenderer(psRenderer, psRenderer.material);
                    }
                }
            }

            // blur fluid surface:
            cmd.Blit(renderTargets.depth, renderTargets.smoothDepth, depth_BlurMaterial);

            // reconstruct normals from smoothed depth:
            cmd.Blit(renderTargets.smoothDepth, renderTargets.normals, normal_ReconstructMaterial);

            // render fluid:
            cmd.SetGlobalTexture("_FluidDepth", renderTargets.depth);
            cmd.SetGlobalTexture("_Foam", renderTargets.foam);
            cmd.SetGlobalTexture("_Refraction", renderTargets.refraction);
            cmd.SetGlobalTexture("_Thickness", renderTargets.thickness1);
            cmd.SetGlobalTexture("_Normals", renderTargets.normals);
            cmd.Blit(renderTargets.smoothDepth, cameraTarget, fluidMaterial);
        }
    }

}