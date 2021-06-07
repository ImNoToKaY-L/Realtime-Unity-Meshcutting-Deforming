using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi{

    [CreateAssetMenu(fileName = "distance field", menuName = "Obi/Distance Field", order = 181)]
	[ExecuteInEditMode]	
	public class ObiDistanceField : ScriptableObject
	{
		[SerializeProperty("InputMesh")]
		[SerializeField] private Mesh input = null;

		[HideInInspector][SerializeField] private float minNodeSize = 0;
		[HideInInspector][SerializeField] private Bounds bounds = new Bounds(); 
		[HideInInspector] public Oni.DFNode[] nodes;		/**< list of distance field nodes*/

		[Range(0.00001f,0.1f)]
		public float maxError = 0.01f;

		[Range(1, 8)]
		public int maxDepth = 5;

		IntPtr oniDistanceField;

		public bool Initialized{
			get{return nodes != null;}
		}

		public IntPtr OniDistanceField {
			get{return oniDistanceField;}
		}

		public Bounds FieldBounds {
			get{return bounds;}
		}

		public float EffectiveSampleSize {
			get{return minNodeSize;}
		}

		public Mesh InputMesh{
			set{
				if (value != input){
					Reset();
					input = value;
				}
			}
			get{return input;}
		}

		public void OnEnable(){

			oniDistanceField = Oni.CreateDistanceField();

	        // Check integrity after serialization, (re?) initialize if there's data missing.
			if (nodes != null){
				Oni.SetDistanceFieldNodes(oniDistanceField,nodes,nodes.Length);
			}
		}

		public void OnDisable(){	
			Oni.DestroyDistanceField(oniDistanceField);
		}

		public void Reset(){
			nodes = null;
			if (input != null)
				bounds = input.bounds;
		}

		public IEnumerator Generate(){

			Reset();

			if (input == null)
				yield break;

			// build distance field:
			int[] tris = input.triangles;
			Vector3[] verts = input.vertices;
			Oni.StartBuildingDistanceField(oniDistanceField,maxError,maxDepth,verts,tris,verts.Length,tris.Length/3);

			int i = 0;
			bool done = false;
			while (!done){

				for (int j = 0; j < 16; ++j)
					done |= Oni.ContinueBuildingDistanceField(oniDistanceField);

				i += 16;
				yield return new CoroutineJob.ProgressInfo("Processed nodes: "+i,1);
			}

			// retrieve nodes:
			int count = Oni.GetDistanceFieldNodeCount(oniDistanceField);
			nodes = new Oni.DFNode[count];
			Oni.GetDistanceFieldNodes(oniDistanceField,nodes);

			// calculate min node size;
			minNodeSize = float.PositiveInfinity;
			for (int j = 0; j < nodes.Length; ++j)
				minNodeSize = Mathf.Min(minNodeSize, nodes[j].center[3] * 2);

			// get bounds:
			float max = Mathf.Max(bounds.size[0],Mathf.Max(bounds.size[1],bounds.size[2]))+0.2f;
			bounds.size = new Vector3(max,max,max);
		}

		/**
		 * Return a volume texture containing a representation of this distance field.
		 */
		public Texture3D GetVolumeTexture(int size){

			if (!Initialized)
				return null;
	
			// upper bound of the distance from any point inside the bounds to the surface.
			float maxDist = Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);				
	
			float spacingX = bounds.size.x / (float)size;
			float spacingY = bounds.size.y / (float)size;
			float spacingZ = bounds.size.z / (float)size;
	
			Texture3D tex = new Texture3D (size, size, size, TextureFormat.Alpha8, false);
	
			var cols = new Color[size*size*size];
			int idx = 0;
			Color c = Color.black;
	
			for (int z = 0; z < size; ++z)
			{
				for (int y = 0; y < size; ++y)
				{
					for (int x = 0; x < size; ++x, ++idx)
					{
						Vector3 samplePoint = bounds.min + new Vector3(spacingX * x + spacingX*0.5f,
						                                 			   spacingY * y + spacingY*0.5f,
						                                  			   spacingZ * z + spacingZ*0.5f);
	
						float distance = Oni.SampleDistanceField(oniDistanceField,samplePoint.x,samplePoint.y,samplePoint.z);
	
						if (distance >= 0)
							c.a = distance.Remap(0,maxDist*0.1f,0.5f,1);
						else 
							c.a = distance.Remap(-maxDist*0.1f,0,0,0.5f);
	
						cols[idx] = c;
					}
				}
			}
			tex.SetPixels (cols);
			tex.Apply ();
			return tex;
	
		}


		/*public void Visualize(){
	
			for (int i = 0; i < nodes.Length; ++i )
			{
				Gizmos.color = new Color(1,1,1,0.2f);
				Gizmos.DrawWireCube(nodes[i].center,Vector3.one * nodes[i].center[3] * 2);
			}

			if (nodes == null || nodes.Length == 0) 
				return;
		}*/

	}
}

