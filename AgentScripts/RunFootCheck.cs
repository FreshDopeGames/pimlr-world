using System.Linq; using System.Text; using UnityEditor; using UnityEngine;
public static class RunFootCheck {
  // mode "list": list run-like clips on the player's controller. otherwise: sample clip <name> and report foot toe-out per frame
  public static string Main(string clipName) {
    var p = Object.FindAnyObjectByType<PimlrProneVisualOffset>(); if (!p) return "no player";
    var a = p.GetComponent<Animator>(); var ctrl = a.runtimeAnimatorController; if (!ctrl) return "no controller";
    var clips = ctrl.animationClips.Distinct().ToArray();
    if (clipName == "list") return string.Join("\n", clips.Where(c => c.name.ToLower().Contains("run") || c.name.ToLower().Contains("sprint") || c.name.ToLower().Contains("jog")).Select(c => $"{c.name} len={c.length:F2} human={c.humanMotion}"));
    var clip = clips.FirstOrDefault(c => c.name == clipName); if (!clip) return "clip not found";
    var sb = new StringBuilder(); var hips = a.GetBoneTransform(HumanBodyBones.Hips);
    AnimationMode.StartAnimationMode();
    try {
      float sumL = 0, sumR = 0; int nL = 0, nR = 0; float minL = 99, minR = 99;
      var rec = new System.Collections.Generic.List<(float t,float l,float r,float hl,float hr)>();
      for (int i = 0; i < 24; i++) {
        float t = clip.length * i / 24f;
        AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(p.gameObject, clip, t); AnimationMode.EndSampling();
        var fwd = Vector3.ProjectOnPlane(p.transform.forward, Vector3.up).normalized;
        float Yaw(HumanBodyBones foot, HumanBodyBones toe, float side) {
          var f = a.GetBoneTransform(foot).position; var to = a.GetBoneTransform(toe).position;
          var d = Vector3.ProjectOnPlane(to - f, Vector3.up).normalized;
          return Vector3.SignedAngle(fwd, d, Vector3.up) * side; }   // + = toe outward
        float l = Yaw(HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 1), r = Yaw(HumanBodyBones.RightFoot, HumanBodyBones.RightToes, -1);
        float hl = a.GetBoneTransform(HumanBodyBones.LeftToes).position.y, hr = a.GetBoneTransform(HumanBodyBones.RightToes).position.y;
        minL = Mathf.Min(minL, hl); minR = Mathf.Min(minR, hr); rec.Add((t, l, r, hl, hr));
      }
      float tol = 0.04f * p.transform.lossyScale.y;   // planted = toes within 4 cm of their lowest point
      foreach (var x in rec) { if (x.hl < minL + tol) { sumL += x.l; nL++; } if (x.hr < minR + tol) { sumR += x.r; nR++; }
        sb.AppendLine($"t={x.t:F2} L={x.l,6:F1} h={(x.hl-minL)/p.transform.lossyScale.y*100,5:F1}cm  R={x.r,6:F1} h={(x.hr-minR)/p.transform.lossyScale.y*100,5:F1}cm"); }
      sb.AppendLine($"planted-foot toe-out (bone axis) L={sumL/Mathf.Max(1,nL):F1} ({nL} frames) R={sumR/Mathf.Max(1,nR):F1} ({nR} frames)");
    } finally { AnimationMode.StopAnimationMode(); }
    return sb.ToString();
  }
}
