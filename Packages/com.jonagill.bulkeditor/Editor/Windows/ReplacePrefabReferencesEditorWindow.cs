#if UNITY_2022_2_OR_NEWER

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace BulkEditor
{
    /// <summary>
    /// An EditorWindow that attempts to replace all usages and references of one prefab to another across
    /// scenes, prefabs, and ScriptableObjects across the project.
    /// </summary>
    [Serializable]
    public class ReplacePrefabReferencesEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Bulk Editing/Prefabs/Windows/Replace Prefab References Window")]
        public static void ShowWindow()
        {
            ReplacePrefabReferencesEditorWindow wnd = GetWindow<ReplacePrefabReferencesEditorWindow>();
            wnd.titleContent = new GUIContent("Replace Prefab References");
        }

        [SerializeField] private GameObject _fromPrefab;
        [SerializeField] private GameObject _toPrefab;
        [SerializeField] private bool _replacePrefabInstances = true;
        [SerializeField] private bool _replacePrefabReferences = true;

        public void CreateGUI()
        {
            var prefabsBox = EditorGUIHelpers.CreateBoxElement("Prefabs");

            var fromPrefabField = new ObjectField("From Prefab");
            fromPrefabField.value = _fromPrefab;
            fromPrefabField.objectType = typeof(GameObject);
            fromPrefabField.allowSceneObjects = false;

            var toPrefabField = new ObjectField("To Prefab");
            toPrefabField.value = _toPrefab;
            toPrefabField.objectType = typeof(GameObject);
            toPrefabField.allowSceneObjects = false;

            prefabsBox.Add(fromPrefabField);
            prefabsBox.Add(toPrefabField);
            rootVisualElement.Add(prefabsBox);

            var settingsBox = EditorGUIHelpers.CreateBoxElement("Settings", true);

            var replaceInstancesToggle = EditorGUIHelpers.CreateBoolToggle(
                "Replace Instances",
                "If true, will try to replace all instances of the target prefab across scenes and prefabs.",
                () => _replacePrefabInstances,
                v => _replacePrefabInstances = v);

            var replaceReferencesToggle = EditorGUIHelpers.CreateBoolToggle(
                "Replace References",
                "If true, will try to replace all references to the target prefab across all serialized assets in the project.",
                () => _replacePrefabReferences,
                v => _replacePrefabReferences = v);

            settingsBox.Add(replaceInstancesToggle);
            settingsBox.Add(replaceReferencesToggle);
            rootVisualElement.Add(settingsBox);

            var convertButton = new Button(ReplacePrefabs);
            convertButton.text = "Replace Prefab References";
            rootVisualElement.Add(convertButton);

            // Add callbacks
            void RefreshEnabledElements()
            {
                convertButton.SetEnabled(_fromPrefab != null && _toPrefab != null);
            }

            fromPrefabField.RegisterValueChangedCallback(evt =>
            {
                _fromPrefab = (GameObject)evt.newValue;
                RefreshEnabledElements();
            });

            toPrefabField.RegisterValueChangedCallback(evt =>
            {
                _toPrefab = (GameObject)evt.newValue;
                RefreshEnabledElements();
            });

            RefreshEnabledElements();
        }

        private void ReplacePrefabs()
        {
            if (_fromPrefab == null || _toPrefab == null)
            {
                return;
            }

            if (_replacePrefabInstances)
            {
                ReplacePrefabInstances(_fromPrefab, _toPrefab);
            }

            if (_replacePrefabReferences)
            {
                ReplacePrefabReferences(_fromPrefab, _toPrefab);
            }
        }

        private static void ReplacePrefabInstances(GameObject fromPrefab, GameObject toPrefab)
        {
            var fromPrefabPath = AssetDatabase.GetAssetPath(fromPrefab);
            BulkEditing.RunFunctionOnAllPrefabs("Replacing instances in prefabs...", (path, go) =>
            {
                var dependencies = AssetDatabase.GetDependencies(path);
                if (!dependencies.Contains(fromPrefabPath))
                {
                    // We don't reference the given prefab, so don't bother loading this asset for editing
                    return false;
                }

                var isDirty = false;
                using (var editingScope = new EditPrefabContentsScope(path))
                {
                    var contentsRoot = editingScope.prefabContentsRoot;
                    var prefabInstances = PrefabUtility.FindAllInstancesOfPrefab(fromPrefab, contentsRoot.scene);

                    foreach (var instance in prefabInstances)
                    {
                        isDirty |= TryReplacePrefabInstance(instance, fromPrefab, toPrefab);
                    }

                    if (isDirty)
                    {
                        editingScope.QueueSave();
                    }
                }

                return isDirty;
            });

            BulkEditing.RunFunctionInAllScenes("Replacing instances in scenes...",
                function: () =>
                {
                    var scene = SceneManager.GetSceneAt(0);

                    var isDirty = false;
                    var prefabInstances = PrefabUtility.FindAllInstancesOfPrefab(fromPrefab, scene);

                    foreach (var instance in prefabInstances)
                    {
                        isDirty |= TryReplacePrefabInstance(instance, fromPrefab, toPrefab);
                    }

                    return isDirty;
                },
                sceneFilter: path =>
                {
                    var dependencies = AssetDatabase.GetDependencies(path);
                    return dependencies.Contains(fromPrefabPath);
                },
                onlyScenesInBuild: false);
        }

        private static bool TryReplacePrefabInstance(GameObject targetObject, GameObject fromPrefab, GameObject toPrefab)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(targetObject) &&
                 PrefabUtility.GetCorrespondingObjectFromSource(targetObject) == fromPrefab)
            {
                Debug.Log($"Replacing prefab instance {targetObject} with prefab ${toPrefab}.\n" +
                              $"{targetObject.GetPathName(true)}");

                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(
                    targetObject,
                    toPrefab,
                    new PrefabReplacingSettings()
                    {
                        objectMatchMode = ObjectMatchMode.ByHierarchy,
                        prefabOverridesOptions = PrefabOverridesOptions.KeepAllPossibleOverrides,
                        changeRootNameToAssetName = false,
                        logInfo = true
                    },
                    InteractionMode.AutomatedAction);

                return true;
            }

            return false;
        }

        private static void ReplacePrefabReferences(GameObject fromPrefab, GameObject toPrefab)
        {
            ReplacePrefabReferencesInPrefabs(fromPrefab, toPrefab);
            ReplacePrefabReferencesInAssets("ScriptableObject", fromPrefab, toPrefab);
            ReplacePrefabReferencesInScenes(fromPrefab, toPrefab);
        }

        private static void ReplacePrefabReferencesInScenes(GameObject fromPrefab, GameObject toPrefab)
        {
            var fromPrefabPath = AssetDatabase.GetAssetPath(fromPrefab);
            var rootObjects = new List<GameObject>();
            var scratchComponentList = new List<Component>();

            BulkEditing.RunFunctionInAllScenes("Replacing references in scenes...",
                function: () =>
                {
                    var isDirty = false;
                    BulkEditing.GetRootObjectsOfLoadedScenes(ref rootObjects);
                    foreach (var rootObject in rootObjects)
                    {
                        rootObject.GetComponentsInChildren(true, scratchComponentList);
                        foreach (var component in scratchComponentList)
                        {
                            isDirty |= TryReplacePrefabReferencesInObject(component, fromPrefab, toPrefab);
                        }
                    }

                    return isDirty;
                },
                sceneFilter: path =>
                {
                    var dependencies = AssetDatabase.GetDependencies(path);
                    return dependencies.Contains(fromPrefabPath);
                },
                onlyScenesInBuild: false);
        }

        private static void ReplacePrefabReferencesInPrefabs(GameObject fromPrefab, GameObject toPrefab)
        {
            var toPrefabPath = AssetDatabase.GetAssetPath(toPrefab);
            var fromPrefabPath = AssetDatabase.GetAssetPath(fromPrefab);
            var scratchComponentList = new List<Component>();

            BulkEditing.RunFunctionOnAllPrefabs(
                "Replacing references in prefabs...",
                function: (path, gameObject) =>
                {
                    if (path == fromPrefabPath || path == toPrefabPath)
                    {
                        return false;
                    }

                    var isDirty = false;
                    using (var editingScope = new EditPrefabContentsScope(path))
                    {
                        editingScope.prefabContentsRoot.GetComponentsInChildren(true, scratchComponentList);
                        foreach (var component in scratchComponentList)
                        {
                            isDirty |= TryReplacePrefabReferencesInObject(component, fromPrefab, toPrefab);
                        }

                        if (isDirty)
                        {
                            editingScope.QueueSave();
                        }
                    }

                    return isDirty;
                });
        }

        private static void ReplacePrefabReferencesInAssets(
            string databaseFilter,
            GameObject fromPrefab,
            GameObject toPrefab,
            Func<Object, IEnumerable<Object>> getSubObjects = null)
        {
            using (new AssetEditingScope())
            {
                var fromPrefabPath = AssetDatabase.GetAssetPath(fromPrefab);
                var toPrefabPath = AssetDatabase.GetAssetPath(toPrefab);
                var assetGuids = AssetDatabase.FindAssets($"t:{databaseFilter}");
                for (var i = 0; i < assetGuids.Length; i++)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
                    if (assetPath == fromPrefabPath || assetPath == toPrefabPath)
                    {
                        continue;
                    }

                    if (i % 50 == 0)
                    {
                        if (EditorUtility.DisplayCancelableProgressBar(
                                $"Replacing references in {databaseFilter}s...",
                                assetPath,
                                i / (float)assetGuids.Length))
                        {
                            break;
                        }
                    }

                    try
                    {
                        // Check if this prefab is a dependency of the asset before we bother loading and traversing it
                        if (AssetDatabase.GetDependencies(assetPath).Contains(fromPrefabPath))
                        {
                            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

                            void ReplaceReferencesInObject(UnityEngine.Object obj)
                            {
                                if (TryReplacePrefabReferencesInObject(obj, fromPrefab, toPrefab))
                                {
                                    EditorUtility.SetDirty(asset);
                                }
                            }

                            ReplaceReferencesInObject(asset);

                            if (getSubObjects != null)
                            {
                                foreach (var subObject in getSubObjects(asset))
                                {
                                    ReplaceReferencesInObject(subObject);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
        }

        private static bool TryReplacePrefabReferencesInObject(Object asset, GameObject fromPrefab, GameObject toPrefab)
        {
            var isDirty = false;
            using (var serializedObject = new SerializedObject(asset))
            {
                SerializedProperty iterator = serializedObject.GetIterator();

                do
                {
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        isDirty |= TryReplacePrefabReference(iterator, fromPrefab, toPrefab);
                    }
                }
                while (iterator.NextVisible(true));

                if (isDirty)
                {
                    serializedObject.ApplyModifiedProperties();
                }
            }

            return isDirty;
        }

        private static bool TryReplacePrefabReference(
            SerializedProperty objectReferenceProperty,
            GameObject fromPrefab,
            GameObject toPrefab)
        {
            var objectReferenceValue = objectReferenceProperty.objectReferenceValue;
            if (objectReferenceValue == null)
            {
                return false;
            }

            if (objectReferenceValue is GameObject gameObjectReference &&
                 BulkEditing.IsGameObjectPartOfHierarchy(gameObjectReference, fromPrefab))
            {
                if (BulkEditing.TryGetMatchingGameObject(
                        gameObjectReference,
                        fromPrefab,
                        toPrefab, out var matchingObject))
                {
                    objectReferenceProperty.objectReferenceValue = matchingObject;
                    return true;
                }
                else
                {
                    Debug.LogError($"Unable to find object that matches {gameObjectReference.GetPathName()} on prefab {toPrefab} " +
                                    $"while replacing references on {objectReferenceProperty.serializedObject.targetObject}",
                        objectReferenceProperty.serializedObject.targetObject);
                }
            }

            if (objectReferenceValue is Component componentReference &&
                 BulkEditing.IsGameObjectPartOfHierarchy(componentReference.gameObject, fromPrefab))
            {
                if (BulkEditing.TryGetMatchingComponent(
                        componentReference,
                        componentReference.GetType(),
                        fromPrefab,
                        toPrefab, out var matchingComponent))
                {
                    objectReferenceProperty.objectReferenceValue = matchingComponent;
                    return true;
                }
                else
                {
                    Debug.LogError($"Unable to find component that matches {componentReference.gameObject.GetPathName()} on prefab {toPrefab} " +
                                    $"while replacing references on {objectReferenceProperty.serializedObject.targetObject}",
                        objectReferenceProperty.serializedObject.targetObject);
                }
            }

            return false;
        }
    }
}

#endif