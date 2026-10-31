using System.Collections.Generic;
using UnityEngine;

namespace CampoMinadoVR
{
    /// <summary>
    /// Gera uma grid de celulas no chao, centrada na posicao deste GameObject.
    ///
    /// As celulas sao placas pretas separadas por um vao (lineWidth). Embaixo delas
    /// fica uma laje cinza, e e ela que aparece nos vaos formando as linhas da grid.
    ///
    /// Use os botoes "Gerar Grid" / "Limpar Grid" no Inspector,
    /// ou o menu de contexto (botao direito no cabecalho do componente).
    /// </summary>
    [DisallowMultipleComponent]
    public class MinesweeperGrid : MonoBehaviour
    {
        [Header("Dimensoes")]
        [Min(1)] public int width = 12;
        [Min(1)] public int height = 12;

        [Header("Medidas (metros)")]
        [Min(0.05f)] public float cellSize = 0.6f;

        [Tooltip("Vao entre as celulas. E por aqui que a linha cinza aparece.")]
        [Min(0f)] public float lineWidth = 0.03f;

        [Min(0.001f)] public float tileThickness = 0.02f;

        [Header("Visual")]
        [Tooltip("Material das celulas (preto).")]
        public Material tileMaterial;

        [Tooltip("Material da laje que forma as linhas (cinza).")]
        public Material lineMaterial;

        [Header("Linhas")]
        public bool showGridLines = true;

        [Tooltip("Quanto a laje cinza fica abaixo do topo das celulas. Da profundidade a linha.")]
        [Min(0f)] public float lineRecess = 0.004f;

        [Tooltip("Borda cinza em volta da grid inteira. 0 = sem borda.")]
        [Min(0f)] public float borderWidth = 0.03f;

        [Header("Execucao")]
        [Tooltip("Gera a grid automaticamente no Play, caso ela ainda nao exista na cena.")]
        public bool buildOnStart = false;

        public const string GridRootName = "GridRoot";
        public const string LinesName = "GridLines";

        const float LineSlabThickness = 0.05f;

        readonly List<MinesweeperCell> cells = new List<MinesweeperCell>();

        /// <summary>Celulas geradas na ultima chamada de BuildGrid (em runtime).</summary>
        public IReadOnlyList<MinesweeperCell> Cells => cells;

        /// <summary>Largura total ocupada pela grid, em metros.</summary>
        public float TotalWidth => width * cellSize + Mathf.Max(0, width - 1) * lineWidth;

        /// <summary>Profundidade total ocupada pela grid, em metros.</summary>
        public float TotalDepth => height * cellSize + Mathf.Max(0, height - 1) * lineWidth;

        void Start()
        {
            if (buildOnStart && FindGridRoot() == null)
                BuildGrid();
        }

        [ContextMenu("Gerar Grid")]
        public void BuildGrid()
        {
            ClearGrid();

            var root = new GameObject(GridRootName);
            root.transform.SetParent(transform, false);

            if (showGridLines)
                CreateLineSlab(root.transform);

            float originX = -TotalWidth * 0.5f + cellSize * 0.5f;
            float originZ = -TotalDepth * 0.5f + cellSize * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = $"Cell_{x}_{y}";
                    tile.transform.SetParent(root.transform, false);
                    tile.transform.localPosition = new Vector3(
                        originX + x * (cellSize + lineWidth),
                        tileThickness * 0.5f,
                        originZ + y * (cellSize + lineWidth));
                    tile.transform.localScale = new Vector3(cellSize, tileThickness, cellSize);

                    var cell = tile.AddComponent<MinesweeperCell>();
                    cell.Setup(x, y);
                    cells.Add(cell);

                    if (tileMaterial != null)
                        tile.GetComponent<MeshRenderer>().sharedMaterial = tileMaterial;
                }
            }
        }

        [ContextMenu("Limpar Grid")]
        public void ClearGrid()
        {
            cells.Clear();

            // Coleta primeiro e destroi depois: em Play Mode o Destroy e adiado,
            // entao procurar dentro do laco causaria loop infinito.
            var toRemove = new List<GameObject>();
            foreach (Transform child in transform)
            {
                if (child.name == GridRootName)
                    toRemove.Add(child.gameObject);
            }

            foreach (var go in toRemove)
            {
                if (Application.isPlaying)
                {
                    go.SetActive(false);
                    Destroy(go);
                }
                else
                {
                    DestroyImmediate(go);
                }
            }
        }

        /// <summary>Devolve a celula na coordenada pedida, ou null se estiver fora da grid.</summary>
        public MinesweeperCell GetCell(int x, int y)
        {
            if (x < 0 || x >= width || y < 0 || y >= height) return null;

            foreach (var cell in cells)
            {
                if (cell != null && cell.X == x && cell.Y == y)
                    return cell;
            }
            return null;
        }

        /// <summary>Posicao local do centro de uma celula, util para spawnar coisas em cima.</summary>
        public Vector3 GetCellLocalPosition(int x, int y)
        {
            float originX = -TotalWidth * 0.5f + cellSize * 0.5f;
            float originZ = -TotalDepth * 0.5f + cellSize * 0.5f;
            return new Vector3(
                originX + x * (cellSize + lineWidth),
                tileThickness * 0.5f,
                originZ + y * (cellSize + lineWidth));
        }

        /// <summary>
        /// Percorre as 8 celulas vizinhas de (x, y), pulando as que caem fora da grid.
        /// Vai ser util para contar minas adjacentes.
        /// </summary>
        public IEnumerable<MinesweeperCell> GetNeighbours(int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    var neighbour = GetCell(x + dx, y + dy);
                    if (neighbour != null)
                        yield return neighbour;
                }
            }
        }

        Transform FindGridRoot()
        {
            foreach (Transform child in transform)
            {
                if (child.name == GridRootName)
                    return child;
            }
            return null;
        }

        void CreateLineSlab(Transform parent)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = LinesName;
            slab.transform.SetParent(parent, false);

            // Topo da laje fica logo abaixo do topo das celulas, entao o vao
            // entre elas vira um sulco cinza.
            float top = tileThickness - lineRecess;
            slab.transform.localPosition = new Vector3(0f, top - LineSlabThickness * 0.5f, 0f);
            slab.transform.localScale = new Vector3(
                TotalWidth + borderWidth * 2f,
                LineSlabThickness,
                TotalDepth + borderWidth * 2f);

            if (lineMaterial != null)
                slab.GetComponent<MeshRenderer>().sharedMaterial = lineMaterial;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(
                new Vector3(0f, tileThickness * 0.5f, 0f),
                new Vector3(TotalWidth, tileThickness, TotalDepth));
        }
    }
}
