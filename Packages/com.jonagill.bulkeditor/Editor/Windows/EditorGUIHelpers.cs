using System;
using System.Linq;

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UIElements;

namespace BulkEditor
{
    /// <summary>
    /// Helper methods for constructing editor windows via code
    /// </summary>
    internal class EditorGUIHelpers
    {
        public static VisualElement CreateBoxElement(string label = null, bool boldLabel = false)
        {
            const float BoxMargin = 3f;
            const float BoxPadding = 2f;
            const float HeaderMargin = 2f;

            var container = new VisualElement
            {
                style =
                {
                    marginTop = BoxMargin,
                    marginBottom = 0, // Don't double-up top and bottom margins
                    marginLeft = BoxMargin,
                    marginRight = BoxMargin,
                    paddingTop = BoxPadding,
                    paddingBottom = BoxPadding,
                    paddingLeft = BoxPadding,
                    paddingRight = BoxPadding,
                }
            };

            if (!string.IsNullOrEmpty(label))
            {
                var labelElement = new Label(label)
                {
                    style =
                    {
                        marginBottom = HeaderMargin,
                        unityFontStyleAndWeight = boldLabel ? FontStyle.Bold : FontStyle.Normal
                    }
                };
                container.Add(labelElement);
            }

            container.AddToClassList("unity-box");
            return container;
        }

        public static Foldout CreateSavedFoldout(
            string preferenceKeyPrefix,
            string label,
            string tooltip = null,
            bool defaultOpen = false)
        {
            var foldout = new Foldout()
            {
                text = label,
                tooltip = tooltip
            };

            var prefKey = $"{preferenceKeyPrefix}_{label}";

            bool GetIsOpen()
            {
                return EditorPrefs.GetBool(prefKey, defaultOpen);
            }

            void SetIsOpen(bool value)
            {
                EditorPrefs.SetBool(prefKey, value);
            }

            void RefreshFoldoutState()
            {
                foldout.value = GetIsOpen();
            }

            foldout.RegisterValueChangedCallback(changeEvent =>
            {
                SetIsOpen(changeEvent.newValue);
                RefreshFoldoutState();
            });

            RefreshFoldoutState();
            return foldout;
        }

        public static Toggle CreateBoolToggle(string label, string tooltip, Func<bool> getValue, Action<bool> setValue)
        {
            Assert.IsFalse(string.IsNullOrEmpty(label), "Toggles created without a label will be instantiated in an undefined state.");

            var toggle = new Toggle(label)
            {
                tooltip = tooltip,
                value = getValue()
            };

            toggle.RegisterValueChangedCallback(evt =>
            {
                setValue(evt.newValue);

                // Stop propagation to fix a bug where un-checking a preference toggle
                // contained in a foldout would also close the foldout
                evt.StopPropagation();
            });
            return toggle;
        }

        public static EnumField CreateEnumField(string label, string tooltip, Func<Enum> getValue, Action<Enum> setValue)
        {
            var enumField = new EnumField(label, getValue())
            {
                tooltip = tooltip,
            };
            enumField.RegisterValueChangedCallback(evt => setValue(evt.newValue));
            return enumField;
        }
    }
}
