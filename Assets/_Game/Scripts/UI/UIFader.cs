using UnityEngine;
using System.Collections;
using Image = UnityEngine.UI.Image;

public class UIFader : MonoBehaviour {

	public Image img;
	public enum FADE { FadeIn, FadeOut }

	[Tooltip("If true, the black cover is opaque from the first frame of the scene (fixes the reload 'behind the curtain' flash). " +
	         "Use on the fader that sits above the loading screen / interstitial.")]
	public bool startCovered = false;

	[Tooltip("Use unscaled time so fades still run if Time.timeScale is 0 during load/interstitial.")]
	public bool useUnscaledTime = true;

	private Coroutine running;

	void Awake(){
		// Awake runs before the scene's first rendered frame, so there is no gap.
		if (startCovered) CoverInstant();
	}

	/// <summary>Snap to solid black right now (no delay). Call BEFORE hiding the loading screen.</summary>
	public void CoverInstant(){
		if (img == null) return;
		StopRunning();
		Color col = img.color;
		img.color = new Color(col.r, col.g, col.b, 1f);
		img.enabled = true;
	}

	public void Fade(FADE fadeDir, float fadeDuration, float StartDelay){
		if(img != null){

			// Stop any fade already in progress so two coroutines never fight over alpha.
			StopRunning();

			if (fadeDir == FADE.FadeIn){
				running = StartCoroutine(FadeCoroutine(1f, 0f, fadeDuration, StartDelay, true));
			}

			if (fadeDir == FADE.FadeOut){
				running = StartCoroutine(FadeCoroutine(0f, 1f, fadeDuration, StartDelay, false));
			}
		}
	}

	void StopRunning(){
		if (running != null){ StopCoroutine(running); running = null; }
	}

	IEnumerator FadeCoroutine(float From, float To, float Duration, float StartDelay, bool DisableOnFinish){
		if (StartDelay > 0f){
			// Hold whatever state the image is in during the delay (e.g. stay black) instead of snapping early.
			if (useUnscaledTime) yield return new WaitForSecondsRealtime(StartDelay);
			else yield return new WaitForSeconds(StartDelay);
		}

		float t = 0;
		Color col = img.color;
		img.enabled = true;
		img.color = new Color(col.r, col.g, col.b, From);

		while(t < 1){
			float alpha = Mathf.Lerp (From, To, t);
			img.color = new Color(col.r, col.g, col.b, alpha);
			t += (useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) / Duration;
			yield return null;
		}

		img.color = new Color(col.r, col.g, col.b, To);
		img.enabled = !DisableOnFinish;
		running = null;
	}
}
