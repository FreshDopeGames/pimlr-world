using UnityEngine;
public static class ReviewCam {
  public static string Main(float dist, float height, float yaw) {
    var p = Object.FindAnyObjectByType<PimlrProneVisualOffset>(); if (!p) return "no player";
    var t = p.transform; float s = t.lossyScale.y;
    var go = GameObject.Find("AGENT_ReviewCam"); if (!go) go = new GameObject("AGENT_ReviewCam");
    var cam = go.GetComponent<Camera>(); if (!cam) cam = go.AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 35; cam.nearClipPlane = 0.05f;
    var dir = Quaternion.AngleAxis(yaw, Vector3.up) * t.forward;
    var target = t.position + Vector3.up * height * s;
    go.transform.position = target + dir * dist * s; go.transform.LookAt(target);
    return $"{p.name} scale={s} cam at {go.transform.position}";
  }
}
