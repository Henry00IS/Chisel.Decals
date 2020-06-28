#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AeternumGames.Chisel.Decals
{
    [InitializeOnLoad]
    public static class DecalManager
    {
        private static bool isRegistered = false;

        static DecalManager()
        {
            if (!isRegistered)
            {
                isRegistered = true;
                Selection.selectionChanged += OnSelectionChanged;
            }
        }

        private static void OnSelectionChanged()
        {
            // if the user no longer has a decal selected we reset
            // the dictionary with all of the meshes and octrees.
            GameObject go = Selection.activeGameObject;
            if (go && !go.GetComponent<ChiselDecal>())
                ChiselDecal.meshTriangleOctrees.Clear();
        }
    }
}

#endif