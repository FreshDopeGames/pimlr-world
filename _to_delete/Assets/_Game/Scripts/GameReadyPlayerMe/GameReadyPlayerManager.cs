using ReadyPlayerMe.AvatarCreator;
using ReadyPlayerMe.Core;
using ReadyPlayerMe;
using UnityEngine;

public class GameReadyPlayerManager : MonoBehaviour
{
    public AvatarCreatorStateMachine avatarCreatorStateMachine;
    [SerializeField] private AvatarConfig inGameConfig;
    private AvatarObjectLoader avatarObjectLoader;
   
    private void OnEnable()
    {
        avatarCreatorStateMachine.AvatarSaved += OnAvatarSaved;
    }

    private void OnDisable()
    {
        avatarCreatorStateMachine.AvatarSaved -= OnAvatarSaved;
        avatarObjectLoader?.Cancel();
    }
  
    private void OnAvatarSaved(string avatarId)
    {
        avatarCreatorStateMachine.gameObject.SetActive(false);

        var startTime = Time.time;
        avatarObjectLoader = new AvatarObjectLoader();
        avatarObjectLoader.AvatarConfig = inGameConfig;
        avatarObjectLoader.OnCompleted += (sender, args) =>
        {
            AvatarAnimatorHelper.SetupAnimator(args.Metadata.BodyType, args.Avatar);
            DebugPanel.AddLogWithDuration("Created avatar loaded", Time.time - startTime);
        };

        avatarObjectLoader.LoadAvatar(AvatarEndpoints.GetAvatarPublicUrl(avatarId));
    }
}
