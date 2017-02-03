using UnityEngine;

[ExecuteInEditMode]
public class EnvPreset : MonoBehaviour {
	public Material skyboxMaterial;
	public float	ambientIntensity = 1;

	void OnEnable() {
		if(skyboxMaterial)
			RenderSettings.skybox = skyboxMaterial;

		if(ambientIntensity > 0f)
			RenderSettings.ambientIntensity = ambientIntensity;
	}

	void OnValidate() {
		ambientIntensity = Mathf.Clamp(ambientIntensity, 0f, 10f);

		if(enabled && gameObject.activeInHierarchy)
			OnEnable();
	}
}
