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

            // horizontal and vertical flip tools:
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Flip Horizontally"))
                uvTiling.vector2Value *= new Vector2(-1.0f, 1.0f);

            if (GUILayout.Button("Flip Vertically"))
                uvTiling.vector2Value *= new Vector2(1.0f, -1.0f);

            EditorGUILayout.EndHorizontal();

            // world axis alignment tools:
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("World Align"))
                WorldAlignDecals();

            if (GUILayout.Button("Face Align"))
                FaceAlignDecals();

            EditorGUILayout.EndHorizontal();

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

        private void WorldAlignDecals()
        {
            foreach (var target in serializedObject.targetObjects)
            {
                var decal = (ChiselDecal)target;
                Undo.RecordObject(decal.transform, "World Align Decal");
                decal.transform.rotation = Quaternion.LookRotation(NearestWorldAxis(decal.transform.forward.normalized));
            }
        }

        private void FaceAlignDecals()
        {
            foreach (var target in serializedObject.targetObjects)
            {
                var decal = (ChiselDecal)target;
                Undo.RecordObject(decal.transform, "Face Align Decal");

                Vector3 f = decal.transform.TransformVector(Vector3.forward * 0.5f);
                if (Physics.Raycast(new Ray(decal.transform.position - f, f), out RaycastHit hit, f.magnitude * 2.0f))
                    decal.transform.rotation = Quaternion.LookRotation(-hit.normal);
            }
        }

        private static Vector3 NearestWorldAxis(Vector3 v)
        {
            if (Mathf.Abs(v.x) < Mathf.Abs(v.y))
            {
                v.x = 0;
                if (Mathf.Abs(v.y) < Mathf.Abs(v.z))
                    v.y = 0;
                else
                    v.z = 0;
            }
            else
            {
                v.y = 0;
                if (Mathf.Abs(v.x) < Mathf.Abs(v.z))
                    v.x = 0;
                else
                    v.z = 0;
            }
            return v;
        }
    }
}

#endif