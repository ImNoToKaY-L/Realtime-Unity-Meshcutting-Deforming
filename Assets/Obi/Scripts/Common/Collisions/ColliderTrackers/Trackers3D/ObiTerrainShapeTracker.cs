using System;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Obi{

	public class ObiTerrainShapeTracker : ObiShapeTracker
	{
        public bool triangleBasedContacts = false;

		private Vector3 size;
		private int resolutionU;
		private int resolutionV;
		private bool heightmapDataHasChanged = false;
        private GCHandle dataHandle;

        public ObiTerrainShapeTracker(TerrainCollider collider, bool triangleBasedContacts){

			this.collider = collider;
            this.triangleBasedContacts = triangleBasedContacts;
			adaptor.is2D = false;
			oniShape = Oni.CreateShape(Oni.ShapeType.Heightmap);

			UpdateHeightData();
		}		

		public void UpdateHeightData(){

			TerrainCollider terrain = collider as TerrainCollider;

			if (terrain != null){

				TerrainData data = terrain.terrainData;

				int width = data.heightmapResolution;
				int height = data.heightmapResolution;
	
				float[,] heights = data.GetHeights(0,0,width,height);
				
				float[] buffer = new float[width * height];
				for (int y = 0; y < height; ++y)
					for (int x = 0; x < width; ++x)
						buffer[y*width+x] = heights[y,x];
				
				Oni.UnpinMemory(dataHandle);
	
				dataHandle = Oni.PinMemory(buffer);

				heightmapDataHasChanged = true;
			}
		}
	
		public override bool UpdateIfNeeded (){

			TerrainCollider terrain = collider as TerrainCollider;
	
			if (terrain != null){

				TerrainData data = terrain.terrainData;

				if (data != null && (data.size != size || 
									 data.heightmapResolution != resolutionU ||
									 data.heightmapResolution != resolutionV || 
									 heightmapDataHasChanged)){

					size = data.size;
					resolutionU = data.heightmapResolution;
					resolutionV = data.heightmapResolution;
					heightmapDataHasChanged = false;
					adaptor.Set(size,resolutionU,resolutionV,dataHandle.AddrOfPinnedObject());
                    adaptor.accurateContacts = triangleBasedContacts;
					Oni.UpdateShape(oniShape,ref adaptor);
					return true;
				}			
			}
			return false;
		}

		public override void Destroy(){
			base.Destroy();

			Oni.UnpinMemory(dataHandle);
		}
	}
}

