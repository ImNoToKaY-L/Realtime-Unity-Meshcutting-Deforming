Shader "Obi/EditorParticles"
{
    SubShader
    {
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest always 
        Cull Back 
 
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
         
            #include "UnityCG.cginc"
 
            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };
 
            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv : TEXCOORD0;
            };
         
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord - 0.5;
                o.color = v.color;
                return o;
            }
 
            fixed4 frag (v2f i) : SV_Target
            {
                // antialiased circle:
                float dist = length(i.uv);
                float pwidth = fwidth(dist);
                float alpha = i.color.a * saturate((0.5 - dist) / pwidth);


                return fixed4(i.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
