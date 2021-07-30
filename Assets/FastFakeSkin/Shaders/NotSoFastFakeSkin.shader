Shader "Skin/NotSoFastFakeSkin" {

        Properties {
            _MainTex ("Base (RGB) Boob Mask (A)", 2D) = "white" {}
            //_MaskTex ("Skin Mask", 2D) = "white" {}
            _NormalMap ("Normalmap", 2D) = "bump" {}                 
            _BoobMap ("Boob Map", 2D) = "gray" {}            
            _Color2 ("Diffuse Tint", color) = (1, 0.859, 0.859, 0)
            _Color3 ("Deep Layer", color) = (0.463, 0.243, 0.224, 0)
            _SpecColor ("Spec color", color) = (1, 0.882, 0.867, 0)
            _SpecPow ("Specularity", Range(0, 1)) = 0.03
            _GlossPow ("Smoothness", Range(0, 1)) = 0.28           
            _Blend1 ("Blend Amount", Range(0, 1)) = 1
        }

        SubShader {
            Tags { "RenderType"="Opaque" }
            LOD 300
            
            CGPROGRAM
            #pragma surface surf StandardSpecular fullforwardshadows vertex:vert
            //addshadow
            #pragma target 3.0
			#include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float4 tangent : TANGENT;
                float3 normal : NORMAL;
                float2 texcoord : TEXCOORD0;
                float2 texcoord1 : TEXCOORD1;
                float2 texcoord2 : TEXCOORD2;
                //fixed4 color : COLOR;
				//#if defined(SHADER_API_XBOX360)
   				//half4 texcoord3 : TEXCOORD3;
    			//half4 texcoord4 : TEXCOORD4;
    			//half4 texcoord5 : TEXCOORD5;
				//#endif     
            };
            
            struct Input {
                float2 uv_MainTex;
                float3 coords0;
				float3 coords1;
                float3 viewDir;
            };
            
            
            sampler2D _MainTex;
            sampler2D _NormalMap;
            fixed4 _Color2;
            fixed4 _Color3;
            float _SpecPow;
            float _GlossPow;
            float _Blend1;


            //sampler2D _MaskTex;
            //uniform float4 _NormalMap_ST;       
            sampler2D _BoobMap;
            
            void vert (inout appdata v, out Input o)
            {              
                UNITY_INITIALIZE_OUTPUT(Input, o);	
                float3 scale = float3(
                    length(UNITY_MATRIX_IT_MV._m00_m10_m20),
                    length(UNITY_MATRIX_IT_MV._m01_m11_m21),
                    length(UNITY_MATRIX_IT_MV._m02_m12_m22)
                );

				TANGENT_SPACE_ROTATION;
				o.coords0 = mul(rotation, UNITY_MATRIX_IT_MV[0].xyz / scale);
				o.coords1 = mul(rotation, UNITY_MATRIX_IT_MV[1].xyz / scale);
            }

            void surf (Input IN, inout SurfaceOutputStandardSpecular o) {
                
                half4 diff = tex2D(_MainTex, IN.uv_MainTex);
                half4 c = diff * _Color2;

                o.Specular = _SpecColor * _SpecPow;
                o.Smoothness = _GlossPow;
               
                float3 normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
				fixed3 blurredNormal = UnpackNormal(tex2Dlod(_NormalMap, float4(IN.uv_MainTex, 0.0, 3.0)));	
               
                half2 nmCombined;
				nmCombined.x = dot(IN.coords0, blurredNormal);
				nmCombined.y = dot(IN.coords1, blurredNormal);
					
                half4 boobMap = tex2D(_BoobMap, nmCombined * 0.5 + 0.5);
                half mask = diff.a;
                
                o.Normal = lerp(blurredNormal, normal, IN.viewDir);
             
                float twiceLuminance = dot(c, fixed4(0.2126, 0.7152, 0.0722, 0)) * 2;
                fixed4 output = 0;
               
                if (twiceLuminance < 1) {
                    output = lerp(_Color3, boobMap, twiceLuminance);
                } else {
                    output = lerp(boobMap, _Color3, twiceLuminance - 1);
                }
                
                half rim = 1.0 - saturate(dot (normalize(IN.viewDir), o.Normal));
                o.Emission = _SpecColor * mask * pow (rim, 4) * 0.2;
                
                o.Albedo = lerp(output, c.rgb, (1.0 -  mask * _Blend1) ) * 2;
                
            }
            ENDCG
        }
        FallBack "Diffuse"
    }