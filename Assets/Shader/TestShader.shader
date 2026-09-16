Shader "Universal Render Pipeline/TestShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        [MainTexture]_MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Specular ("Specular Color", Color) = (1,1,1,1)
        _SpecularScale ("Specular Scale", Range(0,10)) = 5
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline"}

        pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            //声明纹理
            TEXTURE2D(_MainTex);
            //声明纹理采样器
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half4 _Specular;
            half _SpecularScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
            };
            
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos    = TransformObjectToWorld(v.positionOS);
                o.worldNormal = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            
            half4 frag(Varyings i) : SV_Target
            {
                float3 worldNormal = normalize(i.worldNormal);
                float3 worldPos = i.worldPos;

                float3 albedo = _Color.rgb*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);

                Light mainLight=GetMainLight();
                float3 lightDir=normalize(mainLight.direction);
                float diff=dot(worldNormal,lightDir)*0.5+0.5;
                float3 diffColor=mainLight.color*diff*albedo;
                
                float3 ambient=SampleSH(worldNormal)*albedo;

                float3 viewDir=GetWorldSpaceNormalizeViewDir(worldPos);
                float3 halfDir=normalize(lightDir+viewDir);

                float3 high=saturate(dot(worldNormal,halfDir));
                high=pow(high,_SpecularScale);
                float3 specularColor =_Specular.rgb*high*mainLight.color;

                return half4(ambient+diffColor+specularColor,1);
            }
            
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
