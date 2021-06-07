using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;


namespace Obi
{
	/**
	 * Very simple 2D only fluid rendering. This simply draws particles additively to a thickness texture,
	 * and uses the result to tint and refract the background. Does not perform z-testing against the scene depth buffer,
	 * does not calculate lightning, foam, or transmission/reflection effects.
	 */
	public class ObiSimpleFluidRenderer : ObiBaseFluidRenderer
	{
	
		[Range(0.01f,2)]
		public float thicknessCutoff = 1.2f;

		private Material thickness_Material;
		public Material fluidMaterial;
				
		protected override void Setup(){

            GetComponent<Camera>().depthTextureMode |= DepthTextureMode.Depth;

            if (thickness_Material == null)
			{
				thickness_Material = CreateMaterial(Shader.Find("Hidden/FluidThickness"));
			}
	
			bool shadersSupported = thickness_Material;
	
			if (!shadersSupported ||
				!SystemInfo.supportsImageEffects)
	        {
	            enabled = false;
				Debug.LogWarning("Obi Simple Fluid Renderer not supported in this platform.");
	            return;
	        }

            if (fluidMaterial != null)
			{		
				fluidMaterial.SetFloat("_ThicknessCutoff", thicknessCutoff);
			}
		}
	
		protected override void Cleanup()
		{
			if (thickness_Material != null)
				Object.DestroyImmediate (thickness_Material);
		}
	
		public override void UpdateFluidRenderingCommandBuffer()
		{
	
			renderFluid.Clear();
	
	        if (particleRenderers == null || fluidMaterial == null)
	            return;
	        
	        // declare buffers:
	        int refraction = Shader.PropertyToID("_Refraction");
	        int thickness = Shader.PropertyToID("_Thickness");
	
	        // get RTs (at half resolution):
	        renderFluid.GetTemporaryRT(refraction,-2,-2,0,FilterMode.Bilinear);
	        renderFluid.GetTemporaryRT(thickness,-2,-2,0,FilterMode.Bilinear);
	
	        // render background:
	        renderFluid.Blit (BuiltinRenderTextureType.CurrentActive, refraction);
	
	        // render particle thickness to alpha channel of thickness buffer:
	        renderFluid.SetRenderTarget(thickness);
	        renderFluid.ClearRenderTarget(true,true,Color.clear); 
	        foreach(ObiParticleRenderer renderer in particleRenderers){
	            if (renderer != null){
	
					renderFluid.SetGlobalColor("_ParticleColor",renderer.particleColor);
					renderFluid.SetGlobalFloat("_RadiusScale",renderer.radiusScale);
	
	                foreach(Mesh mesh in renderer.ParticleMeshes){
	                    renderFluid.DrawMesh(mesh,Matrix4x4.identity,thickness_Material,0,0);
	                }
	            }
	        }
	
	        // final composite:
	        renderFluid.Blit(refraction,BuiltinRenderTextureType.CameraTarget,fluidMaterial);  
	
		}
	
	}
}

