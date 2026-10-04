using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace HeurekaGames.SmartBuilder
{
    public class SB_SimilarAssetData : ScriptableSingleton<SB_SimilarAssetData>
    {
        private Dictionary<string, string[]> assetSplitIndentifierCache;
        public SB_Favorites favorites;

        #region workerInfo
        private BackgroundWorker similarityWorker = new BackgroundWorker();
        private Action similarAssetsFoundCallback;
        private Dictionary<string, string[]> otherAssetsUnityLabels;
        public string WorkerStatus { get; private set; } = "Idle";
        public float WorkerCompletion { get; private set; } = 1f;
        #endregion

        public bool IsDirty { get; private set; } = false;
        public bool CacheOutOfDate { get; private set; } = false;
        public SB_AssetInfo Selected { get; private set; }
        public List<string> SelectedPrefabTags { get; private set; } = new List<string>();
        [SerializeField] private List<SB_AssetInfo> similarAssets = new List<SB_AssetInfo>();
        public List<SB_AssetInfo> SimilarAssets { get => similarAssets; private set => similarAssets = value; }

        public void InitializeIfNeeded()
        {
            if (assetSplitIndentifierCache == null)
                Initialize();
        }
        public void Initialize()
        {
            RefreshCache();
            initalizeWorker();

            favorites = new SB_Favorites();
        }
        public float RequiredSimilarityIndex
        {
            get
            {
                var req = 0f;
                req += SB_Preferences.instance.SimilarByDirectory.Value ? SB_Preferences.instance.MetricDirectoryRequirement.Value : 0f;
                req += SB_Preferences.instance.SimilarByFileName.Value ? SB_Preferences.instance.MetricFilenameRequirement.Value : 0f;
                req += (SB_Preferences.instance.SimilarByLabels.Value && Selected.labels.Count()>0) ? SB_Preferences.instance.MetricLabelRequirement.Value : 0f;
                req += SB_Preferences.instance.SimilarByTags.Value && Selected.tags.Count() > 0 ? SB_Preferences.instance.MetricTagRequirement.Value : 0f;
                return Mathf.Clamp(req*.5f, 0.1f, req * .5f); //Return half the requirement for each weight combined
            }
        }
        internal void RefreshCache()
        {
            assetSplitIndentifierCache = new Dictionary<string, string[]>();
            var prefabs = AssetDatabase.FindAssets("t:prefab"); //Find all prefabs

            string path;
            foreach (var prefab in prefabs)
            {
                path = AssetDatabase.GUIDToAssetPath(prefab);
                assetSplitIndentifierCache.Add(path, SB_Utils.GetFilenameSplit(Path.GetFileNameWithoutExtension(path)));
            }
            CacheOutOfDate = false;
        }

        internal void ValidateCache()
        {
            var prefabs = AssetDatabase.FindAssets("t:prefab"); //Find all prefabs
            CacheOutOfDate = (assetSplitIndentifierCache == null || prefabs.Count() != assetSplitIndentifierCache.Count());

            if (CacheOutOfDate && SB_Preferences.instance.AutoRefreshOnProjectChange.Value)
                RefreshCache();
        }

        private void initalizeWorker()
        {
            similarityWorker.WorkerReportsProgress = true;
            similarityWorker.WorkerSupportsCancellation = true;

            similarityWorker.DoWork += worker_work;
            similarityWorker.RunWorkerCompleted += worker_complete;
            similarityWorker.ProgressChanged += worker_Progress;
        }

        private void worker_Progress(object sender, ProgressChangedEventArgs args)
        {
            WorkerStatus = (string)args.UserState;
            WorkerCompletion = (float)args.ProgressPercentage / 100f;
        }

        private void worker_work(object sender, DoWorkEventArgs args)
        {
            var relevantIgnoreList = Selected.tags.Where(x => SB_Preferences.instance.IgnoredTags.Entries.Contains(x)).ToArray();

            if (SB_Preferences.instance.SimilarByTags.Value)
            {
                Selected.RemoveIgnoreTags(relevantIgnoreList);
            }

            var requiredSimilarity = RequiredSimilarityIndex;
            int indexes = assetSplitIndentifierCache.Count();
            int counter = 0;
            var cacheChunks = assetSplitIndentifierCache.GroupBy(x => Mathf.FloorToInt(counter++ / SB_Preferences.instance.ThreadChunkSize.Value)); //Group dict in subdicts of n size as we want less threads running

            similarityWorker.ReportProgress(0, "Calculating similarities");

            var validAssetsFound = new List<AssetSimilarityInfo>();
            var strippedTargetPath = getStrippedPath(Path.GetDirectoryName(Selected.path), relevantIgnoreList);

            var parallelResults = Parallel.ForEach(cacheChunks/*, new ParallelOptions { MaxDegreeOfParallelism = worker }*/, //Only run on half the avaliable threads, as it bogs down Unity
           chunk =>
           {
               if (!similarityWorker.CancellationPending)
               {
                   var keys = chunk.Select(x => x.Key).ToArray();
                   var values = chunk.Select(x => x.Value).ToArray();
                   for (int i = 0; i < keys.Count(); i++)
                   {
                       similarityThread(Selected, strippedTargetPath, values[i], relevantIgnoreList, keys[i], requiredSimilarity, ref validAssetsFound);
                   }
                   var index = assetSplitIndentifierCache.Keys.ToList().IndexOf(keys[0]);
                   var completion = ((float)(assetSplitIndentifierCache.Keys.ToList().IndexOf(keys[0]) / (float)assetSplitIndentifierCache.Keys.Count()));

                   similarityWorker.ReportProgress(Mathf.FloorToInt(completion*100), "Calculating similarities");
               }
           });

            var done = false;
            while (!similarityWorker.CancellationPending && !done)
            {
                done = parallelResults.IsCompleted;
            }

            if (!done)
            {
                args.Cancel = true;
                return;
            }
            else
            {
                similarityWorker.ReportProgress(100, "Finalizing");

                try
                {
                    args.Result = validAssetsFound.OrderByDescending(x => x.similarity)
                        .Take(SB_Preferences.instance.MaxSimilarItems.Value)
                        .Select(x => new SB_AssetInfo(x.path, x.similarity, assetSplitIndentifierCache[x.path]))
                    .ToList();
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
            }
        }

        internal bool RemoveObsoleteFavoritesEntries()
        {
            return favorites.TryRemoveObsoleteGUIDs();
        }

        private void worker_complete(object sender, RunWorkerCompletedEventArgs args)
        {
            if (!args.Cancelled)
            {
                SimilarAssets = (List<SB_AssetInfo>)(args.Result);

                AssetPreview.SetPreviewTextureCacheSize(SimilarAssets.Count + 50); //50 default from unity
                foreach (var aInfo in SimilarAssets)
                {
                    aInfo.Init();
                }

                //Alwasy have self as first entry in list
                SB_AssetInfo assetInfoSelf;
                if ((assetInfoSelf = SimilarAssets.SingleOrDefault(x => x.prefab == Selected.prefab)) != null)
                {
                    SimilarAssets.RemoveAt(SimilarAssets.IndexOf(assetInfoSelf));
                    SimilarAssets.Insert(0, assetInfoSelf);
                }

                WorkerStatus = "Idle";
                WorkerCompletion = 0f;
                IsDirty = false;

                similarAssetsFoundCallback();
            }
            else
            {
                similarityWorker.RunWorkerAsync();
            }
        }
        private void similarityThread(SB_AssetInfo selected, string strippedTargetPath, string[] otherTags, string[] relevantIgnores, string otherPath, float requiredSimilarity, ref List<AssetSimilarityInfo> validAssetsFound)
        {
            lock (validAssetsFound) //Threadsafe
            {
                float directoryMetric = 0;
                if (SB_Preferences.instance.SimilarByDirectory.Value)
                {
                    directoryMetric = SB_Utils.GetStringSimilarity(strippedTargetPath, getStrippedPath(Path.GetDirectoryName(otherPath), relevantIgnores)); //heuristic value for filepath                   
                    directoryMetric = Math.Min(1f, directoryMetric);//Make sure we dont have any infinity because of divide by zero
                }

                float tagCount = 0;
                if (SB_Preferences.instance.SimilarByTags.Value)
                {
                    otherTags = SB_Utils.RemoveIgnored(otherTags, relevantIgnores);
                    tagCount = (float)selected.tags.Count(x => otherTags.Contains(x)); //identical name part count
                }

                float unityAssetLabelCount = 0;

                if (SB_Preferences.instance.SimilarByLabels.Value)
                {
                    var otherAssets = otherAssetsUnityLabels != null ? otherAssetsUnityLabels[otherPath] : null;
                    unityAssetLabelCount = (float)selected.labels.Count(x => otherAssets.Contains(x));//See if we have any overlapping unityasset labels
                }

                float filenameMetric = 0;
                if (SB_Preferences.instance.SimilarByFileName.Value)
                {
                    var combinedString1 = string.Join("", selected.tags);
                    var combinedString2 = string.Join("", otherTags);

                    //TODO instead of doing heuristics for the combined filename, do it for each tag in array, and make a combined value?
                    filenameMetric = SB_Utils.GetStringSimilarity(combinedString1, combinedString2);//heuristic value for file name

                    //Make sure we dont have any infinity because of divide by zero
                    filenameMetric = Math.Min(1f, filenameMetric);
                }

                float combined =
                    unityAssetLabelCount * SB_Preferences.instance.MetricLabelWeight.Value
                    + tagCount * SB_Preferences.instance.MetricTagWeight.Value
                    + directoryMetric * SB_Preferences.instance.MetricDirectoryWeight.Value
                    + filenameMetric * SB_Preferences.instance.MetricFilenameWeight.Value;

                if (combined >= requiredSimilarity)
                    validAssetsFound.Add(new AssetSimilarityInfo(otherPath, combined));
            }
        }
        internal void ClearObsolete()
        {
            //Delete if any prefab has been deleted
            SimilarAssets.RemoveAll(a => a.prefab == null);
        }
        internal bool UpdateSelection(Action callback, UnityEngine.Object newSelection, bool forceReload)
        {
            if (!newSelection)
                return false;

            UnityEngine.Object tmpSelected;
            if (PrefabUtility.IsPartOfPrefabAsset(newSelection))
                tmpSelected = newSelection;
            else if (!Selection.activeTransform || !PrefabUtility.IsPartOfPrefabInstance(Selection.objects[0]))
                return false;
            else
                tmpSelected = PrefabUtility.GetCorrespondingObjectFromOriginalSource(Selection.objects[0]);

            if (tmpSelected == Selected?.prefab && !forceReload) //only update if we have selected a new object
                return false;

            selectAsset(tmpSelected);

            IsDirty = true;
            SelectedPrefabTags = SB_Utils.GetFilenameSplit(Selected.prefab.name).ToList();

            getSimilarAssets(callback);
            return true;
        }

        private void selectAsset(UnityEngine.Object tmpSelected)
        {
            Selected = new SB_AssetInfo(tmpSelected);
        }

        //Get similar asset (Do expensive stuff in thread)
        private void getSimilarAssets(Action callback)
        {
            similarAssetsFoundCallback = callback;
            otherAssetsUnityLabels = null;

            if (SB_Preferences.instance.SimilarByLabels.Value)
            {
#if UNITY_2021_1_OR_NEWER
                otherAssetsUnityLabels = assetSplitIndentifierCache.ToDictionary(x => x.Key, x => AssetDatabase.GetLabels(AssetDatabase.GUIDFromAssetPath(x.Key)));
#else
                otherAssetsUnityLabels = assetSplitIndentifierCache.ToDictionary(x => x.Key, x => AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(x.Key)));
#endif
            }

            //Cancel current background worker if already running (Restarting takess place inside 'worker_complete'
            if (similarityWorker != null && similarityWorker.IsBusy)
            {
                similarityWorker.CancelAsync();
            }
            else
                similarityWorker.RunWorkerAsync(); // starts the background worker
        }
        private string getStrippedPath(string targetAssetPath, string[] relevantIgnoreList)
        {
            //We dont want to change this based on ignored tags, so just return original value
            if (!SB_Preferences.instance.SimilarByTags.Value)
                return targetAssetPath;

            //Remove all the tags from the path, to improve heuristics
            System.Text.StringBuilder sb = new System.Text.StringBuilder(targetAssetPath.ToLower());

            for (int i = 0; i < relevantIgnoreList.Length; i++)
            {
                sb.Replace(relevantIgnoreList[i].ToLower(), "");
            }

            return sb.ToString().ToLower();
        }
        internal void IgnoreTag(string tag, bool ignore)
        {
            if (ignore)
                SB_Preferences.instance.IgnoredTags.Add(tag);
            else
                SB_Preferences.instance.IgnoredTags.Remove(tag);
        }
        internal bool HasFavoriteSelected()
        {
            return (Selected != null && favorites?.Entries != null && favorites.Entries.Contains(Selected.id));
        }
        internal void MakeSelectionFavorite(bool bFavorite)
        {
            MakeSelectionFavorite(Selected, bFavorite);
        }
        internal void MakeSelectionFavorite(int index, bool bFavorite)
        {
            SB_AssetInfo aInfo = SimilarAssets[index];
            MakeSelectionFavorite(aInfo, bFavorite);
        }

        internal void MakeSelectionFavorite(SB_AssetInfo aInfo, bool bFavorite)
        {
            favorites.TryRemoveObsoleteGUIDs();
            favorites.MakeFavorite(aInfo.id, bFavorite);
        }

        internal bool IsFavorite(int index)
        {
            SB_AssetInfo aInfo = SimilarAssets[index];
            return favorites.Entries.Contains(aInfo.id);
        }

    }
    [System.Serializable]
    public class SB_AssetInfo
    {
        [SerializeField] private string _id;
        [SerializeField] private string _path;
        [SerializeField] private string _fileName;
        [SerializeField] private float _similarity;
        [SerializeField] private string[] _tags;
        [SerializeField] private string[] _labels;
        [SerializeField] private UnityEngine.Object _prefab;

        public string id { get => _id; private set => _id = value; }
        public string path { get => _path; private set => _path = value; }
        public string fileName { get => _fileName; private set => _fileName = value; }
        public float similarity { get => _similarity; private set => _similarity = value; }
        public string[] tags { get => _tags; private set => _tags = value; }
        public string[] labels { get => _labels; private set => _labels = value; }
        public UnityEngine.Object prefab { get => _prefab; private set => _prefab = value; }

        public IEnumerable<string> TagsExcludedIgnored => tags.Where(x => !SB_Preferences.instance.IgnoredTags.Entries.Contains(x));

        public SB_AssetInfo(string path, float similarity, string[] tags)
        {
            this.path = path;
            this.similarity = similarity;
            this.tags = tags;
        }

        public SB_AssetInfo(UnityEngine.Object selected)
        {
            this.prefab = selected; //Do we need this?
            this.tags = SB_Utils.GetFilenameSplit(this.prefab.name);
            this.path = AssetDatabase.GetAssetPath(prefab);
            this.id = AssetDatabase.AssetPathToGUID(path);
            this.fileName = Path.GetFileNameWithoutExtension(path);
            this.labels = AssetDatabase.GetLabels(prefab);
        }

        internal void Init()
        {
            id = AssetDatabase.AssetPathToGUID(path);
            fileName = Path.GetFileNameWithoutExtension(path);
            prefab = AssetDatabase.LoadMainAssetAtPath(path);
            this.labels = AssetDatabase.GetLabels(prefab);
        }

        internal void RemoveIgnoreTags(string[] relevantIgnoreList)
        {
            tags = SB_Utils.RemoveIgnored(tags, relevantIgnoreList);
            tags = tags.Where(x => !SB_Preferences.instance.IgnoredTags.Entries.Contains(x)).ToArray();
        }
    }

    public struct AssetSimilarityInfo
    {
        public string path;
        public float similarity;

        public AssetSimilarityInfo(string path, float similarity)
        {
            this.path = path;
            this.similarity = similarity;
        }
    }
}