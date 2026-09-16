using UnityEngine;

/// <summary>
/// Minimal device-independent selection UI for a LocalAvatarLoader.
/// It uses Unity's built-in GUI so no new scene prefab or online service is required.
/// </summary>
public sealed class LocalAvatarSelectionUI : MonoBehaviour
{
    [SerializeField] private LocalAvatarLoader loader;
    [SerializeField] private Rect screenArea = new Rect(16f, 16f, 260f, 0f);
    [SerializeField] private bool showInGame = true;

    private void Awake()
    {
        if (loader == null)
        {
            loader = GetComponent<LocalAvatarLoader>();
        }

        if (loader == null)
        {
            loader = FindObjectOfType<LocalAvatarLoader>();
        }
    }

    private void OnGUI()
    {
        if (!showInGame || !Application.isPlaying || loader == null)
        {
            return;
        }

        var names = loader.AvailableAvatarNames;
        var height = Mathf.Clamp(66f + names.Count * 34f, 100f, 420f);
        var area = new Rect(screenArea.x, screenArea.y, screenArea.width, height);

        GUILayout.BeginArea(area, GUI.skin.box);
        GUILayout.Label("Local avatar");
        if (names.Count == 0)
        {
            GUILayout.Label("No GLB assets configured.");
        }
        else
        {
            for (var i = 0; i < names.Count; i++)
            {
                if (GUILayout.Button(names[i]))
                {
                    loader.LoadAvatar(names[i]);
                }
            }
        }
        GUILayout.EndArea();
    }
}