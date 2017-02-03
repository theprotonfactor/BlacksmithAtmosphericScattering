using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class AtmosphericScattering : MonoBehaviour {
	public enum OcclusionDownscale { x1 = 1, x2 = 2, x4 = 4 }
	public enum OcclusionSamples { x64 = 0, x164 = 1, x244 = 2 }
	public enum ScatterDebugMode { None, Scattering, Occlusion, OccludedScattering, Rayleigh, Mie, Height }
	public enum DepthTexture { Enable, Disable, Ignore }

	[Header("World Components")]
	public Gradient	worldRayleighColorRamp				= null;
	public float	worldRayleighColorIntensity			= 1f;
    [Range(0f, 1000f)]
	public float	worldRayleighDensity				= 10f;
	public float	worldRayleighExtinctionFactor		= 1.1f;
    [Range(0f, 1f)]
	public float	worldRayleighIndirectScatter		= 0.33f;
	public Gradient	worldMieColorRamp					= null;
	public float	worldMieColorIntensity				= 1f;
    [Range(0f, 1000f)]
	public float	worldMieDensity						= 15f;
	public float	worldMieExtinctionFactor			= 0f;
    [Range(0f, 1f)]
	public float	worldMiePhaseAnisotropy				= 0.9f;
    [Range(-200f, 300f)]
	public float	worldNearScatterPush				= 0f;
    [Range(1f, 10000f)]
	public float	worldNormalDistance					= 1000f;

	[Header("Height Components")]
	public Color	heightRayleighColor					= Color.white;
	public float	heightRayleighIntensity				= 1f;
    [Range(0f, 1000f)]
	public float	heightRayleighDensity				= 10f;
    [Range(0f, 1000f)]
	public float	heightMieDensity					= 0f;
	public float	heightExtinctionFactor				= 1.1f;
	public float	heightSeaLevel						= 0f;
	public float	heightDistance						= 50f;
	public Vector3	heightPlaneShift					= Vector3.zero;
    [Range(-200f, 300f)]
	public float	heightNearScatterPush				= 0f;
    [Range(1f, 10000f)]
	public float	heightNormalDistance				= 1000f;

	[Header("Sky Dome")]
	public Vector3		skyDomeScale					= new Vector3(1f, 0.1f, 1f);
	public Vector3		skyDomeRotation					= Vector3.zero;
	public Transform	skyDomeTrackedYawRotation		= null;
	public bool			skyDomeVerticalFlip				= false;
	public Cubemap		skyDomeCube						= null;
    [Range(0f, 8f)]
	public float		skyDomeExposure					= 1f;
	public Color		skyDomeTint						= Color.white;
	[HideInInspector] public Vector3 skyDomeOffset		= Vector3.zero;

	[Header("Scatter Occlusion")]
	public bool					useOcclusion			= false;
    [Range(0f, 1f)]
    public float				occlusionBias			= 0f;
    [Range(0f, 1f)]
    public float				occlusionBiasIndirect	= 0.6f;
    [Range(0f, 1f)]
    public float				occlusionBiasClouds		= 0.3f;
	public OcclusionDownscale	occlusionDownscale		= OcclusionDownscale.x2;
	public OcclusionSamples		occlusionSamples		= OcclusionSamples.x64;
	public bool					occlusionDepthFixup		= true;
	public float				occlusionDepthThreshold	= 25f;
	public bool					occlusionFullSky		= false;
    [Range(0f, 1f)]
    public float				occlusionBiasSkyRayleigh= 0.2f;
    [Range(0f, 1f)]
    public float				occlusionBiasSkyMie		= 0.4f;

    [Header("Position")]
    public GameObject skySphere;
    public bool followCamera = true;

    [Header("Other")]
    [Range(1f, 2f)]
    public float			worldScaleExponent			= 1.0f;
	public bool				forcePerPixel				= false;
	public bool				forcePostEffect				= false;
	[Tooltip("Soft clouds need depth values. Ignore means externally controlled.")]
	public DepthTexture		depthTexture				= DepthTexture.Enable;
	public ScatterDebugMode	debugMode					= ScatterDebugMode.None;
	
	[HideInInspector] public Shader occlusionShader;
    [HideInInspector] public Light m_ActiveSun;

	bool			m_isAwake;

	Camera			m_currentCamera;
    Transform       m_cameraTransform;
    Transform       m_SunTranform;
	Material		m_occlusionMaterial;
    float           m_shadowDistance;
    Vector3         m_cameraPositionOld;
    Vector3         m_cameraRotationOld;
    float           m_cameraFarClip;
    float           m_cameraFOV;
    float           m_cameraAspect;

	UnityEngine.Rendering.CommandBuffer m_occlusionCmdAfterShadows, m_occlusionCmdBeforeScreen;
	
	public static AtmosphericScattering instance { get; private set; }

    private CommandBuffer[] buffer1;
    private CommandBuffer[] buffer2;

    int occlusionId;
    int cameraPositionId;
    int viewportCornderId;
    int viewportRightId;
    int viewportUpId;
    int refDistanceId;

    bool usingDeferred;


    void Awake() {

		if(!GetComponent<MeshFilter>()) {
			var mf = gameObject.AddComponent<MeshFilter>();
			mf.sharedMesh = new Mesh();
			mf.sharedMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
			mf.sharedMesh.SetTriangles((int[])null, 0);
		}
		if(!GetComponent<MeshRenderer>()) {
			var mr = gameObject.AddComponent<MeshRenderer>();
			mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
			mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
		}

		if(occlusionShader == null)
			occlusionShader = Shader.Find("Hidden/AtmosphericScattering_Occlusion");

		m_occlusionMaterial = new Material(occlusionShader);
		m_occlusionMaterial.hideFlags = HideFlags.HideAndDontSave;

		if(worldRayleighColorRamp == null) {
			worldRayleighColorRamp = new Gradient();
			worldRayleighColorRamp.SetKeys(
				new[]{ new GradientColorKey(new Color(0.3f, 0.4f, 0.6f), 0f), new GradientColorKey(new Color(0.5f, 0.6f, 0.8f), 1f) },
			new[]{ new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
			);
		}
		if(worldMieColorRamp == null) {
			worldMieColorRamp = new Gradient();
			worldMieColorRamp.SetKeys(
				new[]{ new GradientColorKey(new Color(0.95f, 0.75f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.9f, 8.0f), 1f) },
			new[]{ new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
			);
		}

		m_isAwake = true;
	}


	void OnEnable() {
		if(!m_isAwake)
			return;

		UpdateKeywords(true);
		UpdateStaticUniforms();

        if (instance && instance != this)
            DestroyImmediate(instance);
		
		instance = this;

        if (AtmosphericScatteringSun.instance != null)
             m_ActiveSun = AtmosphericScatteringSun.instance.sunLight;

        m_SunTranform = m_ActiveSun.transform;

        if (!HasBuffers(m_ActiveSun))
        {
            AddBuffers(m_ActiveSun);
        }

        occlusionId = Shader.PropertyToID("u_OcclusionTexture");

        cameraPositionId = Shader.PropertyToID("_CameraPosition");
        viewportCornderId = Shader.PropertyToID("_ViewportCorner");
        viewportRightId = Shader.PropertyToID("_ViewportRight");
        viewportUpId = Shader.PropertyToID("_ViewportUp");
        refDistanceId = Shader.PropertyToID("_OcclusionSkyRefDistance");

        if (m_currentCamera != Camera.main)
            SetCurrentCamera(Camera.main);
    }

    public void SetCurrentCamera(Camera camera)
    {
        if (camera == null)
            return;

        m_currentCamera = camera;
        m_cameraTransform = camera.transform;

        if ((SystemInfo.graphicsShaderLevel >= 40 || depthTexture == DepthTexture.Enable) && m_currentCamera.depthTextureMode == DepthTextureMode.None)
            m_currentCamera.depthTextureMode = DepthTextureMode.Depth;
        else if (depthTexture == DepthTexture.Disable && m_currentCamera.depthTextureMode != DepthTextureMode.None)
            m_currentCamera.depthTextureMode = DepthTextureMode.None;

        usingDeferred = m_currentCamera.actualRenderingPath == RenderingPath.DeferredShading;

        AtmosphericScatteringDeferred postEffect = m_currentCamera.gameObject.GetComponent<AtmosphericScatteringDeferred>();

        if (usingDeferred || forcePostEffect)
        {
            if (postEffect == null)
            {
                m_currentCamera.gameObject.AddComponent(typeof(AtmosphericScatteringDeferred));
            }
            else
            {
                postEffect.enabled = true;
            }
        }
        else
        {
            if (postEffect != null)
            {
                postEffect.enabled = false;
            }
        }

        m_cameraPositionOld = m_cameraTransform.localPosition;
        m_cameraRotationOld = m_cameraTransform.localRotation.eulerAngles;
        m_cameraFOV = m_currentCamera.fieldOfView;
        m_cameraFarClip = m_currentCamera.farClipPlane;
        m_cameraAspect = m_currentCamera.aspect;
        m_shadowDistance = QualitySettings.shadowDistance;

        SetCameraParams();
    }

    bool Vec3Equals(Vector3 a, Vector3 b)
    {
        if (a.x != b.x)
            return false;
        if (a.y != b.y)
            return false;
        if (a.z != b.z)
            return false;

        return true;
    }

    public bool HasBuffers(Light lightSource)
    {
        if (m_occlusionCmdBeforeScreen == null || m_occlusionCmdAfterShadows == null)
            InitializeBuffers();

        if (!lightSource)
            return false;

        buffer1 = lightSource.GetCommandBuffers(LightEvent.BeforeScreenspaceMask);
        buffer2 = lightSource.GetCommandBuffers(LightEvent.AfterShadowMap);

        bool buffer1Added = false;
        bool buffer2Added = false;

        for (int i = 0; i < (int)Mathf.Max(buffer1.Length, buffer2.Length); i++)
        {
            if (i < buffer1.Length)
            {
                if (buffer1[i].Equals(m_occlusionCmdBeforeScreen))
                {
                    buffer1Added = true;
                    break;
                }
            }

            if (i < buffer2.Length)
            {
                if (buffer2[i].Equals(m_occlusionCmdAfterShadows))
                {
                    buffer2Added = true;
                    break;
                }
            }

        }

        return (buffer1Added && buffer2Added);
          
    }

    public void InitializeBuffers()
    {
        if (m_occlusionCmdAfterShadows != null)
            m_occlusionCmdAfterShadows.Dispose();
        if (m_occlusionCmdBeforeScreen != null)
            m_occlusionCmdBeforeScreen.Dispose();

        m_occlusionCmdAfterShadows = new UnityEngine.Rendering.CommandBuffer();
        m_occlusionCmdAfterShadows.name = "Scatter Occlusion Pass 1";
        m_occlusionCmdAfterShadows.SetGlobalTexture("u_CascadedShadowMap", new UnityEngine.Rendering.RenderTargetIdentifier(UnityEngine.Rendering.BuiltinRenderTextureType.CurrentActive));
        m_occlusionCmdBeforeScreen = new UnityEngine.Rendering.CommandBuffer();
        m_occlusionCmdBeforeScreen.name = "Scatter Occlusion Pass 2";
    }

    public void AddBuffers(Light lightSource)
    {
        lightSource.AddCommandBuffer(UnityEngine.Rendering.LightEvent.AfterShadowMap, m_occlusionCmdAfterShadows);
        lightSource.AddCommandBuffer(UnityEngine.Rendering.LightEvent.BeforeScreenspaceMask, m_occlusionCmdBeforeScreen);
    }

	void OnDisable() {
		UpdateKeywords(false);

        if (instance == this)
            instance = null;
	}

	void UpdateKeywords(bool enable) {
		Shader.DisableKeyword("ATMOSPHERICS");
		Shader.DisableKeyword("ATMOSPHERICS_PER_PIXEL");
		Shader.DisableKeyword("ATMOSPHERICS_OCCLUSION");
		Shader.DisableKeyword("ATMOSPHERICS_OCCLUSION_FULLSKY");
		Shader.DisableKeyword("ATMOSPHERICS_OCCLUSION_EDGE_FIXUP");
		Shader.DisableKeyword("ATMOSPHERICS_SUNRAYS");
		Shader.DisableKeyword("ATMOSPHERICS_DEBUG");

		if(enable) {
			if (!forcePerPixel)
				Shader.EnableKeyword("ATMOSPHERICS");
			else
				Shader.EnableKeyword("ATMOSPHERICS_PER_PIXEL");
			
			if(useOcclusion) {
				Shader.EnableKeyword("ATMOSPHERICS_OCCLUSION");

				if(occlusionDepthFixup && occlusionDownscale != OcclusionDownscale.x1)
					Shader.EnableKeyword("ATMOSPHERICS_OCCLUSION_EDGE_FIXUP");

				if(occlusionFullSky)
					Shader.EnableKeyword("ATMOSPHERICS_OCCLUSION_FULLSKY");
			}

			if(debugMode != ScatterDebugMode.None)
				Shader.EnableKeyword("ATMOSPHERICS_DEBUG");
		}
	}

	public void OnValidate() {
		if(!m_isAwake)
			return;

		if(instance == this) {
			OnDisable();
			OnEnable();
		}

#if UNITY_EDITOR
		UnityEditor.SceneView.RepaintAll();
#endif
	}

	void LateUpdate() {
		if(!m_isAwake)
			return;

        if (followCamera && skySphere != null)
        {
            Vector3 skyPos = skySphere.transform.localPosition;

            skySphere.transform.localPosition = new Vector3(m_cameraTransform.localPosition.x, skyPos.y, m_cameraTransform.localPosition.z); 
        }

		if(!m_ActiveSun) {
			// When there's no primary light, mie scattering and occlusion will be disabled, so there's
			// nothing for us to update.
			UpdateDynamicUniforms();
			return;
		}

		UpdateDynamicUniforms();

		if(useOcclusion) {

           // if (!usingDeferred || forcePostEffect)
            //{
                if (!Vec3Equals(m_cameraPositionOld, m_cameraTransform.localPosition) || !Vec3Equals(m_cameraRotationOld, m_cameraTransform.localRotation.eulerAngles) || m_cameraFOV != m_currentCamera.fieldOfView || m_cameraFarClip != m_currentCamera.farClipPlane || m_cameraAspect != m_currentCamera.aspect)
                {           
                    m_cameraPositionOld = m_cameraTransform.localPosition;
                    m_cameraRotationOld = m_cameraTransform.localRotation.eulerAngles;
                    m_cameraFOV = m_currentCamera.fieldOfView;
                    m_cameraFarClip = m_currentCamera.farClipPlane;
                    m_cameraAspect = m_currentCamera.aspect;
                    SetCameraParams();
                }
            //}

			Rect srcRect = m_currentCamera.pixelRect;
			float downscale = 1f / (float)(int)occlusionDownscale;
            int occWidth = Mathf.RoundToInt(srcRect.width * downscale);
            int occHeight = Mathf.RoundToInt(srcRect.height * downscale);
			      
            if (m_occlusionCmdAfterShadows == null || m_occlusionCmdBeforeScreen == null)
            {
                if (!HasBuffers(m_ActiveSun))
                {
                    AddBuffers(m_ActiveSun);
                }
            }

			m_occlusionCmdBeforeScreen.Clear();
			m_occlusionCmdBeforeScreen.GetTemporaryRT(occlusionId, occWidth, occHeight, 0, FilterMode.Bilinear, RenderTextureFormat.R8, RenderTextureReadWrite.sRGB);
			m_occlusionCmdBeforeScreen.Blit(
				(Texture)null, 
				occlusionId,
				m_occlusionMaterial,
				(int)occlusionSamples
			);
			m_occlusionCmdBeforeScreen.SetGlobalTexture(occlusionId, occlusionId);
		}
	}


    void SetCameraParams()
    {
        Vector3 camRgt = m_cameraTransform.right;
        Vector3 camUp = m_cameraTransform.up;
        Vector3 camFwd = m_cameraTransform.forward;

        float dy = Mathf.Tan(m_currentCamera.fieldOfView * 0.5f * 0.0174532924f);
        float dx = dy * m_currentCamera.aspect;


        float farClip = m_currentCamera.farClipPlane;
        float farClipDX = dx * farClip;
        float farClipDY = dy * farClip;

        Vector3 vpCenter = new Vector3(camFwd.x * farClip, camFwd.y * farClip, camFwd.z * farClip);
        Vector3 vpRight = new Vector3(camRgt.x * farClipDX, camRgt.y * farClipDX, camRgt.z * farClipDX);
        Vector3 vpUp = new Vector3(camUp.x * farClipDY, camUp.y * farClipDY, camUp.z * farClipDY);

        m_occlusionMaterial.SetVector(cameraPositionId, m_cameraTransform.position);
        m_occlusionMaterial.SetVector(viewportCornderId, new Vector3(vpCenter.x - vpRight.x - vpUp.x, vpCenter.y - vpRight.y - vpUp.y, vpCenter.z - vpRight.z - vpUp.z));
        m_occlusionMaterial.SetVector(viewportRightId, new Vector3(vpRight.x * 2f, vpRight.y * 2f, vpRight.z * 2f));
        m_occlusionMaterial.SetVector(viewportUpId, new Vector3(vpUp.x * 2f, vpUp.y * 2f, vpUp.z * 2f));


        float refDist = (Mathf.Min(farClip, m_shadowDistance) - 1f) / farClip;

        m_occlusionMaterial.SetFloat(refDistanceId, refDist);
    }

	public void UpdateStaticUniforms() {
		Shader.SetGlobalVector("u_SkyDomeOffset", skyDomeOffset);
		Shader.SetGlobalVector("u_SkyDomeScale", skyDomeScale);
		Shader.SetGlobalTexture("u_SkyDomeCube", skyDomeCube);
		Shader.SetGlobalFloat("u_SkyDomeExposure", skyDomeExposure);
		Shader.SetGlobalColor("u_SkyDomeTint", skyDomeTint);

		Shader.SetGlobalFloat("u_ShadowBias", useOcclusion ? occlusionBias : 1f);
		Shader.SetGlobalFloat("u_ShadowBiasIndirect", useOcclusion ? occlusionBiasIndirect : 1f);
		Shader.SetGlobalFloat("u_ShadowBiasClouds", useOcclusion ? occlusionBiasClouds : 1f);
		Shader.SetGlobalVector("u_ShadowBiasSkyRayleighMie", useOcclusion ? new Vector4(occlusionBiasSkyRayleigh, occlusionBiasSkyMie, 0f, 0f) : Vector4.zero);
		Shader.SetGlobalFloat("u_OcclusionDepthThreshold", occlusionDepthThreshold);

		Shader.SetGlobalFloat("u_WorldScaleExponent", worldScaleExponent);
		
		Shader.SetGlobalFloat("u_WorldNormalDistanceRcp", 1f/worldNormalDistance);
		Shader.SetGlobalFloat("u_WorldNearScatterPush", -Mathf.Pow(Mathf.Abs(worldNearScatterPush), worldScaleExponent) * Mathf.Sign(worldNearScatterPush));
		
		Shader.SetGlobalFloat("u_WorldRayleighDensity", -worldRayleighDensity / 100000f);
		Shader.SetGlobalFloat("u_MiePhaseAnisotropy", worldMiePhaseAnisotropy);
		Shader.SetGlobalVector("u_RayleighInScatterPct", new Vector4(1f - worldRayleighIndirectScatter, worldRayleighIndirectScatter, 0f, 0f));
		
		Shader.SetGlobalFloat("u_HeightNormalDistanceRcp", 1f/heightNormalDistance);
		Shader.SetGlobalFloat("u_HeightNearScatterPush", -Mathf.Pow(Mathf.Abs(heightNearScatterPush), worldScaleExponent) * Mathf.Sign(heightNearScatterPush));
		Shader.SetGlobalFloat("u_HeightRayleighDensity", -heightRayleighDensity / 100000f);
		
		Shader.SetGlobalFloat("u_HeightSeaLevel", heightSeaLevel);
		Shader.SetGlobalFloat("u_HeightDistanceRcp", 1f/heightDistance);
		Shader.SetGlobalVector("u_HeightPlaneShift", heightPlaneShift);
		Shader.SetGlobalVector("u_HeightRayleighColor", (Vector4)heightRayleighColor * heightRayleighIntensity);
		Shader.SetGlobalFloat("u_HeightExtinctionFactor", heightExtinctionFactor);
		Shader.SetGlobalFloat("u_RayleighExtinctionFactor", worldRayleighExtinctionFactor);
		Shader.SetGlobalFloat("u_MieExtinctionFactor", worldMieExtinctionFactor);
		
		Color rayleighColorM20 = worldRayleighColorRamp.Evaluate(0.00f);
        Color rayleighColorM10 = worldRayleighColorRamp.Evaluate(0.25f);
        Color rayleighColorO00 = worldRayleighColorRamp.Evaluate(0.50f);
        Color rayleighColorP10 = worldRayleighColorRamp.Evaluate(0.75f);
        Color rayleighColorP20 = worldRayleighColorRamp.Evaluate(1.00f);

        Color mieColorM20 = worldMieColorRamp.Evaluate(0.00f);
        Color mieColorO00 = worldMieColorRamp.Evaluate(0.50f);
        Color mieColorP20 = worldMieColorRamp.Evaluate(1.00f);
		
		Shader.SetGlobalVector("u_RayleighColorM20", (Vector4)rayleighColorM20 * worldRayleighColorIntensity);
		Shader.SetGlobalVector("u_RayleighColorM10", (Vector4)rayleighColorM10 * worldRayleighColorIntensity);
		Shader.SetGlobalVector("u_RayleighColorO00", (Vector4)rayleighColorO00 * worldRayleighColorIntensity);
		Shader.SetGlobalVector("u_RayleighColorP10", (Vector4)rayleighColorP10 * worldRayleighColorIntensity);
		Shader.SetGlobalVector("u_RayleighColorP20", (Vector4)rayleighColorP20 * worldRayleighColorIntensity);
		
		Shader.SetGlobalVector("u_MieColorM20", (Vector4)mieColorM20 * worldMieColorIntensity);
		Shader.SetGlobalVector("u_MieColorO00", (Vector4)mieColorO00 * worldMieColorIntensity);
		Shader.SetGlobalVector("u_MieColorP20", (Vector4)mieColorP20 * worldMieColorIntensity);

		Shader.SetGlobalFloat("u_AtmosphericsDebugMode", (int)debugMode);
	}

	void UpdateDynamicUniforms() {
		bool hasSun = !!m_ActiveSun;

		float trackedYaw = skyDomeTrackedYawRotation ? skyDomeTrackedYawRotation.eulerAngles.y : 0f;
       
        Shader.SetGlobalMatrix("u_SkyDomeRotation", Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(skyDomeRotation.x, 0f, 0f) * Quaternion.Euler(0f, skyDomeRotation.y - trackedYaw, 0f), new Vector3(1f, skyDomeVerticalFlip ? -1f : 1f, 1f)));

        if (hasSun)
        {
            Shader.SetGlobalVector("u_SunDirection", -m_SunTranform.forward);
            Shader.SetGlobalFloat("u_WorldMieDensity", -worldMieDensity / 100000f);
            Shader.SetGlobalFloat("u_HeightMieDensity", -heightMieDensity / 100000f);
        }
        else
        {
            Shader.SetGlobalVector("u_SunDirection", Vector3.down);
            Shader.SetGlobalFloat("u_WorldMieDensity", 0f);
            Shader.SetGlobalFloat("u_HeightMieDensity", 0f);
        }

        //Rect pixelRect = m_currentCamera ? m_currentCamera.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
        Rect pixelRect = m_currentCamera.pixelRect;
        float scale = (int)occlusionDownscale;
		Vector4 depthTextureScaledTexelSize = new Vector4(scale / pixelRect.width, scale / pixelRect.height, -scale / pixelRect.width, -scale / pixelRect.height);
		Shader.SetGlobalVector("u_DepthTextureScaledTexelSize", depthTextureScaledTexelSize);
	}

    public Camera GetCurrentCamera()
    {
        return m_currentCamera;
    }
}