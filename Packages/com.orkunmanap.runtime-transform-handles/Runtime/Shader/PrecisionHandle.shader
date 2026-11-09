Shader "Handles/Precision"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _PlaneBorderWidth ("Plane border width (local units)", Range(0, 0.1)) = 0
        _PlaneBorderOpacity ("Plane border opacity", Range(0, 1)) = 0.95
    }

    // Universal Render Pipeline SubShader. Unity auto-selects this when URP is active
    // (the "RenderPipeline"="UniversalPipeline" tag matches the current pipeline) and
    // skips it entirely when the URP package is not installed (PackageRequirements).
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "12.1.0"
        }

        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _PlaneBorderWidth;
            float _PlaneBorderOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 padPosition : TEXCOORD0;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.padPosition = IN.positionOS.xz;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Rounded pad half-size .5 and corner radius .16 match the authoring mesh.
                // Zero width leaves axes, rings and backing materials exactly as before.
                if (_PlaneBorderWidth <= 0) return _Color;
                float2 q = abs(IN.padPosition) - .34;
                float distanceToEdge = length(max(q, 0)) + min(max(q.x, q.y), 0) - .16;
                float aa = max(fwidth(distanceToEdge), .0001);
                float border = smoothstep(-_PlaneBorderWidth - aa * .5,
                    -_PlaneBorderWidth + aa * .5, distanceToEdge);
                return float4(_Color.rgb, lerp(_Color.a, _PlaneBorderOpacity, border));
            }
            ENDHLSL
        }
    }

    // Built-in Render Pipeline SubShader (fallback; selected when no SRP is active).
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" }
        LOD 100

        ZWrite Off
        ZTest Always
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 padPosition : TEXCOORD0;
            };

            float4 _Color;
            float _PlaneBorderWidth;
            float _PlaneBorderOpacity;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.padPosition = v.vertex.xz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Rounded pad half-size .5 and corner radius .16 match the authoring mesh.
                // Zero width leaves axes, rings and backing materials exactly as before.
                if (_PlaneBorderWidth <= 0) return _Color;
                float2 q = abs(i.padPosition) - .34;
                float distanceToEdge = length(max(q, 0)) + min(max(q.x, q.y), 0) - .16;
                float aa = max(fwidth(distanceToEdge), .0001);
                float border = smoothstep(-_PlaneBorderWidth - aa * .5,
                    -_PlaneBorderWidth + aa * .5, distanceToEdge);
                return float4(_Color.rgb, lerp(_Color.a, _PlaneBorderOpacity, border));
            }
            ENDCG
        }
    }
}
