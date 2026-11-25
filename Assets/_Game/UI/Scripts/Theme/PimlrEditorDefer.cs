// #UI: run an action on the next Editor update tick (Editor only; at runtime it runs immediately).
// Used instead of EditorApplication.delayCall, which can stall while the Editor is in the background
// (seen in this project: hundreds of queued delayCalls not running while the update loop kept ticking).

using System;

namespace PIMLR.UI
{
    public static class PimlrEditorDefer
    {
        public static void NextTick(Action action)
        {
            if (action == null) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.CallbackFunction cb = null;
            cb = () =>
            {
                UnityEditor.EditorApplication.update -= cb;
                try { action(); }
                catch (Exception e) { UnityEngine.Debug.LogException(e); }
            };
            UnityEditor.EditorApplication.update += cb;
#else
            action();
#endif
        }
    }
}
