using System.Linq; using System.Text; using UnityEngine; using UnityEngine.SceneManagement;
public static class FindHips {
  static string P(Transform t){var s=t.name;while(t.parent){t=t.parent;s=t.name+"/"+s;}return s;}
  public static string Main(){
    var sb=new StringBuilder();
    var p=SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="Player");
    sb.AppendLine("Player pos "+p.transform.position+" scale "+p.transform.lossyScale);
    foreach(Transform c in p.transform) sb.AppendLine(" child "+c.name+" active="+c.gameObject.activeSelf+" comps="+string.Join(",",c.GetComponents<Component>().Select(x=>x?x.GetType().Name:"?")));
    foreach(var h in p.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Hips")) sb.AppendLine("HIPS "+P(h)+" childCount="+h.childCount+" active="+h.gameObject.activeInHierarchy);
    var a=p.GetComponent<Animator>(); var hb=a.GetBoneTransform(HumanBodyBones.Hips); sb.AppendLine("animator hips -> "+(hb?P(hb):"null"));
    return sb.ToString();}}
