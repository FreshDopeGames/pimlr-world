using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads avatar models imported from Assets/Avatars without contacting Ready Player Me.
/// The serialized definitions are refreshed in the editor so the references are included
/// in player builds (runtime code cannot enumerate the Unity project Assets directory).
/// </summary>
[DisallowMultipleComponent]
public sealed class LocalAvatarLoader : MonoBehaviour
{
    [Serializable]
    private struct AvatarDefinition
    {
        public string name;
        public GameObject asset;
    }

    [SerializeField] private Transform avatarRoot;
    [SerializeField] private List<AvatarDefinition> avatars = new List<AvatarDefinition>();
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private string defaultAvatarName;

    private GameObject activeAvatar;
    private readonly List<string> availableAvatarNames = new List<string>();

    public IReadOnlyList<string> AvailableAvatarNames => availableAvatarNames;
    public string SelectedAvatarName { get; private set; }
    public GameObject ActiveAvatar => activeAvatar;

    public event Action<string, GameObject> AvatarLoaded;
    public event Action<string> AvatarLoadFailed;

    private void Awake()
    {
        RebuildRuntimeCatalog();

        if (loadOnStart)
        {
            var initialName = GetInitialAvatarName();
            if (!string.IsNullOrEmpty(initialName))
            {
                LoadAvatar(initialName);
            }
        }
    }

    /// <summary>
    /// Instantiates the imported GLB whose filename/name matches avatarName.
    /// </summary>
    public bool LoadAvatar(string avatarName)
    {
        var definition = FindDefinition(avatarName);
        if (definition.asset == null)
        {
            Debug.LogWarning($"Local avatar '{avatarName}' was not found in Assets/Avatars.", this);
            AvatarLoadFailed?.Invoke(avatarName);
            return false;
        }

        if (activeAvatar != null)
        {
            Destroy(activeAvatar);
        }

        var parent = avatarRoot != null ? avatarRoot : transform;
        activeAvatar = Instantiate(definition.asset, parent);
        activeAvatar.name = definition.name;
        activeAvatar.transform.localPosition = Vector3.zero;
        activeAvatar.transform.localRotation = Quaternion.identity;
        activeAvatar.transform.localScale = Vector3.one;
        SelectedAvatarName = definition.name;

        PlayerPrefs.SetString("PIMLR.LocalAvatar", SelectedAvatarName);
        PlayerPrefs.Save();
        AvatarLoaded?.Invoke(SelectedAvatarName, activeAvatar);
        return true;
    }

    public bool LoadAvatarAt(int index)
    {
        if (index < 0 || index >= availableAvatarNames.Count)
        {
            return false;
        }

        return LoadAvatar(availableAvatarNames[index]);
    }

    public void RebuildRuntimeCatalog()
    {
        availableAvatarNames.Clear();
        for (var i = 0; i < avatars.Count; i++)
        {
            if (avatars[i].asset == null || string.IsNullOrEmpty(avatars[i].name))
            {
                continue;
            }

            if (!availableAvatarNames.Contains(avatars[i].name))
            {
                availableAvatarNames.Add(avatars[i].name);
            }
        }
    }

    private AvatarDefinition FindDefinition(string avatarName)
    {
        for (var i = 0; i < avatars.Count; i++)
        {
            if (string.Equals(avatars[i].name, avatarName, StringComparison.OrdinalIgnoreCase))
            {
                return avatars[i];
            }
        }

        return default;
    }

    private string GetInitialAvatarName()
    {
        var savedName = PlayerPrefs.GetString("PIMLR.LocalAvatar", string.Empty);
        if (!string.IsNullOrEmpty(savedName) && FindDefinition(savedName).asset != null)
        {
            return savedName;
        }

        if (!string.IsNullOrEmpty(defaultAvatarName) && FindDefinition(defaultAvatarName).asset != null)
        {
            return defaultAvatarName;
        }

        return availableAvatarNames.Count > 0 ? availableAvatarNames[0] : string.Empty;
    }

    private void OnDestroy()
    {
        AvatarLoaded = null;
        AvatarLoadFailed = null;
    }
}