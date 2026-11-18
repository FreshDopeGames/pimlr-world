using UnityEngine; using JUTPS.CharacterBrain;
public static class ForcePose {
  public static string Main(string pose){
    var p = GameObject.FindFirstObjectByType<PimlrProneVisualOffset>();
    if (p==null) return "no PimlrProneVisualOffset in play scene";
    var b = p.GetComponent<JUCharacterBrain>();
    if (pose=="prone") b._Prone(); else if (pose=="crouch") b._Crouch(); else { b.IsProne=false; b.IsCrouched=false; }
    var anim = p.GetComponent<Animator>(); var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
    return $"{p.name} pose={pose} IsCrouched={b.IsCrouched} IsProne={b.IsProne} hipsY-rootY={(hips.position.y-p.transform.position.y):F3} scale={p.transform.lossyScale.y}";
  }
}
