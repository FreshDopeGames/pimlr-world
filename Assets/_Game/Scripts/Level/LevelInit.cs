using UnityEngine;

public class LevelInit : MonoBehaviour
{
    [Space(5)]
    [Header("Settings")]
    public string showMenuAtStart = "";
    public bool createUI;
    public bool create_PIMLR_UI;
    public bool createPlayerManager;
    public bool createEnumManager;
    public bool authManager;

    // PIMLR (playtest): the Resources prefab is named "Global UI".
    [SerializeField] private string uiPrefabName = "Global UI";

    void Awake()
    {
        if (createUI) InstantiatePrefab<UIManager>(uiPrefabName);
        if (create_PIMLR_UI) InstantiatePrefab<UIManager>("PIMLR_UI");
        if (authManager) InstantiatePrefab<AuthManager>("AuthManager");

        if ((createUI || create_PIMLR_UI) && !string.IsNullOrEmpty(showMenuAtStart))
            ShowMenuAtStart();
    }

    void ShowMenuAtStart()
    {
        UIManager ui = FindObjectOfType<UIManager>();
        if (ui != null) ui.ShowMenu(showMenuAtStart);
        else Debug.LogWarning("LevelInit: no UIManager found, cannot show '" + showMenuAtStart + "'.", this);
    }

    void InstantiatePrefab<T>(string prefabName) where T : Component
    {
        if (FindObjectOfType<T>() != null) return;
        Object prefab = Resources.Load(prefabName);
        if (prefab == null)
        {
            Debug.LogError("LevelInit: Resources prefab '" + prefabName + "' was not found.", this);
            return;
        }
        Instantiate(prefab, Vector3.zero, Quaternion.identity);
    }
}