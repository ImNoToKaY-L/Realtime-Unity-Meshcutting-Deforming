// Mostly based on the Unity skin shader from the Unity Lab demo
Shader "Skin/FFSFull_Cutout" {

        Properties{
            _MainTex("Base (RGB) Cutout Mask (A)", 2D) = "white" {}
            _MaskTex("Skin Masks: Spec (R) Gloss (G) Occlusion (B) Boob Mask (A)", 2D) = "white" {}
            _NormalMap("Base Normalmap", 2D) = "bump" {}
            _BoobMap("Boob Map", 2D) = "gray" {}
            _BRDFTex("Brdf Map", 2D) = "gray" {}
            _BeckmannTex("BeckmannTex", 2D) = "gray" {}
            _Color2("Diffuse Tint", color) = (1, 0.859, 0.859, 0)
            _Color3("Deep Layer", color) = (0.463, 0.243, 0.224, 0)
            //_SpecColor ("Spec color", color) = (1, 0.882, 0.867, 0)
            _SpecPow("Specularity", Range(1, 16)) = 3.12
            _GlossPow("Smoothness", Range(0, 1)) = 0.28
            _Blend1("Blend Amount", Range(0, 1)) = 1
            _AmbientContribution("Ambience", Range(0, 1)) = 1
            _Bright("Brightness", Range(0, 1)) = 1
            _SpecOcc("Spec Occlusion", Range(0, 1)) = 0
            _Cutoff("Alpha cutoff", Range(0,1)) = 0.5
        }

        SubShader{
            Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }
            LOD 300

            CGPROGRAM
            #pragma surface surf StandardSkin fullforwardshadows vertex:vert alphatest:_Cutoff
            //addshadow
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct SurfaceOutputStandardSkin {
                fixed3 Albedo;      // diffuse color
                half Specular;    	// specular color
                fixed3 Normal;      // tangent space normal, if written
                half3 Emission;
                half Specularity;
                half Smoothness;    // 0 = rough, 1 = smooth
                half Occlusion;     // occlusion (default 1)
                half SpecOcclusion;
                fixed Alpha;        // alpha for transparencies
            };

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
                float2 uv_MaskTex;
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
            float _Bright;
            float _SpecOcc;


            sampler2D _MaskTex;
            //uniform float4 _NormalMap_ST;
            float _AmbientContribution;

            sampler2D _BoobMap;
            sampler2D _BRDFTex;
            sampler2D _BeckmannTex;


            void vert(inout appdata v, out Input o)
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

            float Fresnel(float3 _half, float3 view, float f0) {
                float base = 1.0 - dot(view, _half);
                float exponential = pow(base, 5.0);
                return exponential + f0 * (1.0 - exponential);
            }

            half SpecularKSK(sampler2D beckmannTex, float3 normal, float3 light, float3 view, float roughness) {

                const float _specularFresnel = 1.08;

                half3 _half = view + light;
                half3 halfn = normalize(_half);

                half ndotl = max(dot(normal, light), 0.0);
                half ndoth = max(dot(normal, halfn), 0.0);

                half ph = pow(2.0 * tex2D(beckmannTex, float2(ndoth, roughness)).r, 10.0);
                half f = lerp(0.25, Fresnel(halfn, view, 0.028), _specularFresnel);
                half ksk = max(ph * f / dot(_half, _half), 0.0);

                return ndotl * ksk;
            }

            half3 Skin_BRDF_PBS(SurfaceOutputStandardSkin s, float oneMinusReflectivity, half3 viewDir, UnityLight light, UnityIndirect gi)
            {

                half3 normalizedLightDir = normalize(light.dir);
                viewDir = normalize(viewDir);

                float3 occl = max(1e-4, light.color.rgb * s.Occlusion);

                half specular = (s.Specularity * SpecularKSK(_BeckmannTex, s.Normal, normalizedLightDir, viewDir , s.Smoothness));
                half specular2 = (s.Specularity * SpecularKSK(_BeckmannTex, s.Normal, normalizedLightDir, viewDir , (s.Smoothness + 0.2)));

                const float blendAmount = 0.6;
                specular = blendAmount * specular + (1 - blendAmount) * specular2;

                half3 brdf = float3(1,1,1);
                float dotNL = dot(s.Normal, normalizedLightDir);
                float2 brdfUV;

                brdfUV.x = dotNL * 0.5 + 0.5;
                brdfUV.y = 0.7 * dot(light.color, fixed3(0.2126, 0.7152, 0.0722));

                brdf = tex2D(_BRDFTex, brdfUV).rgb;

                half3 color = half3(0,0,0);

                half nv = DotClamped(s.Normal, viewDir);
                half grazingTerm = saturate(1 - s.Smoothness + (1 - oneMinusReflectivity));

                color.rgb += s.Albedo * (_AmbientContribution * gi.diffuse + occl * brdf)
                            + specular * light.color
                            + gi.specular * FresnelLerp(specular, grazingTerm, nv) * _AmbientContribution * s.SpecOcclusion;

                return color;
            }

            inline half4 LightingStandardSkin(SurfaceOutputStandardSkin s, half3 viewDir, UnityGI gi)
            {
                s.Normal = normalize(s.Normal);

                half oneMinusReflectivity;
                half3 specColor;
                s.Albedo = EnergyConservationBetweenDiffuseAndSpecular(s.Albedo, s.Specular, /*out*/ oneMinusReflectivity);

                half outputAlpha;
                s.Albedo = PreMultiplyAlpha(s.Albedo, s.Alpha, oneMinusReflectivity, /*out*/ outputAlpha);

                half4 color = half4(0.0, 0.0, 0.0, 1.0);
                color.a = s.Alpha;
                color.rgb = Skin_BRDF_PBS(s, oneMinusReflectivity, viewDir, gi.light, gi.indirect);

                return color;
            }

            inline void LightingStandardSkin_GI(SurfaceOutputStandardSkin s, UnityGIInput data, inout UnityGI gi)
            {
                gi = UnityGlobalIllumination(data, s.Occlusion, s.Smoothness, s.Normal);
            }

            void surf(Input IN, inout SurfaceOutputStandardSkin o) {

                half4 diff = tex2D(_MainTex, IN.uv_MainTex);
                float4 masks = tex2D(_MaskTex, IN.uv_MaskTex).rgba;
                half4 c = diff * _Color2;

                float3 normal = UnpackNormal(tex2D(_NormalMap, IN.uv_MainTex));
                fixed3 blurredNormal = UnpackNormal(tex2Dlod(_NormalMap, float4(IN.uv_MainTex, 0.0, 3.0)));

                half2 nmCombined;
                nmCombined.x = dot(IN.coords0, blurredNormal);
                nmCombined.y = dot(IN.coords1, blurredNormal);

                half4 boobMap = tex2D(_BoobMap, nmCombined * 0.5 + 0.5);
                half mask = masks.a;

                o.Normal = lerp(blurredNormal, normal, IN.viewDir);

                float twiceLuminance = dot(c, fixed4(0.2126, 0.7152, 0.0722, 0)) * 2;
                fixed4 output = 0;

                if (twiceLuminance < 1) {
                    output = lerp(_Color3, boobMap, twiceLuminance);
                } else {
                    output = lerp(boobMap, _Color3, twiceLuminance - 1);
                }

                //we don't need a rim light on this one
                //half rim = 1.0 - saturate(dot (normalize(IN.viewDir), o.Normal));
                //o.Emission = _SpecColor * mask * pow (rim, 4) * 0.2;
                o.Emission = 0;

                o.Albedo = lerp(output, c.rgb, (1.0 - mask * _Blend1)) * _Bright * 2;                              

                o.Specularity = masks.r * _SpecPow;
                o.Smoothness = masks.g * _GlossPow;
                o.SpecOcclusion = saturate(1.0 + (masks.a - 1.0) * _SpecOcc);
                o.Occlusion = masks.b;
                o.Alpha = diff.a;
            }
            ENDCG
        }
        FallBack "Diffuse"
    }