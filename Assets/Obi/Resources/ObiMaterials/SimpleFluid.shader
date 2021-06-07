Shader "Obi/Fluid/Simple2DFluid"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
		_RefractionCoeff ("Refraction", Range (-0.1, 0.1)) = 0.01
		_Color ("Fluid color", Color) = (0.3,0.6,1,1)
    }

    SubShader
    {
        Pass
        {

            Name "SimpleFluid"
            Tags {"LightMode" = "ForwardBase"}

            Blend SrcAlpha OneMinusSrcAlpha 

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "ObiParticles.cginc"

            sampler2D _Refraction;
			sampler2D _Thickness;
			fixed4 _Color;
			float _RefractionCoeff;
			float _ThicknessCutoff;

            struct vin
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : POSITION;
            };

            v2f vert (vin v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.pos);
                o.uv = v.uv;

                return o;
            }

            half4 frag (v2f i) : COLOR
            {

                fixed4 fluid = tex2D(_Thickness,i.uv);
                // basic thickness-driven refraction:
                fixed4 output = tex2D(_Refraction,i.uv + _RefractionCoeff*fluid.a);   

                // thresholding for cheap metaball-like effect
                if (fluid.a * 10 < _ThicknessCutoff)
                    discard;

                return output*_Color; // tint your fluid here.
            }
            ENDCG
        }        

    }
}