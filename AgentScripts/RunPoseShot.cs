using System.Linq; using UnityEditor; using UnityEngine;
public static class RunPoseShot {
  // Edit mode: sample a clip frame on the player, render it from a temp camera, save PNG, restore.
  public static string Main(string clipName, float frac, float yaw, string outPath) {
    var p = Object.FindAnyObjectByType<PimlrProneVisualOffset>(); var a = p.GetComponent<Animator>();
    var clip = a.runtimeAnimatorController.animationClips.First(c => c.name == clipName);
    AnimationMode.StartAnimationMode();
    var go = new GameObject("AGENT_ShotCam"); go.hideFlags = HideFlags.HideAndDontSave;
    try {
      AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(p.gameObject, clip, clip.length * frac); AnimationMode.EndSampling();
      float s = p.transform.lossyScale.y; var hips = a.GetBoneTransform(HumanBodyBones.Hips).position;
      var target = new Vector3(hips.x, p.transform.position.y + 0.45f * s, hips.z);
      var dir = Quaternion.AngleAxis(yaw, Vector3.up) * p.transform.forward;
      var cam = go.AddComponent<Camera>(); cam.fieldOfView = 40; cam.nearClipPlane = 0.05f;
      go.transform.position = target + dir * 1.8f * s + Vector3.up * 0.25f * s; go.transform.LookAt(target);
      var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
      RenderTexture.active = rt; var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
      System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG()); RenderTexture.active = null; cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
      return "saved " + outPath;
    } finally { Object.DestroyImmediate(go); AnimationMode.StopAnimationMode(); } } }
