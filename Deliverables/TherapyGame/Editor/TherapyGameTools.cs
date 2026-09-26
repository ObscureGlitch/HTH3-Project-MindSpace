using System;
using System.IO;
using System.Linq;
using TheLastWatch.Environment;
using TheLastWatch.Interaction;
using TheLastWatch.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TherapyGame.Editor
{
    [InitializeOnLoad]
    public static class TherapyGameTools
    {
        private const string ScenePath = "Assets/TherapyGame/Scenes/TherapyRoom.unity";
        private const string RequestPath = "Assets/TherapyGame/OpenOnImport.txt";
        static TherapyGameTools() => EditorApplication.delayCall += FinishImport;

        private static void FinishImport()
        {
            if (!File.Exists(RequestPath) || File.ReadAllText(RequestPath).Trim() != "pending") return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += FinishImport; return;
            }
            File.WriteAllText(RequestPath, "complete");
            OpenRoom();
        }

        [MenuItem("Therapy Game/Open Therapy Room")]
        public static void OpenRoom()
        {
            if (EditorApplication.isPlaying) { Debug.Log("Therapy room imported. Stop Play mode, then use Therapy Game > Open Therapy Room."); return; }
            bool dirty = Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty);
            if (dirty)
            {
                File.WriteAllText("Assets/TherapyGame/ImportReport.txt", "Imported successfully. Existing scene has unsaved edits, so it was left open. Save it when ready and use Therapy Game > Open Therapy Room.\n");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
                EditorGUIUtility.PingObject(Selection.activeObject); return;
            }
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ValidateImportedRoom();
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
            {
                view.in2DMode = false; view.sceneLighting = true;
                view.LookAt(new Vector3(0, 1.20f, .35f), Quaternion.Euler(8, 0, 0), 3.8f, false, true);
            }
            Selection.activeGameObject = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "TherapyRoom");
            Debug.Log("Therapy room imported and open. Press Play to explore. WASD walk, mouse look, E notice, Esc release cursor.");
        }

        [MenuItem("Therapy Game/Validate Imported Room")]
        public static void ValidateImportedRoom()
        {
            GameObject root = GameObject.Find("TherapyRoom");
            if (root == null) throw new InvalidOperationException("Open TherapyRoom first.");
            int missing = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            int badMaterials = root.GetComponentsInChildren<Renderer>(true).Sum(r => r.sharedMaterials.Count(m => m == null || m.shader == null));
            bool input = root.GetComponentInChildren<WellnessExplorer>() != null;
            bool pipeline = root.GetComponent<WellnessScenePipeline>()?.pipeline != null;
            int targets = root.GetComponentsInChildren<WellnessInteraction>().Length;
            string result = $"TherapyRoom import verification\nMissing scripts: {missing}\nMissing materials: {badMaterials}\nExplorer: {input}\nScene pipeline: {pipeline}\nInteraction targets: {targets}\n";
            if (missing > 0 || badMaterials > 0 || !input || !pipeline || targets != 7) throw new InvalidOperationException(result);
            File.WriteAllText("Assets/TherapyGame/ImportReport.txt", result + "PASS. Imported and opened in this project.\n");
        }
    }
}
