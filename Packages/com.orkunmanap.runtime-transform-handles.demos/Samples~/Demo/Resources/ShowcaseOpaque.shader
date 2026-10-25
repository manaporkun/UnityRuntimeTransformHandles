Shader "TransformHandles/Samples/ShowcaseOpaque"
{
    Properties { _Color ("Color", Color) = (0.2,0.7,0.6,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 vertex : SV_POSITION; float3 normal : TEXCOORD0; };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float shade = .55 + .45 * saturate(dot(normalize(i.normal), normalize(float3(-.4,1,-.3))));
                return fixed4(_Color.rgb * shade, 1);
            }
            ENDCG
        }
    }
}
