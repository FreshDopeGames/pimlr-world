using UnityEngine;

/// <summary>PIMLR #ARROW: look and feel of the objective arrow. Lives at Resources/PIMLR/ObjectiveArrow.asset.</summary>
[CreateAssetMenu(menuName = "PIMLR/Objective Arrow Settings", fileName = "ObjectiveArrow")]
public class PimlrObjectiveArrowSettings : ScriptableObject
{
    public const string ResourcePath = "PIMLR/ObjectiveArrow";

    [Header("Look")]
    [Tooltip("Bright yellow body (unlit so it reads at night and in shade).")]
    public Material fillMaterial;
    [Tooltip("Dark shell drawn behind the body (Cull Front). Leave empty for no outline.")]
    public Material outlineMaterial;
    [Range(0f, 0.1f)] public float outlineThickness = 0.035f;
    [Tooltip("Blender-made arrow (pointing +Z, ~1 m long, pivot in the middle). Source: ArtSource/PIMLR_ObjectiveArrow.blend. Empty = generated fallback mesh.")]
    public Mesh customMesh;
    [Tooltip("Matching outline shell from the same FBX (the arrow pushed out along its normals), drawn with Outline Material.")]
    public Mesh customOutlineMesh;

    [Header("Placement")]
    [Tooltip("Metres above the player's feet when on foot.")]
    public float heightOnFoot = 2.1f;
    [Tooltip("Metres above the vehicle's pivot when driving.")]
    public float heightInVehicle = 3.1f;
    public float sizeOnFoot = 0.55f;
    public float sizeInVehicle = 1.35f;
    [Tooltip("Height on the target the arrow dips toward (metres above its pivot).")]
    public float targetAimHeight = 1f;
    [Range(0f, 45f)] public float maxPitch = 22f;
    public float turnSpeed = 10f;
    [Tooltip("0 = arrow stays flat; 1 = its face fully rolls toward the camera around the pointing axis.")]
    [Range(0f, 1f)] public float faceCamera = 0.7f;

    [Header("Life")]
    public float bobHeight = 0.07f;
    public float bobSpeed = 2.6f;
    [Range(0f, 0.2f)] public float pulseAmount = 0.05f;
    public float pulseSpeed = 4f;
    [Tooltip("Seconds to pop in / out.")]
    public float popTime = 0.25f;

    [Header("Hide when close")]
    [Tooltip("Save The Fans: hide this far beyond the fan's arrival radius.")]
    public float extraHideMargin = 1.5f;
    [Tooltip("Story ending: hide when the police car is this close.")]
    public float policeHideDistance = 10f;

    static PimlrObjectiveArrowSettings cached;
    public static PimlrObjectiveArrowSettings Load()
    {
        if (cached) return cached;
        cached = Resources.Load<PimlrObjectiveArrowSettings>(ResourcePath);
        if (!cached) cached = CreateInstance<PimlrObjectiveArrowSettings>(); // defaults; materials fall back below
        if (!cached.fillMaterial)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            cached.fillMaterial = new Material(sh) { color = new Color(1f, 0.82f, 0.05f) };
        }
        return cached;
    }
}
