using UnityEngine;

[RequireComponent(typeof(Camera))]
#if UNITY_5_4_OR_NEWER && UNITY_EDITOR
[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
#endif
public class AtmosphericScatteringDeferred : UnityStandardAssets.ImageEffects.PostEffectsBase {
	[HideInInspector] public Shader deferredFogShader = null;

    Matrix4x4 frustumCorners = Matrix4x4.identity;
    Material m_fogMaterial;
    Camera cam;
    Transform camtr;
    Vector3 camPosition;
    Vector3 camRotation;
    float camNear;
    float camFar;
    float camFov;
    float camAspect;

    void OnEnable()
    {
        cam = (Camera)GetComponent(typeof(Camera));

        if (AtmosphericScattering.instance == null)
        {
            this.enabled = false;
            return;
        }

        if (cam.actualRenderingPath != RenderingPath.DeferredShading && !AtmosphericScattering.instance.forcePostEffect)
        {
            this.enabled = false;
            return;
        }

        if (!CheckResources())
        {
            this.enabled = false;
            return;
        }

        
        camtr = cam.transform;
        camNear = cam.nearClipPlane;
        camFar = cam.farClipPlane;
        camFov = cam.fieldOfView;
        camAspect = cam.aspect;
        camPosition = camtr.localPosition;
        camRotation = camtr.localEulerAngles;
    }
	
	public override bool CheckResources() {
		CheckSupport (true);
		
		if(!deferredFogShader)
			deferredFogShader = Shader.Find("Hidden/AtmosphericScattering_Deferred");

		m_fogMaterial = CheckShaderAndCreateMaterial(deferredFogShader, m_fogMaterial);
		
		if(!isSupported)
			ReportAutoDisable();

		return isSupported;
	}
	
	[ImageEffectOpaque]
	void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        #if UNITY_EDITOR && UNITY_5_4_OR_NEWER
                        if (!Application.isPlaying)
                            AtmosphericScattering.instance.SetCurrentCamera(cam);
        #endif

        if (cam.nearClipPlane != camNear || camFar != cam.farClipPlane || camFov != cam.fieldOfView || camAspect != cam.aspect)
        {
            camNear = cam.nearClipPlane;
            camFar = cam.farClipPlane;
            camFov = cam.fieldOfView;
            camAspect = cam.aspect;
            camPosition = camtr.position;
            camRotation = camtr.localEulerAngles;

            DispatchFrustumPoints();   
        }
        else
        {
            if (!Vec3Equals(camtr.position, camPosition) || !Vec3Equals(camtr.localEulerAngles, camRotation))
            {
                camNear = cam.nearClipPlane;
                camFar = cam.farClipPlane;
                camFov = cam.fieldOfView;
                camAspect = cam.aspect;
                camPosition = camtr.position;
                camRotation = camtr.localEulerAngles;

                DispatchFrustumPoints();
            }
        }

        CustomGraphicsBlit(source, destination, m_fogMaterial, 0);
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

    void DispatchFrustumPoints()
    {
        float fovWHalf = camFov * 0.5f;
        float tanHalf = camNear * Mathf.Tan(fovWHalf * 0.0174532924f);
        float tanHalfAspect = tanHalf * camAspect;


        Vector3 toRight = new Vector3(camtr.right.x * tanHalfAspect, camtr.right.y * tanHalfAspect, camtr.right.z * tanHalfAspect);


        Vector3 toTop = new Vector3(camtr.up.x * tanHalf, camtr.up.y * tanHalf, camtr.up.z * tanHalf);


        Vector3 topLeft = new Vector3(camtr.forward.x * camNear - toRight.x + toTop.x, camtr.forward.y * camNear - toRight.y + toTop.y, camtr.forward.z * camNear - toRight.z + toTop.z);

        float camScale = topLeft.magnitude * camFar / camNear;

        topLeft.Normalize();
        topLeft = new Vector3(topLeft.x * camScale, topLeft.y * camScale, topLeft.z * camScale);


        Vector3 topRight = new Vector3(camtr.forward.x * camNear + toRight.x + toTop.x, camtr.forward.y * camNear + toRight.y + toTop.y, camtr.forward.z * camNear + toRight.z + toTop.z);

        topRight.Normalize();
        topRight = new Vector3(topRight.x * camScale, topRight.y * camScale, topRight.z * camScale);


        Vector3 bottomRight = new Vector3(camtr.forward.x * camNear + toRight.x - toTop.x, camtr.forward.y * camNear + toRight.y - toTop.y, camtr.forward.z * camNear + toRight.z - toTop.z);

        bottomRight.Normalize();
        bottomRight = new Vector3(bottomRight.x * camScale, bottomRight.y * camScale, bottomRight.z * camScale);


        Vector3 bottomLeft = new Vector3(camtr.forward.x * camNear - toRight.x - toTop.x, camtr.forward.y * camNear - toRight.y - toTop.y, camtr.forward.z * camNear - toRight.z - toTop.z);

        bottomLeft.Normalize();
        bottomLeft = new Vector3(bottomLeft.x * camScale, bottomLeft.y * camScale, bottomLeft.z * camScale);

        frustumCorners.SetRow(0, topLeft);
        frustumCorners.SetRow(1, topRight);
        frustumCorners.SetRow(2, bottomRight);
        frustumCorners.SetRow(3, bottomLeft);


        m_fogMaterial.SetMatrix("_FrustumCornersWS", frustumCorners);
        m_fogMaterial.SetVector("_CameraWS", camtr.position);
    }
	
	static void CustomGraphicsBlit(RenderTexture src, RenderTexture dst, Material mat, int pass) {
		RenderTexture.active = dst;
		
		mat.SetTexture("_MainTex", src);
		
		GL.PushMatrix();
		GL.LoadOrtho();
		
		mat.SetPass(pass);
		
		GL.Begin(GL.QUADS);
		
		GL.MultiTexCoord2(0, 0.0f, 0.0f);
		GL.Vertex3(0.0f, 0.0f, 3.0f); // BL
		
		GL.MultiTexCoord2(0, 1.0f, 0.0f);
		GL.Vertex3(1.0f, 0.0f, 2.0f); // BR
		
		GL.MultiTexCoord2(0, 1.0f, 1.0f);
		GL.Vertex3(1.0f, 1.0f, 1.0f); // TR
		
		GL.MultiTexCoord2(0, 0.0f, 1.0f);
		GL.Vertex3(0.0f, 1.0f, 0.0f); // TL
		
		GL.End();
		GL.PopMatrix();
	}
}
