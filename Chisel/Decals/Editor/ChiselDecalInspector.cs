using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace AeternumGames.Chisel.Decals
{
    [CustomEditor(typeof(ChiselDecal))]
    [CanEditMultipleObjects]
    public class ChiselDecalInspector : Editor
    {
        private SerializedProperty uvTiling;
        private SerializedProperty uvOffset;

        private void OnEnable()
        {
            uvTiling = serializedObject.FindProperty("uvTiling");
            uvOffset = serializedObject.FindProperty("uvOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(uvTiling);
            EditorGUILayout.PropertyField(uvOffset);

            bool dirty = false;
            dirty = serializedObject.hasModifiedProperties;
            serializedObject.ApplyModifiedProperties();
            if (dirty)
            {
                foreach (var target in serializedObject.targetObjects)
                {
                    var decal = (ChiselDecal)target;
                    decal.Rebuild();
                }
            }
        }
    }
}