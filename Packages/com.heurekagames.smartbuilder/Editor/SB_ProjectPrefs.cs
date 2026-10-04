using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace HeurekaGames.SmartBuilder
{
    public class SB_History : SB_ProjectPrefs<SB_History>
    {
        public SB_History() : base() { }
    }

    public class SB_Favorites : SB_ProjectPrefs<SB_History>
    {
        public SB_Favorites() : base() { }

        internal void MakeFavorite(string id, bool bFavorite)
        {
            if (bFavorite)
                Entries.Add(id);
            else if (Entries.Contains(id))
                Entries.Remove(id);
        }
    }

    public class SB_ProjectPrefs<T>
    {
        [SerializeField] public List<string> Entries = new List<string>();

        public SB_ProjectPrefs()
        {
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += save;
            AssemblyReloadEvents.afterAssemblyReload += load;
            EditorApplication.quitting += EditorApplication_quitting;

            load();
        }

        private void EditorApplication_playModeStateChanged(PlayModeStateChange playmode)
        {
            if (playmode == PlayModeStateChange.ExitingEditMode)
                save();
            else if (playmode == PlayModeStateChange.EnteredEditMode)
                load();
        }

        private void EditorApplication_quitting()
        {
            EditorApplication.quitting -= save;
            AssemblyReloadEvents.beforeAssemblyReload -= save;
            AssemblyReloadEvents.afterAssemblyReload -= load;

            save();
        }

        private void load()
        {
            EditorJsonUtility.FromJsonOverwrite(EditorPrefs.GetString(getIdentifier()), this);
        }

        private void save()
        {
            EditorPrefs.SetString(getIdentifier(), EditorJsonUtility.ToJson(this));
        }

        public bool TryRemoveObsoleteGUIDs()
        {
            return Entries.TryRemoveObsoleteGUIDs();
        }

        private string getIdentifier()
        {
            var hash = Application.dataPath.GetHashCode();

            //Unique ID for this project
            return $"SB_Preferences.AssetName_{hash.ToString()}_{this.GetType().Name}";
        }
    }
}
