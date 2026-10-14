using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class NPCIdleVariation : MonoBehaviour
{
    [Range(0.8f, 1.2f)] public float minSpeed = 0.95f;
    [Range(0.8f, 1.2f)] public float maxSpeed = 1.05f;

    private IEnumerator Start()
    {
        var animator = GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // Wait a frame so the controller has entered its default state.
        yield return null;

        var info = animator.GetCurrentAnimatorStateInfo(0);
        animator.Play(info.fullPathHash, 0, Random.value); // random point in the loop
        animator.speed = Random.Range(minSpeed, maxSpeed);  // slight tempo difference
    }
}