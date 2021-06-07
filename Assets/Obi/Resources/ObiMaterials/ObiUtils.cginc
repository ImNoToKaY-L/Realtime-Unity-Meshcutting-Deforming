#ifndef OBIUTILS_INCLUDED
#define OBIUTILS_INCLUDED

#include "UnityCG.cginc"
#include "UnityStandardUtils.cginc"
#include "AutoLight.cginc"

float4x4 _Camera_to_World;

struct fout {
 	half4 color : COLOR;
	float depth : DEPTH;
};

half3 SampleSphereAmbient(float3 eyeNormal, float3 eyePos)
{
	#if UNITY_SHOULD_SAMPLE_SH
		half3 worldNormal = mul(transpose((float3x3)UNITY_MATRIX_V),eyeNormal);  
		half3 worldPos = mul(_Camera_to_World,half4(eyePos,1.0));  	
		return ShadeSHPerPixel(half4(worldNormal, 1.0),half3(0,0,0),worldPos);
	#else
		return UNITY_LIGHTMODEL_AMBIENT;
	#endif
}

#endif
