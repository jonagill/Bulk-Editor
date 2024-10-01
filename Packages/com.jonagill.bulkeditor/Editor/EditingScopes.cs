using System;
using UnityEditor;
using UnityEngine;

namespace BulkEditor
{
    /// <summary>
    /// Scope that prevents the AssetDatabse from refreshing while it is active
    /// </summary>
    public class AssetEditingScope : IDisposable
    {
        public AssetEditingScope()
        {
            AssetDatabase.StartAssetEditing();
        }

        public void Dispose()
        {
            AssetDatabase.StopAssetEditing();
        }
    }

    /// <summary>
    /// Replacement for PrefabUtility.EditPrefabContentsScope() that only saves the prefab if it got marked as dirty.
    /// </summary>
    public struct EditPrefabContentsScope : IDisposable
    {
        public readonly string assetPath;
        public readonly GameObject prefabContentsRoot;
        public bool IsSaveQueued { get; private set; }


        public EditPrefabContentsScope(string assetPath)
        {
            this.assetPath = assetPath;
            this.prefabContentsRoot = PrefabUtility.LoadPrefabContents(assetPath);
            this.IsSaveQueued = false;
        }

        public void QueueSave()
        {
            IsSaveQueued = true;
        }

        public void Dispose()
        {
            if (IsSaveQueued)
            {
                PrefabUtility.SaveAsPrefabAsset(this.prefabContentsRoot, this.assetPath);
            }

            PrefabUtility.UnloadPrefabContents(this.prefabContentsRoot);
        }
    }

}