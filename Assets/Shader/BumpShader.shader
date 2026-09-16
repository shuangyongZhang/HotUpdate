Shader "zsy/BumpShader"
{
    Properties
    {
        [MainTexture]_MainTex ("Albedo (RGB)", 2D) = "white" {}
        _BumpMap ("Bump Map", 2D) = "bump" {}
        _MetallicMap ("Metallic Map", 2D) = "white" {}
        _EmissionMap ("Emission Map", 2D) = "white" {}

        [MainColor]_Color ("MainColor", Color) = (1,1,1,1)
        //发光颜色
        _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        //发光强度
        _Emission ("Emission", Range(0,1)) = 0.5
        //光滑度
        _Smoothness ("Smoothness", Range(0,1)) = 0.5
        //金属度
        _Metallic ("Metallic", Range(0,1)) = 0.5
        //反射颜色
        _Specular ("Specular Color", Color) = (1,1,1,1)
        //反射强度
        _SpecularScale ("Specular Scale", Range(0,10)) = 5
        //深度
        _BumpPower ("Bump Power", Range(0,10)) = 5
    }
    SubShader
    {

        Tags {
             "RenderPipeline" = "UniversalPipeline"
             "Queue" = "Geometry"
             "RenderType" = "Opaque"
            }
        pass
        {
            Name "ShadowCasterPass"
            Tags {
                "LightMode" = "ShadowCaster"
            }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
        pass
        {
            Name "BumpPass"
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicMap);
            SAMPLER(sampler_MetallicMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);
            
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BumpMap_ST;
            half4 _Color;
            half _Smoothness;
            half _Metallic;
            half4 _Specular;
            half _SpecularScale;
            half _BumpPower;
            half4 _EmissionColor;
            half _Emission;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float4 O2C0 : TEXCOORD1;
                float4 O2C1 : TEXCOORD2;
                float4 O2C2 : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                float3 worldPos=TransformObjectToWorld(v.positionOS);
                VertexNormalInputs inputs= GetVertexNormalInputs(v.normalOS, v.tangentOS);
                o.O2C0 = float4(inputs.tangentWS.x,inputs.bitangentWS.x,inputs.normalWS.x,worldPos.x);
                o.O2C1 = float4(inputs.tangentWS.y,inputs.bitangentWS.y,inputs.normalWS.y,worldPos.y);
                o.O2C2 = float4(inputs.tangentWS.z,inputs.bitangentWS.z,inputs.normalWS.z,worldPos.z);
                o.uv.xy = TRANSFORM_TEX(v.uv,_MainTex);
                o.uv.zw = TRANSFORM_TEX(v.uv,_BumpMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 worldPos = float3(i.O2C0.w,i.O2C1.w,i.O2C2.w);
                float3x3 TBN=float3x3(i.O2C0.xyz,i.O2C1.xyz,i.O2C2.xyz);
                float3 normal=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv.zw));
                float2 normalXY=normal.xy*_BumpPower;
                float normalz=sqrt(1.0- saturate(dot(normalXY,normalXY)));
                normal=normalize(float3(normalXY,normalz));
                float3 worldNormal=mul(TBN,normal);

                //物体固有颜色
                float3 albedo=_Color.rgb*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv.xy).rgb;

                float4 shadowColor=TransformWorldToShadowCoord(worldPos);
                Light mainLight=GetMainLight(shadowColor);
                float3 lightDir=normalize(mainLight.direction);
                float3 viewDir=GetWorldSpaceNormalizeViewDir(worldPos);

                //采样获得金属度
                float3 metallicMap = SAMPLE_TEXTURE2D(_MetallicMap,sampler_MetallicMap,i.uv.xy).rgb;
                float metallic=metallicMap.r*_Metallic;

                BRDFData brdfdata;
                half a=1.0;
                InitializeBRDFData(albedo,metallic,_Specular,_Smoothness,a,brdfdata);
                //主光源的 漫反射+高光反射
                half3 mainColor = LightingPhysicallyBased(brdfdata,mainLight, worldNormal,viewDir);
                //间接漫反射光照采样
                half3 bakedGI=SampleSH(worldNormal);
                //间接光的漫反射+高光反射
                float3 ambient = GlobalIllumination(brdfdata,bakedGI,1.0,worldPos,worldNormal,viewDir);
                //采样获得发光颜色
                float3 emissionMap = SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv.xy).rgb;
                float3 emissionColor=emissionMap*_EmissionColor.rgb*_Emission;
                
                return half4(mainColor+ambient+emissionColor,a);    
            }

            ENDHLSL
        }

    }
}