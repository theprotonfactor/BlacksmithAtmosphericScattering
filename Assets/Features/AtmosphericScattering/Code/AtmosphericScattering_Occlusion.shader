Shader "Hidden/AtmosphericScattering_Occlusion" {

Properties
{
	_CameraPosition("Camera Position", Vector) = (0, 0, 0, 0)
	_ViewportCorner("Viewport Corner", Vector) = (0, 0, 0, 0)
	_ViewportRight("Viewport Right", Vector) = (0, 0, 0, 0)
	_ViewportUp("Viewport Up", Vector) = (0, 0, 0, 0)
	_CollectedOcclusionData("Occlusion Data", 2D) = "defaulttexture" {}
	_CollectedOcclusionData_TexelSize("Texel Size", Vector) = (0, 0, 0, 0)
	_CollectedOcclusionDataScaledTexelSize("Scaled Texel Size", Vector) = (0, 0, 0, 0)
	_OcclusionSkyRefDistance("Sky Distance", Float) = 0

}

CGINCLUDE
	#pragma target 3.0
	#pragma only_renderers d3d11 d3d9 opengl glcore
	
	#pragma multi_compile _ ATMOSPHERICS_OCCLUSION
	#pragma multi_compile _ ATMOSPHERICS_OCCLUSION_FULLSKY
	
	#if !defined(SHADER_API_D3D11)
		#undef ATMOSPHERICS_OCCLUSION_FULLSKY
	#endif
	
	/*this forces the HW PCF path required for correctly sampling the cascaded shadow map
	   render texture (a fix is scheduled for 5.2) */

#if defined(SHADOWS_DEPTH) || defined(SHADOWS_CUBE)
#define SHADOWS_NATIVE
#endif

	//#include "UnityCG.cginc"
	#include "AtmosphericScattering.cginc"

	float3 			_CameraPosition;
	float3 			_ViewportCorner;
	float3 			_ViewportRight;
	float3 			_ViewportUp;
	sampler2D		_CollectedOcclusionData;
	float4			_CollectedOcclusionData_TexelSize;
	float4			_CollectedOcclusionDataScaledTexelSize;
	float			_OcclusionSkyRefDistance;
	
	struct v2f {
		float4 pos	: SV_POSITION;
		float2 uv	: TEXCOORD0;
		float3 ray	: TEXCOORD2;
	};
	
	v2f vert(appdata_img v) {
		v2f o;
		o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
		o.uv = v.texcoord.xy;
		o.ray = _ViewportCorner + o.uv.x * _ViewportRight + o.uv.y * _ViewportUp;
		return o;
	}

	float frag_collect(const v2f i, const int it) {
		const float itF = 1.f / (float)it;
		const float itFM1 = 1.f / (float)(it - 1);
		
		float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
		float occlusion = 0.f;

#if !defined(ATMOSPHERICS_OCCLUSION_FULLSKY)
		UNITY_BRANCH
		#if UNITY_VERSION >= 550
					if (rawDepth < 0.0000001f)
						return 0.75f;
		#else
				if(rawDepth > 0.999999999f)
					return 0.75f;
		#endif
#endif
			
		
#if defined(ATMOSPHERICS_OCCLUSION_FULLSKY)
		UNITY_BRANCH
		#if UNITY_VERSION >= 550
					if (rawDepth < 0.0000001f){
		#else
				if(rawDepth > 0.999999999f) {
		#endif
			float3 worldDir = i.ray * _OcclusionSkyRefDistance;			
			float4 worldPos = float4(0.f, 0.f, 0.f, 1.f);
			
			float fracStep = 0.f;
			for(int i = 0; i < it; ++i, fracStep += itF) {
				worldPos.xyz = _CameraPosition + worldDir * fracStep * fracStep;
				
				float4 cascadeWeights = getCascadeWeights_splitSpheres(worldPos.xyz);
				bool inside = dot(cascadeWeights, float4(1,1,1,1)) < 4;
				float3 samplePos = getShadowCoord(worldPos, cascadeWeights);
				occlusion += inside ? UNITY_SAMPLE_SHADOW(u_CascadedShadowMap, samplePos) : 1.f;
			}
		} //else
#endif
		else
		{
			float depth = Linear01Depth(rawDepth);
			float3 worldDir = i.ray * depth;
			
			float4 worldPos = float4(_CameraPosition + worldDir, 1.f);
			float3 deltaStep = -worldDir * itFM1;
			
			for(int i = 0; i < it; ++i, worldPos.xyz += deltaStep) {
				float4 cascadeWeights = getCascadeWeights_splitSpheres(worldPos.xyz);
				bool inside = dot(cascadeWeights, float4(1,1,1,1)) < 4;
				float3 samplePos = getShadowCoord(worldPos, cascadeWeights);
				occlusion += inside ? UNITY_SAMPLE_SHADOW(u_CascadedShadowMap, samplePos) : 1.f;
			}
#if !defined(ATMOSPHERICS_OCCLUSION_FULLSKY)
			return occlusion * itF;
#endif
		}
#if defined(ATMOSPHERICS_OCCLUSION_FULLSKY)
		return occlusion * itF;
#endif

	}
	
	fixed4 frag_collect64(v2f i) : SV_Target { return frag_collect(i, 64); }
	fixed4 frag_collect164(v2f i) : SV_Target { return frag_collect(i, 164); }
	fixed4 frag_collect244(v2f i) : SV_Target { return frag_collect(i, 244); }

ENDCG

SubShader {
	ZTest Always Cull Off ZWrite Off
	
	Pass {
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_collect64
		ENDCG
	}
	
	Pass {
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_collect164
		ENDCG
	}
	
	Pass {
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_collect244
		ENDCG
	}
}
Fallback off
}

