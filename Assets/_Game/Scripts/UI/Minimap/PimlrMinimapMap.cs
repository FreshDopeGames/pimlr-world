// PIMLR #MAP: a baked, flat 2D map of one scene for the minimap (made by PIMLR > Minimap > Bake 2D Map).
// Lives in Resources/PIMLR/Minimap/<SceneName>.asset so PimlrMinimap2D finds it at runtime by scene name.

using UnityEngine;

[CreateAssetMenu(menuName = "PIMLR/Minimap Map", fileName = "MinimapMap")]
public class PimlrMinimapMap : ScriptableObject
{
    public Texture2D texture;
    [Tooltip("World X / Z of the texture's bottom-left corner.")]
    public Vector2 worldMin;
    [Tooltip("World width (X) and depth (Z) the texture covers, in metres.")]
    public Vector2 worldSize;
    [Tooltip("Metres per texel the map was baked at.")]
    public float metresPerPixel = 0.4f;

    public static PimlrMinimapMap ForScene(string sceneName) => Resources.Load<PimlrMinimapMap>("PIMLR/Minimap/" + sceneName);
}
