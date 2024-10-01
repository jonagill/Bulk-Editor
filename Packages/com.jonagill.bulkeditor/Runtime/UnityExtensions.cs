using System.Collections.Generic;
using UnityEngine;

namespace BulkEditor
{
    public static class UnityExtensions
    {
        private static List<string> ScratchStrings  = new List<string> ();

        /// <summary>
        /// Print a GameObject's path in the hierarchy
        /// </summary>
        public static string GetPathName(
            this GameObject gameObject,
            bool includeScene = false,
            bool includeRoot = true,
            bool includeSelf = true,
            Transform customRoot = null)
        {
            lock (ScratchStrings)
            {
                ScratchStrings.Clear();

                Transform transform = gameObject.transform;
                if (!includeSelf)
                {
                    transform = transform.parent;
                }

                var isRoot = false;
                while (!isRoot)
                {
                    isRoot = transform == null || transform == customRoot;
                    if (transform != null)
                    {
                        if (!isRoot || includeRoot)
                        {
                            ScratchStrings.Add(transform.name);
                        }

                        transform = transform.parent;
                    }
                }

                ScratchStrings.Reverse();
                string outputString = string.Join("/", ScratchStrings);

                if (includeScene)
                {
                    outputString += $" ({gameObject.scene.name})";
                }

                return outputString;
            }
        }
    }
}