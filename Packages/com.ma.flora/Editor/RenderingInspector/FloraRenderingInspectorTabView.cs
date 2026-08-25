// Copyright © Magnetic Arcade. All Rights Reserved.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MA.Flora.Editor
{
    internal sealed class FloraRenderingInspectorTabView : BindableElement, INotifyValueChanged<int>
    {
        private const string UssClassName = "flora-rendering-inspector-tab-view";
        private const string TabHeaderClassName = "flora-rendering-inspector-tab-view__tab-header";
        private const string TabContentClassName = "flora-rendering-inspector-tab-view__tab-content";
        private const string TabClassName = "flora-rendering-inspector-tab-view__tab";
        private const string ActiveTabClassName = "flora-rendering-inspector-tab-view__tab--active";

        private readonly VisualElement m_Header;
        private readonly VisualElement m_Content;
        private readonly List<Label> m_Tabs = new();
        private readonly List<VisualElement> m_TabContents = new();
        private int m_Index = -1;

        public FloraRenderingInspectorTabView()
        {
            AddToClassList(UssClassName);

            m_Header = new VisualElement();
            m_Header.AddToClassList(TabHeaderClassName);
            hierarchy.Add(m_Header);

            m_Content = new VisualElement();
            m_Content.AddToClassList(TabContentClassName);
            hierarchy.Add(m_Content);
        }

        public override VisualElement contentContainer => m_Content;

        public int value
        {
            get => m_Index;
            set
            {
                if (m_Index == value)
                    return;

                if (panel != null)
                {
                    using var pooled = ChangeEvent<int>.GetPooled(m_Index, value);
                    pooled.target = this;
                    SetValueWithoutNotify(value);
                    SendEvent(pooled);
                }
                else
                    SetValueWithoutNotify(value);
            }
        }

        public Label AddTab(string tabName, string tooltip, VisualElement content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            var tabIndex = m_Tabs.Count;
            var tab = new Label(string.IsNullOrEmpty(tabName) ? $"Tab {tabIndex + 1}" : tabName)
            {
                focusable = true,
                tooltip = tooltip ?? string.Empty,
            };
            tab.AddToClassList(TabClassName);
            tab.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                value = tabIndex;
                evt.StopPropagation();
            });
            tab.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.Space)
                    return;

                value = tabIndex;
                evt.StopPropagation();
            });

            m_Tabs.Add(tab);
            m_Header.Add(tab);

            m_TabContents.Add(content);
            m_Content.Add(content);

            SetTabContentVisible(tabIndex, false);
            if (m_Index == -1)
                SetValueWithoutNotify(0);

            return tab;
        }

        public VisualElement GetTabElement(int index)
            => index >= 0 && index < m_Tabs.Count ? m_Tabs[index] : null;

        public void SetValueWithoutNotify(int newValue)
        {
            if (newValue < 0 || newValue >= m_Tabs.Count)
            {
                ClearActiveTab();
                return;
            }

            if (m_Index >= 0 && m_Index < m_Tabs.Count)
            {
                m_Tabs[m_Index].RemoveFromClassList(ActiveTabClassName);
                SetTabContentVisible(m_Index, false);
            }

            m_Index = newValue;
            m_Tabs[m_Index].AddToClassList(ActiveTabClassName);
            SetTabContentVisible(m_Index, true);
        }

        private void ClearActiveTab()
        {
            if (m_Index >= 0 && m_Index < m_Tabs.Count)
            {
                m_Tabs[m_Index].RemoveFromClassList(ActiveTabClassName);
                SetTabContentVisible(m_Index, false);
            }

            m_Index = -1;
        }

        private void SetTabContentVisible(int index, bool visible)
        {
            if (index < 0 || index >= m_TabContents.Count)
                return;

            m_TabContents[index].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
