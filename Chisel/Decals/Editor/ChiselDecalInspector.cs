#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace AeternumGames.Chisel.Decals
{
    [CustomEditor(typeof(ChiselDecal))]
    [CanEditMultipleObjects]
    public class ChiselDecalInspector : Editor
    {
        private SerializedProperty uvTiling;
        private SerializedProperty uvOffset;
        private SerializedProperty maxAngle;
        private SerializedProperty zOffset;

        private void OnEnable()
        {
            uvTiling = serializedObject.FindProperty("uvTiling");
            uvOffset = serializedObject.FindProperty("uvOffset");
            maxAngle = serializedObject.FindProperty("maxAngle");
            zOffset = serializedObject.FindProperty("zOffset");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var prevWideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            EditorGUILayout.PropertyField(uvTiling);
            EditorGUILayout.PropertyField(uvOffset);
            EditorGUIUtility.wideMode = prevWideMode;

            EditorGUILayout.PropertyField(maxAngle);

            // horizontal and vertical flip tools:
            EditorGUILayout.Separator();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Flip X"))
                uvTiling.vector2Value *= new Vector2(-1.0f, 1.0f);

            if (GUILayout.Button("Flip Y"))
                uvTiling.vector2Value *= new Vector2(1.0f, -1.0f);

            if (GUILayout.Button("↺ -90"))
                RotateDecals(-90.0f);

            if (GUILayout.Button("↻ +90"))
                RotateDecals(90.0f);

            EditorGUILayout.EndHorizontal();

            // world axis alignment tools:
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("World Align"))
                WorldAlignDecals();

            if (GUILayout.Button("Face Align"))
                FaceAlignDecals();

            if (GUILayout.Button("Smart Place"))
                SmartPlaceByCamera();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            // z-offset tools:
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.PropertyField(zOffset);

            if (GUILayout.Button("-"))
                ZOffsetOrderDecrease();

            if (GUILayout.Button("+"))
                ZOffsetOrderIncrease();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Order By Sibling Index"))
                ZOffsetOrderBySiblings();

            if (GUILayout.Button("Smart Order"))
                SmartOrder(serializedObject.targetObjects.Cast<ChiselDecal>().ToArray());

            EditorGUILayout.EndHorizontal();

            // force rebuild the decal when modified - always on undo or redo.
            if (serializedObject.ApplyModifiedProperties() || (Event.current.commandName == "UndoRedoPerformed"))
            {
                RebuildTargetDecals();
            }

            // force rebuild the decal manually.
            if (GUILayout.Button("Force Rebuild"))
            {
                ChiselDecal.meshTriangleOctrees.Clear();
                RebuildTargetDecals();
            }

            // global tools.

            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("⚠ Global Decal Tools", EditorStyles.boldLabel);
            EditorGUILayout.Separator();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Rebuild All"))
            {
                ChiselDecal.meshTriangleOctrees.Clear();
                RebuildGlobalDecals();
            }

            if (GUILayout.Button("Smart Order"))
                SmartOrder(FindObjectsOfType<ChiselDecal>());

            EditorGUILayout.EndHorizontal();
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
                if (Physics.Raycast(new Ray(decal.transform.position - f, f), out RaycastHit hit, f.magnitude * 2.0f, -1, QueryTriggerInteraction.Ignore))
                    decal.transform.rotation = Quaternion.LookRotation(-hit.normal);
            }
        }

        private void SmartPlaceByCamera()
        {
            foreach (var target in serializedObject.targetObjects)
            {
                var decal = (ChiselDecal)target;
                Undo.RecordObject(decal.transform, "Move Decal To Camera");

                // rotate to match the camera:
                Camera camera = SceneView.lastActiveSceneView?.camera;
                if (camera != null)
                {
                    // attempt to find collision in front of the camera.
                    if (Physics.Raycast(new Ray(camera.transform.position, camera.transform.forward), out RaycastHit hit, 50.0f, -1, QueryTriggerInteraction.Ignore))
                    {
                        decal.transform.position = hit.point;
                        decal.transform.rotation = Quaternion.LookRotation(-hit.normal);
                    }
                    else
                    {
                        decal.transform.position = camera.transform.position + (camera.transform.forward.normalized * 3.0f);
                        decal.transform.rotation = Quaternion.LookRotation(NearestWorldAxis(camera.transform.forward));
                    }
                }
            }
        }

        private void RotateDecals(float degrees)
        {
            foreach (var target in serializedObject.targetObjects)
            {
                var decal = (ChiselDecal)target;
                Undo.RecordObject(decal.transform, "Rotate Decals");

                decal.transform.Rotate(Vector3.forward, -degrees);
            }
        }

        private void ZOffsetOrderIncrease()
        {
            foreach (var target in serializedObject.targetObjects)
            {
                var decal = (ChiselDecal)target;
                var so = new SerializedObject(target);
                var zo = so.FindProperty("zOffset");
                Undo.RecordObject(decal, "Increase Z-Offset of Decals");

                zo.intValue += 1;

                // force rebuild the decal when modified.
                if (so.ApplyModifiedPropertiesWithoutUndo())
                {
                    RebuildTargetDecals();
                }
            }
        }

        private void ZOffsetOrderDecrease()
        {
            foreach (var target in serializedObject.targetObjects)
            {
                var decal = (ChiselDecal)target;
                var so = new SerializedObject(target);
                var zo = so.FindProperty("zOffset");
                Undo.RecordObject(decal, "Decrease Z-Offset of Decals");

                zo.intValue = Mathf.Max(0, zo.intValue - 1);

                // force rebuild the decal when modified.
                if (so.ApplyModifiedPropertiesWithoutUndo())
                {
                    RebuildTargetDecals();
                }
            }
        }

        private void ZOffsetOrderBySiblings()
        {
            foreach (var target in serializedObject.targetObjects)
            {
                var decal = (ChiselDecal)target;
                var so = new SerializedObject(target);
                var zo = so.FindProperty("zOffset");
                Undo.RecordObject(decal, "Order Decals By Siblings");

                zo.intValue = decal.transform.GetSiblingIndex();

                // force rebuild the decal when modified.
                if (so.ApplyModifiedPropertiesWithoutUndo())
                {
                    RebuildTargetDecals();
                }
            }
        }

        private void RebuildGlobalDecals()
        {
            foreach (var decal in FindObjectsOfType<ChiselDecal>())
            {
                decal.Rebuild();
            }
        }

        // this algorithm can be improved, it's not working right - feel free to contribute a fix! ~ Henry
        // the idea is that we combine decals to small groups touching each-other (by bounds),
        // then order them by the order they appear in the hierarchy.
        // it should keep numbers small 0 to X so they are snug against the wall.
        private void SmartOrder(ChiselDecal[] decals)
        {
            // build an octree to contain the rendering bounds of every decal.
            BoundsOctree<ChiselDecal> octree = new BoundsOctree<ChiselDecal>(8.0f, Vector3.zero, 1.0f, 1.0f);

            // find the rendering bounds of every decal and add them to the octree.
            for (int i = 0; i < decals.Length; i++)
            {
                ChiselDecal decal = decals[i];
                octree.Add(decal, decal.GetComponent<MeshRenderer>().bounds);
            }

            // iterate over every decal:
            for (int i = 0; i < decals.Length; i++)
            {
                ChiselDecal decal = decals[i];

                // find all of the decals that are intersecting this decal using the octree.
                List<ChiselDecal> others = new List<ChiselDecal>();
                octree.GetColliding(others, decal.GetComponent<MeshRenderer>().bounds);

                // order all of them (including us) by their sibling index.
                ChiselDecal[] ordered = others.OrderBy(o => o.transform.GetSiblingIndex()).ToArray();

                // find our position in the ordered set.
                int zoffset = 0;
                for (int j = 0; j < ordered.Length; j++)
                    if (ordered[j] == decal)
                        zoffset = j;

                // use this as our z-offset.
                var so = new SerializedObject(decal);
                var zo = so.FindProperty("zOffset");
                Undo.RecordObject(decal, "Global Smart Order Decals");

                zo.intValue = zoffset;

                // force rebuild the decal when modified.
                if (so.ApplyModifiedPropertiesWithoutUndo())
                {
                    decal.Rebuild();
                }
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