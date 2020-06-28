#if UNITY_EDITOR

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
        private SerializedProperty maxAngle;

        private void OnEnable()
        {
            uvTiling = serializedObject.FindProperty("uvTiling");
            uvOffset = serializedObject.FindProperty("uvOffset");
            maxAngle = serializedObject.FindProperty("maxAngle");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(uvTiling);
            EditorGUILayout.PropertyField(uvOffset);
            EditorGUILayout.PropertyField(maxAngle);

            // force rebuild the decal when modified.
            if (serializedObject.ApplyModifiedProperties())
            {
                RebuildTargetDecals();
            }

            // force rebuild the decal manually - always on undo or redo.
            if (GUILayout.Button("Force Rebuild") || (Event.current.commandName == "UndoRedoPerformed"))
            {
                RebuildTargetDecals();
            }
        }

        private void RebuildTargetDecals()
        {
            foreach (var target in serializedObject.targetObjects)
            {
                var decal = (ChiselDecal)target;
                decal.Rebuild();
            }
        }
    }
}

#endif