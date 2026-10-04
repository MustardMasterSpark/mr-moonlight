using System.Linq;

namespace HeurekaGames.SmartReplacer
{
    public class SRMenuItems
    {
        [UnityEditor.MenuItem("Window/Heureka/SmartBuilder/SmartReplacer")]
        [UnityEditor.MenuItem("Tools/SmartBuilder/SmartReplacer")]
        [UnityEditor.MenuItem("GameObject/"+ComponentTransferWindow.WINDOWNAME+"/Advanced Replace", false, -100)]
        private static void ReplaceObjects()
        {
            ComponentTransferWindow.ShowConfig(new ComponentTransferConfig(UnityEditor.Selection.transforms.Select(t=>t.gameObject).ToArray(), null, false));
        }

        /// <summary>
        /// ReplaceObjectValidator
        /// </summary>
        [UnityEditor.MenuItem("GameObject/" + ComponentTransferWindow.WINDOWNAME + "/Advanced Replace", true, -100)]
        private static bool ReplaceObjectsValidate()
        {
            return UnityEditor.Selection.activeTransform != null;
        }
    }
}
