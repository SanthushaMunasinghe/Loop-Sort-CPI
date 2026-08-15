// Toony Colors Pro+Mobile 2
// (c) 2014-2023 Jean Moreno

Shader "Toony Colors Pro 2/User/Carpet"
{
	Properties
	{
		[TCP2HeaderHelp(Base)]
		_Color ("Color", Color) = (1,1,1,1)
		[TCP2ColorNoAlpha] _HColor ("Highlight Color", Color) = (0.75,0.75,0.75,1)
		[TCP2ColorNoAlpha] _SColor ("Shadow Color", Color) = (0.2,0.2,0.2,1)
		[MainTexture] _MainTex ("Albedo", 2D) = "white" {}
		[TCP2Separator]

		[TCP2Header(Ramp Shading)]
		_RampThreshold ("Threshold", Range(0.01,1)) = 0.5
		_RampSmoothing ("Smoothing", Range(0.001,1)) = 0.5
		[TCP2Separator]
		
		[TCP2HeaderHelp(Specular)]
		[TCP2ColorNoAlpha] _SpecularColor ("Specular Color", Color) = (0.5,0.5,0.5,1)
		_SpecularRoughnessPBR ("Roughness", Range(0,1)) = 0.5
		[TCP2Separator]
		
		[TCP2HeaderHelp(Normal Mapping)]
		_BumpMap ("Normal Map", 2D) = "bump" {}
		_BumpScale ("Scale", Float) = 1
		[TCP2Separator]
		
		[TCP2Vector4Floats(Contrast X,Contrast Y,Contrast Z,Smoothing,1,16,1,16,1,16,0.05,10)] _TriplanarSamplingStrength ("Triplanar Sampling Parameters", Vector) = (8,8,8,0.5)

		// Avoid compile error if the properties are ending with a drawer
		[HideInInspector] __dummy__ ("unused", Float) = 0
	}

	SubShader
	{
		Tags
		{
			"RenderType"="Opaque"
		}

		CGINCLUDE

		#include "UnityCG.cginc"
		#include "UnityLightingCommon.cginc"	// needed for LightColor

		// Texture/Sampler abstraction
		#define TCP2_TEX2D_WITH_SAMPLER(tex)						UNITY_DECLARE_TEX2D(tex)
		#define TCP2_TEX2D_NO_SAMPLER(tex)							UNITY_DECLARE_TEX2D_NOSAMPLER(tex)
		#define TCP2_TEX2D_SAMPLE(tex, samplertex, coord)			UNITY_SAMPLE_TEX2D_SAMPLER(tex, samplertex, coord)
		#define TCP2_TEX2D_SAMPLE_LOD(tex, samplertex, coord, lod)	UNITY_SAMPLE_TEX2D_SAMPLER_LOD(tex, samplertex, coord, lod)

		// Shader Properties
		sampler2D _BumpMap;
		TCP2_TEX2D_WITH_SAMPLER(_MainTex);
		
		// Shader Properties
		float4 _BumpMap_ST;
		float _BumpScale;
		float4 _MainTex_ST;
		fixed4 _Color;
		float _RampThreshold;
		float _RampSmoothing;
		fixed4 _HColor;
		fixed4 _SColor;
		float _SpecularRoughnessPBR;
		fixed4 _SpecularColor;

		float4 _TriplanarSamplingStrength;

		// Texture sampling with triplanar UVs
		float4 tex2D_triplanar(sampler2D samp, float4 tiling_offset, float3 worldPos, float3 worldNormal)
		{
			half4 sample_y = ( tex2D(samp, worldPos.xz * tiling_offset.xy + tiling_offset.zw).rgba );
			half4 sample_x = ( tex2D(samp, worldPos.zy * tiling_offset.xy + tiling_offset.zw).rgba );
			half4 sample_z = ( tex2D(samp, worldPos.xy * tiling_offset.xy + tiling_offset.zw).rgba );
			
			// blending
			half3 blendWeights = pow(abs(worldNormal), _TriplanarSamplingStrength.xyz / _TriplanarSamplingStrength.w);
			blendWeights = blendWeights / (blendWeights.x + abs(blendWeights.y) + blendWeights.z);
			half4 triplanar = sample_x * blendWeights.x + sample_y * blendWeights.y + sample_z * blendWeights.z;
			
			return triplanar;
		}
			
		//Specular help functions (from UnityStandardBRDF.cginc)
		inline float3 SpecSafeNormalize(float3 inVec)
		{
			half dp3 = max(0.001f, dot(inVec, inVec));
			return inVec * rsqrt(dp3);
		}
		
			//GGX
			#define TCP2_PI			3.14159265359
			#define TCP2_INV_PI		0.31830988618f
			#if defined(SHADER_API_MOBILE)
				#define TCP2_EPSILON 1e-4f
			#else
				#define TCP2_EPSILON 1e-7f
			#endif
			inline half GGX(half NdotH, half roughness)
			{
				half a2 = roughness * roughness;
				half d = (NdotH * a2 - NdotH) * NdotH + 1.0f;
				return TCP2_INV_PI * a2 / (d * d + TCP2_EPSILON);
			}

		ENDCG

		// Main Surface Shader

		CGPROGRAM

		#pragma surface surf ToonyColorsCustom vertex:vertex_surface exclude_path:deferred exclude_path:prepass keepalpha halfasview nolightmap nofog nolppv
		#pragma target 3.0

		//================================================================
		// STRUCTS

		// Vertex input
		struct appdata_tcp2
		{
			float4 vertex : POSITION;
			float3 normal : NORMAL;
			float4 texcoord0 : TEXCOORD0;
			float4 texcoord1 : TEXCOORD1;
			float4 texcoord2 : TEXCOORD2;
			half4 tangent : TANGENT;
			UNITY_VERTEX_INPUT_INSTANCE_ID
		};

		struct Input
		{
			half3 viewDir;
			half3 tangent;
			float3 worldPos;
			half3 worldNormal; INTERNAL_DATA
			float2 texcoord0;
		};

		//================================================================

		// Custom SurfaceOutput
		struct SurfaceOutputCustom
		{
			half atten;
			half3 Albedo;
			half3 Normal;
			half3 worldNormal;
			half3 Emission;
			half Specular;
			half Gloss;
			half Alpha;

			Input input;

			// Shader Properties
			float __rampThreshold;
			float __rampSmoothing;
			float3 __highlightColor;
			float3 __shadowColor;
			float __ambientIntensity;
			float __specularRoughnessPbr;
			float3 __specularColor;
		};

		//================================================================
		// VERTEX FUNCTION

		void vertex_surface(inout appdata_tcp2 v, out Input output)
		{
			UNITY_INITIALIZE_OUTPUT(Input, output);

			// Texture Coordinates
			output.texcoord0.xy = v.texcoord0.xy * _MainTex_ST.xy + _MainTex_ST.zw;

			output.tangent = mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0)).xyz;

		}

		//================================================================
		// SURFACE FUNCTION

		void surf(Input input, inout SurfaceOutputCustom output)
		{

			input.worldNormal = WorldNormalVector(input, output.Normal);

			// Shader Properties Sampling
			float4 __normalMap = ( tex2D_triplanar(_BumpMap, float4(1, 1, 1, 1) * _BumpMap_ST, input.worldPos, input.worldNormal) );
			float __bumpScale = ( _BumpScale );
			float4 __albedo = ( TCP2_TEX2D_SAMPLE(_MainTex, _MainTex, input.texcoord0.xy).rgba );
			float4 __mainColor = ( _Color.rgba );
			float __alpha = ( __albedo.a * __mainColor.a );
			output.__rampThreshold = ( _RampThreshold );
			output.__rampSmoothing = ( _RampSmoothing );
			output.__highlightColor = ( _HColor.rgb );
			output.__shadowColor = ( _SColor.rgb );
			output.__ambientIntensity = ( 1.0 );
			output.__specularRoughnessPbr = ( _SpecularRoughnessPBR );
			output.__specularColor = ( _SpecularColor.rgb );

			output.input = input;

			half4 normalMap = __normalMap;
			output.Normal = UnpackScaleNormal(normalMap, __bumpScale);

			half3 worldNormal = WorldNormalVector(input, output.Normal);
			output.worldNormal = worldNormal;

			output.Albedo = __albedo.rgb;
			output.Alpha = __alpha;

			output.Albedo *= __mainColor.rgb;

		}

		//================================================================
		// LIGHTING FUNCTION

		inline half4 LightingToonyColorsCustom(inout SurfaceOutputCustom surface, half3 viewDir, UnityGI gi)
		{

			half3 lightDir = gi.light.dir;
			#if defined(UNITY_PASS_FORWARDBASE)
				half3 lightColor = _LightColor0.rgb;
				half atten = surface.atten;
			#else
				// extract attenuation from point/spot lights
				half3 lightColor = _LightColor0.rgb;
				half atten = max(gi.light.color.r, max(gi.light.color.g, gi.light.color.b)) / max(_LightColor0.r, max(_LightColor0.g, _LightColor0.b));
			#endif

			half3 normal = normalize(surface.Normal);
			half ndl = dot(normal, lightDir);
			half3 ramp;
			
			#define		RAMP_THRESHOLD	surface.__rampThreshold
			#define		RAMP_SMOOTH		surface.__rampSmoothing
			ndl = saturate(ndl);
			ramp = smoothstep(RAMP_THRESHOLD - RAMP_SMOOTH*0.5, RAMP_THRESHOLD + RAMP_SMOOTH*0.5, ndl);

			// Apply attenuation (shadowmaps & point/spot lights attenuation)
			ramp *= atten;

			// Highlight/Shadow Colors
			#if !defined(UNITY_PASS_FORWARDBASE)
				ramp = lerp(half3(0,0,0), surface.__highlightColor, ramp);
			#else
				ramp = lerp(surface.__shadowColor, surface.__highlightColor, ramp);
			#endif

			// Output color
			half4 color;
			color.rgb = surface.Albedo * lightColor.rgb * ramp;
			color.a = surface.Alpha;

			// Apply indirect lighting (ambient)
			half occlusion = 1;
			#ifdef UNITY_LIGHT_FUNCTION_APPLY_INDIRECT
				half3 ambient = gi.indirect.diffuse;
				ambient *= surface.Albedo * occlusion * surface.__ambientIntensity;

				color.rgb += ambient;
			#endif

			half3 halfDir = SpecSafeNormalize(float3(lightDir) + float3(viewDir));
			
			//Specular: GGX
			half roughness = surface.__specularRoughnessPbr*surface.__specularRoughnessPbr;
			half nh = saturate(dot(normal, halfDir));
			half spec = GGX(nh, saturate(roughness));
			spec *= TCP2_PI * 0.05;
			#ifdef UNITY_COLORSPACE_GAMMA
				spec = max(0, sqrt(max(1e-4h, spec)));
				half surfaceReduction = 1.0 - 0.28 * roughness * surface.__specularRoughnessPbr;
			#else
				half surfaceReduction = 1.0 / (roughness*roughness + 1.0);
			#endif
			spec = max(0, spec * ndl);
			spec *= surfaceReduction;
			spec *= atten;
			
			//Apply specular
			color.rgb += spec * lightColor.rgb * surface.__specularColor;

			return color;
		}

		void LightingToonyColorsCustom_GI(inout SurfaceOutputCustom surface, UnityGIInput data, inout UnityGI gi)
		{
			half3 normal = surface.Normal;

			// GI without reflection probes
			gi = UnityGlobalIllumination(data, 1.0, normal); // occlusion is applied in the lighting function, if necessary

			surface.atten = data.atten; // transfer attenuation to lighting function
			gi.light.color = _LightColor0.rgb; // remove attenuation

		}

		ENDCG

	}

	Fallback "Diffuse"
	CustomEditor "ToonyColorsPro.ShaderGenerator.MaterialInspector_SG2"
}

/* TCP_DATA u config(ver:"2.9.16";unity:"2022.3.62f3";tmplt:"SG2_Template_Default";features:list["UNITY_5_4","UNITY_5_5","UNITY_5_6","UNITY_2017_1","UNITY_2018_1","UNITY_2018_2","UNITY_2018_3","UNITY_2019_1","UNITY_2019_2","UNITY_2019_3","UNITY_2019_4","UNITY_2020_1","UNITY_2021_1","UNITY_2021_2","UNITY_2022_2","SPECULAR","TRIPLANAR_BUMP","TRIPLANAR_OBJ_POS_OFFSET","BUMP","BUMP_SCALE","SPEC_PBR_GGX"];flags:list["halfasview"];flags_extra:dict[];keywords:dict[RENDER_TYPE="Opaque",RampTextureDrawer="[TCP2Gradient]",RampTextureLabel="Ramp Texture",SHADER_TARGET="3.0"];shaderProperties:list[,,,,,,,,,,sp(name:"Normal Map";imps:list[imp_mp_texture(uto:True;tov:"";tov_lbl:"";gto:False;sbt:False;scr:False;scv:"";scv_lbl:"";gsc:False;roff:False;goff:False;sin_anm:False;sin_anmv:"";sin_anmv_lbl:"";gsin:False;notile:False;triplanar_local:False;def:"bump";locked_uv:False;uv:6;cc:4;chan:"RGBA";mip:-1;mipprop:False;ssuv_vert:False;ssuv_obj:False;uv_type:Triplanar;uv_chan:"XZ";tpln_scale:1;uv_shaderproperty:__NULL__;uv_cmp:__NULL__;sep_sampler:__NULL__;prop:"_BumpMap";md:"";gbv:False;custom:False;refs:"";pnlock:False;guid:"ec497802-b2f0-4f4b-9348-6e74951c3d3e";op:Multiply;lbl:"Normal Map";gpu_inst:False;dots_inst:False;locked:False;impl_index:0)];layers:list[];unlocked:list[];layer_blend:dict[];custom_blend:dict[];clones:dict[];isClone:False)];customTextures:list[];codeInjection:codeInjection(injectedFiles:list[];mark:False);matLayers:list[]) */
/* TCP_HASH 7285f50e5cdd4846cd9993ec5a8760be */
