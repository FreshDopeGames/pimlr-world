using System.Linq; using System.Text; using UnityEditor; using UnityEngine;
public static class ShoeYaw {
  // Measures the SHOE MESH toe-out (sole long axis) for: muscle-zero pose, and planted frames of a clip.
  static (float l, float r) Sole(SkinnedMeshRenderer smr, Transform root) {
    var m = new Mesh(); smr.BakeMesh(m, true);
    var w = m.vertices.Select(v => smr.transform.TransformPoint(v)).ToArray();
    var fwd = Vector3.ProjectOnPlane(root.forward, Vector3.up).normalized; var right = Vector3.Cross(Vector3.up, fwd);
    var mid = w.Aggregate(Vector3.zero, (a2, b2) => a2 + b2) / w.Length;
    float Side(int s) {
      var pts = w.Where(p => Vector3.Dot(p - mid, right) * s > 0).ToArray();
      float minY = pts.Min(p => p.y); var sole = pts.Where(p => p.y < minY + 0.04f * root.lossyScale.y).ToArray();
      var c = sole.Aggregate(Vector3.zero, (a, b) => a + b) / sole.Length;
      float xx=0,xz=0,zz=0; foreach (var p in sole){ var d=p-c; float x=Vector3.Dot(d,right), z=Vector3.Dot(d,fwd); xx+=x*x; xz+=x*z; zz+=z*z; }
      float ang = 0.5f*Mathf.Atan2(2*xz, zz-xx)*Mathf.Rad2Deg;   // long-axis angle from forward, + = toward right
      return ang * s; }
    Object.DestroyImmediate(m); return (Side(-1), Side(1));
  }
  public static string Main(string clipName) {
    var p = Object.FindAnyObjectByType<PimlrProneVisualOffset>(); var a = p.GetComponent<Animator>();
    var shoe = p.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Renderer_Outfit_Footwear");
    var sb = new StringBuilder();
    AnimationMode.StartAnimationMode();
    try {
      var clip = a.runtimeAnimatorController.animationClips.First(c => c.name == clipName);
      var toesL = a.GetBoneTransform(HumanBodyBones.LeftToes); var toesR = a.GetBoneTransform(HumanBodyBones.RightToes);
      var rec = new System.Collections.Generic.List<(float l,float r,float hl,float hr)>();
      for (int i = 0; i < 24; i++) {
        AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(p.gameObject, clip, clip.length * i / 24f); AnimationMode.EndSampling();
        var y = Sole(shoe, p.transform); rec.Add((y.l, y.r, toesL.position.y, toesR.position.y)); }
      float mL = rec.Min(x => x.hl), mR = rec.Min(x => x.hr), tol = 0.02f * p.transform.lossyScale.y;
      var L = rec.Where(x => x.hl < mL + tol).Select(x => x.l).ToArray(); var R = rec.Where(x => x.hr < mR + tol).Select(x => x.r).ToArray();
      sb.AppendLine($"{clipName}: planted SHOE toe-out L={L.Average():F1} ({L.Length}f) R={R.Average():F1} ({R.Length}f)  [+ = outward]");
    } finally { AnimationMode.StopAnimationMode(); }
    // muscle-zero (Unity's neutral human pose)
    var h = new HumanPoseHandler(a.avatar, a.transform); var hp = new HumanPose(); h.GetHumanPose(ref hp);
    hp.muscles = new float[HumanTrait.MuscleCount]; h.SetHumanPose(ref hp);
    var z = Sole(shoe, p.transform); sb.AppendLine($"muscle-zero pose: SHOE toe-out L={z.l:F1} R={z.r:F1}");
    h.Dispose();
    return sb.ToString(); } }
