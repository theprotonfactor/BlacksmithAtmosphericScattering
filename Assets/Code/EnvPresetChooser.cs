using UnityEngine;
using System.Collections;
using System.Linq;

public class EnvPresetChooser : MonoBehaviour {
	public Transform[] presets { get { return transform.Cast<Transform>().ToArray(); } }

	public int GetActivePreset() {
		for(int i = 0, n = transform.childCount; i < n; ++i)
			if(transform.GetChild(i).gameObject.activeSelf)
				return i;

		return -1;
	}

	public void SetActivePreset(int index) {
		if(index < 0 || index >= transform.childCount)
			return;

		for(int i = 0, n = transform.childCount; i < n; ++i)
			transform.GetChild(i).gameObject.SetActive(false);
		
		transform.GetChild(index).gameObject.SetActive(true);

        //AtmosphericScattering.instance.SetCurrentCamera(Camera.current);
	}

#if UNITY_EDITOR && !UNITY_WEBPLAYER
	public void DumpAllScreens() {
		StartCoroutine(DoDumpAllScreens());
	}

	IEnumerator DoDumpAllScreens() {
		yield return null;
		
		var presets = this.presets;
		var oldActive = GetActivePreset();

		const int columns = 2;
		int rows = Mathf.CeilToInt(presets.Length / (float)columns);
		Debug.LogFormat("Dumping {0} screenshots and merging into {1} by {2} grids.", presets.Length, columns, rows);

		for(int e = 0, n = (int)AtmosphericScattering.ScatterDebugMode.Height; e <= n; ++e) {
			var paths = new string[presets.Length];

			for(int p = 0, m = presets.Length; p < m; ++p) {
				SetActivePreset(p);
				
				var scatter = presets[p].GetComponentInChildren<AtmosphericScattering>();
				var oldMode = scatter.debugMode;
				var oldPixel = scatter.forcePerPixel;
				var oldSamples = scatter.occlusionSamples;
				scatter.debugMode = (AtmosphericScattering.ScatterDebugMode)e;
				scatter.forcePerPixel = true;
				scatter.occlusionSamples = AtmosphericScattering.OcclusionSamples.x244;
				scatter.OnValidate();

				// make sure we tick enlighten updates for lit renders..
				if(p == 0)
					yield return new WaitForSeconds(0.33f);
				else
					yield return null;

				var path = paths[p] = string.Format("scatter_{0}_{1}.png", (AtmosphericScattering.ScatterDebugMode)e, presets[p].name);
				Application.CaptureScreenshot(path);

				yield return null;

				scatter.debugMode = oldMode;
				scatter.forcePerPixel = oldPixel;
				scatter.occlusionSamples = oldSamples;
			}

			Texture2D dsttex = null;
			for(int p = 0, m = presets.Length; p < m; ++p) {
				var srctex = new Texture2D(0, 0);
				srctex.LoadImage(System.IO.File.ReadAllBytes(paths[p]));

				if(dsttex == null)
					dsttex = new Texture2D(srctex.width * columns, srctex.height * rows, TextureFormat.ARGB32, false, false);

				dsttex.SetPixels((p % columns) * srctex.width, (rows - 1 - (p / columns)) * srctex.height, srctex.width, srctex.height, srctex.GetPixels());
				Object.DestroyImmediate(srctex);
			}

			System.IO.File.WriteAllBytes(string.Format("scatter_merged_{0}.png", ((AtmosphericScattering.ScatterDebugMode)e).ToString().ToLowerInvariant()), dsttex.EncodeToPNG());
			Object.DestroyImmediate(dsttex);
		}

		SetActivePreset(oldActive);
	}
#endif//UNITY_EDITOR && !UNITY_WEBPLAYER
}
