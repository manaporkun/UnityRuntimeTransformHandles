Shader "TransformHandles/Samples/LevelEditorOpaque"
{
    Properties { _Color ("Color", Color) = (0.2,0.7,0.6,1) _Grid ("Grid", Float) = 0 }
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
            struct v2f { float4 vertex : SV_POSITION; float3 world : TEXCOORD0; float3 normal : TEXCOORD1; };
            fixed4 _Color;
            float _Grid;
            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float shade = .55 + .45 * saturate(dot(normalize(i.normal), normalize(float3(-.4,1,-.3))));
                float2 cell = abs(frac(i.world.xz + .5) - .5);
                float gridLine = 1 - smoothstep(.015, .035, min(cell.x, cell.y));
                return fixed4(lerp(_Color.rgb * shade, _Color.rgb * .82, gridLine * _Grid * .35), 1);
            }
            ENDCG
        }
    }
}
