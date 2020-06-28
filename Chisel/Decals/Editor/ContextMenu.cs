using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR

namespace AeternumGames.Chisel.Decals
{
    public class ContextMenu
    {
        [MenuItem("GameObject/Chisel/Create/Decal")]
        private static void CreateDecal()
        {
            var go = new GameObject("Decal");
            go.AddComponent<ChiselDecal>();
            Undo.RegisterCreatedObjectUndo(go, "Undo Create Decal");

            // child underneath current selection:
            GameObject selection = Selection.activeGameObject;
            if (selection != null)
            {
                // unless the current selection is a decal - then we put ourselves underneath it.
                if (selection.GetComponent<ChiselDecal>())
                {
                    if (selection.transform.parent)
                        go.transform.parent = selection.transform.parent;
                }
                else
                {
                    go.transform.parent = selection.transform;
                }
            }

            // rotate to match the camera:
            Camera camera = SceneView.lastActiveSceneView?.camera;
            if (camera != null)
            {
                // attempt to find collision in front of the camera.
                if (Physics.Raycast(new Ray(camera.transform.position, camera.transform.forward), out RaycastHit hit, 50.0f))
                {
                    go.transform.position = hit.point;
                    go.transform.rotation = Quaternion.LookRotation(-hit.normal);
                }
                else
                {
                    go.transform.position = camera.transform.position + (camera.transform.forward.normalized * 3.0f);
                    go.transform.rotation = Quaternion.LookRotation(NearestWorldAxis(camera.transform.forward));
                }
            }

            // select the game object in the hierarchy.
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
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