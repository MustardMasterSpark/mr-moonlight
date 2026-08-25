// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Search;
using Object = UnityEngine.Object;

namespace MA.Flora.Editor
{
    internal static class FloraAssetPickers
    {
        private static List<string> s_ExcludedGlobalIds = new();

        private static void BuildExcludedIds<T>(List<T> excludeItems) where T : Object
        {
            foreach (var excludeItem in excludeItems)
            {
                var globalId = GlobalObjectId.GetGlobalObjectIdSlow(excludeItem);
                if (!globalId.Equals(default))
                    s_ExcludedGlobalIds.Add(globalId.ToString());
            }
        }

        private static bool FilterObjectsByExcludedIds(SearchItem item)
        {
            return string.IsNullOrEmpty(item.id) ||
                   s_ExcludedGlobalIds.Count == 0 ||
                   !s_ExcludedGlobalIds.Contains(item.id, StringComparer.InvariantCultureIgnoreCase);
        }

        private const SearchFlags DefaultSearchFlags =
            SearchFlags.Sorted |
            SearchFlags.Synchronous |
            SearchFlags.Packages |
            SearchFlags.OpenPicker;

        private const SearchViewFlags DefaultSearchViewFlags =
            SearchViewFlags.ObjectPicker |
            SearchViewFlags.Packages |
            SearchViewFlags.DisableBuilderModeToggle |
            SearchViewFlags.DisableQueryHelpers;

        public static void ShowModelPicker(List<GameObject> excludeItems, bool multiselect, Action<GameObject[]> selectHandler)
        {
            BuildExcludedIds(excludeItems);

            var searchContext = SearchService.CreateContext("asset", "t:prefab", DefaultSearchFlags);

            void SelectionObjectHandler(SearchItem item, bool cancelled)
            {
                if (item.context.selection == null)
                    return;

                if (!cancelled && item.ToObject())
                {
                    var selectedItems = item.context.selection.Select(selectedItem => selectedItem.ToObject()).Cast<GameObject>().ToArray();
                    if (selectedItems.Length > 0)
                    {
                        selectHandler(selectedItems);
                    }
                }
            }

            var searchViewState = SearchViewState.CreatePickerState(
                title: L10n.Tr("Models"),
                context: searchContext,
                selectHandler: SelectionObjectHandler,
                filterHandler: FilterObjectsByExcludedIds,
                flags: DefaultSearchViewFlags);
            searchViewState.excludeClearItem = true;

            var searchView = SearchService.ShowPicker(searchViewState);
            searchView.multiselect = multiselect;
        }
    }
}
