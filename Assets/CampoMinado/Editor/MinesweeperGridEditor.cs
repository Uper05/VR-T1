using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampoMinadoVR.EditorTools
{
    /// <summary>
    /// Botoes de "Gerar Grid" / "Limpar Grid" no Inspector e criacao
    /// automatica dos materiais (como assets, para sobreviverem ao salvar a cena).
    /// </summary>
    [CustomEditor(typeof(MinesweeperGrid))]
    public class MinesweeperGridEditor : Editor
    {
        const string MaterialFolder = "Assets/CampoMinado/Materials";

        static readonly Color TileColor = new Color(0.035f, 0.035f, 0.040f); // preto
        static readonly Color LineColor = new Color(0.340f, 0.350f, 0.370f); // cinza

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var grid = (MinesweeperGrid)target;

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"{grid.width} x {grid.height} celulas  —  " +
                $"{grid.TotalWidth:0.00} m x {grid.TotalDepth:0.00} m no chao",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Gerar Grid", GUILayout.Height(28)))
                {
                    EnsureMaterials(grid);
                    Undo.RegisterFullObjectHierarchyUndo(grid.gameObject, "Gerar Grid");
                    grid.BuildGrid();
                    MarkDirty(grid);
                }

                if (GUILayout.Button("Limpar Grid", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(grid.gameObject, "Limpar Grid");
                    grid.ClearGrid();
                    MarkDirty(grid);
                }
            }
        }

        static void MarkDirty(MinesweeperGrid grid)
        {
            EditorUtility.SetDirty(grid);
            if (!Application.isPlaying)
                EditorSceneManager.MarkSceneDirty(grid.gameObject.scene);
        }

        static void EnsureMaterials(MinesweeperGrid grid)
        {
            if (grid.tileMaterial == null)
                grid.tileMaterial = LoadOrCreate("TileBlack", TileColor, 0.10f);

            if (grid.lineMaterial == null)
                grid.lineMaterial = LoadOrCreate("GridLine", LineColor, 0.05f);
        }

        static Material LoadOrCreate(string assetName, Color color, float smoothness)
        {
            string path = $"{MaterialFolder}/{assetName}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            if (!Directory.Exists(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard")
                      ?? Shader.Find("Sprites/Default");

            var mat = new Material(shader) { name = assetName };
            if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))      mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }
    }
}
