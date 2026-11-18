using System.Linq; using System.Text; using UnityEngine; using UnityEngine.SceneManagement;
public static class CheckSwap {
  public static string Main() {
    var sb = new StringBuilder();
    var p = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
    if (!p) return "no Player root";
    var a = p.GetComponent<Animator>(); sb.AppendLine($"avatar={(a.avatar ? a.avatar.name : "null")} valid={(a.avatar && a.avatar.isValid)}");
    foreach (var r in p.GetComponentsInChildren<SkinnedMeshRenderer>(true))
      sb.AppendLine($"{r.name} active={r.gameObject.activeSelf} mesh={(r.sharedMesh ? r.sharedMesh.name + " v" + r.sharedMesh.vertexCount : "null")} mat={(r.sharedMaterial ? r.sharedMaterial.name : "null")} nullBones={r.bones.Count(b => !b)}");
    var h = a.GetBoneTransform(HumanBodyBones.Head); sb.AppendLine("head world y " + (h ? (h.position.y - p.transform.position.y).ToString("F3") : "?"));
    sb.AppendLine("dirty=" + SceneManager.GetActiveScene().isDirty);
    return sb.ToString(); } }
