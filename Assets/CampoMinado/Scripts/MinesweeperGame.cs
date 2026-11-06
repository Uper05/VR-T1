using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CampoMinadoVR
{
    public enum GameState { Ready, Playing, Won, Lost }

    /// <summary>
    /// Regras do campo minado: sorteio das minas, contagem de vizinhos,
    /// revelacao em cascata, bandeiras, vitoria e derrota.
    ///
    /// Coloque no mesmo GameObject do MinesweeperGrid (o "CampoMinado" da cena).
    /// No Play ele liga todas as celulas, adiciona a interacao XR em cada uma
    /// e comeca uma partida.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MinesweeperGrid))]
    public class MinesweeperGame : MonoBehaviour
    {
        [Header("Partida")]
        [Min(1)] public int mineCount = 20;

        [Tooltip("Garante que a primeira celula revelada (e suas vizinhas) nunca tenham mina.")]
        public bool safeFirstReveal = true;

        [Tooltip("Segundos ate reiniciar sozinho depois de ganhar ou perder. 0 = nao reinicia.")]
        [Min(0f)] public float autoRestartDelay = 4f;

        [Header("Materiais")]
        [Tooltip("Celula revelada, casas pares (sugestao: TileLight).")]
        public Material revealedMaterial;

        [Tooltip("Celula revelada, casas impares, para formar xadrez (sugestao: TileDark). Opcional.")]
        public Material revealedAltMaterial;

        [Tooltip("Celula da mina que explodiu. Se vazio, cria um vermelho em runtime.")]
        public Material explodedMaterial;

        [Header("Numeros")]
        [Min(0f)] public float numberFontSize = 3.5f;

        public Color[] numberColors =
        {
            new Color(0.10f, 0.25f, 0.85f), // 1
            new Color(0.10f, 0.55f, 0.15f), // 2
            new Color(0.85f, 0.10f, 0.10f), // 3
            new Color(0.10f, 0.10f, 0.50f), // 4
            new Color(0.50f, 0.10f, 0.10f), // 5
            new Color(0.05f, 0.50f, 0.50f), // 6
            new Color(0.05f, 0.05f, 0.05f), // 7
            new Color(0.35f, 0.35f, 0.35f), // 8
        };

        [Header("Bandeira")]
        [Tooltip("Prefab com MinesweeperFlag. Se vazio, monta uma bandeira simples em runtime.")]
        public MinesweeperFlag flagPrefab;

        [Header("Interacao VR")]
        [Tooltip("Adiciona MinesweeperCellInteractable em todas as celulas no Play.")]
        public bool addInteractionToCells = true;

        [Header("HUD")]
        [Tooltip("Texto de status. Se vazio, cria um atras da grid.")]
        public TextMeshPro statusText;

        [Header("Eventos")]
        public UnityEvent onGameStarted;
        public UnityEvent onGameWon;
        public UnityEvent onGameLost;
        public UnityEvent<int> onMinesRemainingChanged;

        MinesweeperGrid grid;
        readonly List<MinesweeperCell> cells = new List<MinesweeperCell>();
        bool minesPlaced;
        int revealedSafeCount;
        int flagCount;
        int activeMineCount;
        Coroutine restartRoutine;

        public GameState State { get; private set; } = GameState.Ready;
        public int MinesRemaining => activeMineCount - flagCount;

        void Awake()
        {
            grid = GetComponent<MinesweeperGrid>();
        }

        void Start()
        {
            grid.RefreshCells();
            if (grid.Cells.Count == 0)
                grid.BuildGrid();

            cells.Clear();
            cells.AddRange(grid.Cells);

            foreach (var cell in cells)
            {
                cell.Bind(this);
                if (addInteractionToCells && cell.GetComponent<MinesweeperCellInteractable>() == null)
                    cell.gameObject.AddComponent<MinesweeperCellInteractable>();
            }

            if (explodedMaterial == null && revealedMaterial != null)
            {
                explodedMaterial = new Material(revealedMaterial) { name = "Exploded (runtime)" };
                var red = new Color(0.85f, 0.15f, 0.12f);
                if (explodedMaterial.HasProperty("_BaseColor")) explodedMaterial.SetColor("_BaseColor", red);
                if (explodedMaterial.HasProperty("_Color"))     explodedMaterial.SetColor("_Color", red);
            }

            if (statusText == null)
                statusText = CreateStatusText();

            NewGame();
        }

        // ------------------------------------------------------------------
        // Controle da partida
        // ------------------------------------------------------------------

        /// <summary>Limpa o tabuleiro e espera a primeira revelacao para sortear as minas.</summary>
        [ContextMenu("Nova Partida")]
        public void NewGame()
        {
            if (restartRoutine != null)
            {
                StopCoroutine(restartRoutine);
                restartRoutine = null;
            }

            foreach (var cell in cells)
            {
                if (cell.flag != null) cell.flag.Remove();
                cell.ResetCell();
            }

            int maxMines = Mathf.Max(1, cells.Count - 1);
            activeMineCount = Mathf.Clamp(mineCount, 1, maxMines);

            minesPlaced = false;
            revealedSafeCount = 0;
            flagCount = 0;
            State = GameState.Ready;

            UpdateStatus();
            onMinesRemainingChanged?.Invoke(MinesRemaining);
            onGameStarted?.Invoke();
        }

        public void RequestReveal(MinesweeperCell cell)
        {
            if (cell == null || cell.Revealed || cell.Flagged) return;
            if (State == GameState.Won || State == GameState.Lost) return;

            if (!minesPlaced)
            {
                PlaceMines(cell);
                State = GameState.Playing;
            }

            if (cell.HasMine)
            {
                Lose(cell);
                return;
            }

            FloodReveal(cell);

            if (revealedSafeCount >= cells.Count - activeMineCount)
                Win();
        }

        public void RequestToggleFlag(MinesweeperCell cell)
        {
            if (cell == null || cell.Revealed) return;
            if (State == GameState.Won || State == GameState.Lost) return;

            bool planting = !cell.Flagged;
            cell.SetFlagged(planting);

            if (planting)
            {
                if (cell.flag == null) cell.flag = CreateFlag();
                cell.flag.Plant(cell);
                flagCount++;
            }
            else
            {
                if (cell.flag != null) cell.flag.Remove();
                flagCount--;
            }

            UpdateStatus();
            onMinesRemainingChanged?.Invoke(MinesRemaining);
        }

        // ------------------------------------------------------------------
        // Regras
        // ------------------------------------------------------------------

        void PlaceMines(MinesweeperCell firstCell)
        {
            var excluded = new HashSet<MinesweeperCell> { firstCell };
            if (safeFirstReveal)
            {
                foreach (var n in grid.GetNeighbours(firstCell.X, firstCell.Y))
                    excluded.Add(n);
            }

            var candidates = new List<MinesweeperCell>();
            foreach (var cell in cells)
                if (!excluded.Contains(cell)) candidates.Add(cell);

            // Grid pequena demais para proteger os vizinhos: protege so a primeira celula.
            if (candidates.Count < activeMineCount)
            {
                candidates.Clear();
                foreach (var cell in cells)
                    if (cell != firstCell) candidates.Add(cell);
                activeMineCount = Mathf.Min(activeMineCount, candidates.Count);
            }

            // Fisher-Yates parcial: embaralha so o necessario.
            for (int i = 0; i < activeMineCount; i++)
            {
                int j = Random.Range(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                candidates[i].HasMine = true;
                if (candidates[i].bomb != null) candidates[i].bomb.Arm();
            }

            foreach (var cell in cells)
            {
                int count = 0;
                foreach (var n in grid.GetNeighbours(cell.X, cell.Y))
                    if (n.HasMine) count++;
                cell.AdjacentMines = count;
            }

            minesPlaced = true;
        }

        /// <summary>
        /// Revela a celula e, se ela nao tiver minas vizinhas, abre a regiao em volta.
        /// Usa fila em vez de recursao para nao estourar a pilha em grids grandes.
        /// </summary>
        void FloodReveal(MinesweeperCell start)
        {
            var queue = new Queue<MinesweeperCell>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (cell.Revealed || cell.Flagged || cell.HasMine) continue;

                cell.MarkRevealed(RevealedMaterialFor(cell), NumberColorFor(cell.AdjacentMines), numberFontSize);
                revealedSafeCount++;

                if (cell.AdjacentMines == 0)
                {
                    foreach (var n in grid.GetNeighbours(cell.X, cell.Y))
                        if (!n.Revealed && !n.Flagged) queue.Enqueue(n);
                }
            }
        }

        void Lose(MinesweeperCell exploded)
        {
            State = GameState.Lost;

            exploded.ShowMine(explodedMaterial, true);
            foreach (var cell in cells)
            {
                if (cell != exploded && cell.HasMine && !cell.Flagged)
                    cell.ShowMine(RevealedMaterialFor(cell), false);
            }

            UpdateStatus();
            onGameLost?.Invoke();
            ScheduleRestart();
        }

        void Win()
        {
            State = GameState.Won;

            // Planta bandeira nas minas que faltaram, como no classico.
            foreach (var cell in cells)
            {
                if (cell.HasMine && !cell.Flagged)
                {
                    cell.SetFlagged(true);
                    if (cell.flag == null) cell.flag = CreateFlag();
                    cell.flag.Plant(cell);
                    flagCount++;
                }
            }

            UpdateStatus();
            onMinesRemainingChanged?.Invoke(MinesRemaining);
            onGameWon?.Invoke();
            ScheduleRestart();
        }

        void ScheduleRestart()
        {
            if (autoRestartDelay > 0f)
                restartRoutine = StartCoroutine(RestartAfter(autoRestartDelay));
        }

        IEnumerator RestartAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            restartRoutine = null;
            NewGame();
        }

        // ------------------------------------------------------------------
        // Visual
        // ------------------------------------------------------------------

        Material RevealedMaterialFor(MinesweeperCell cell)
        {
            if (revealedAltMaterial != null && (cell.X + cell.Y) % 2 == 1)
                return revealedAltMaterial;
            return revealedMaterial;
        }

        Color NumberColorFor(int count)
        {
            if (count <= 0 || numberColors == null || numberColors.Length == 0) return Color.black;
            return numberColors[Mathf.Min(count, numberColors.Length) - 1];
        }

        void UpdateStatus()
        {
            if (statusText == null) return;

            switch (State)
            {
                case GameState.Won:
                    statusText.text = "Voce venceu!";
                    statusText.color = new Color(0.3f, 0.9f, 0.4f);
                    break;
                case GameState.Lost:
                    statusText.text = "Boom! Voce perdeu";
                    statusText.color = new Color(1f, 0.35f, 0.3f);
                    break;
                default:
                    statusText.text = $"Minas: {MinesRemaining}";
                    statusText.color = Color.white;
                    break;
            }
        }

        TextMeshPro CreateStatusText()
        {
            var go = new GameObject("StatusText");
            go.transform.SetParent(transform, false);

            // Em pe, na borda do fundo da grid (+Z), virado para quem esta em -Z.
            go.transform.localPosition = new Vector3(0f, 0.6f, grid.TotalDepth * 0.5f + 0.3f);
            go.transform.localRotation = Quaternion.identity;

            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
            text.fontSize = 6f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(grid.TotalWidth, 1f);
            return text;
        }

        MinesweeperFlag CreateFlag()
        {
            if (flagPrefab != null)
                return Instantiate(flagPrefab);

            // Bandeira simples: mastro (cilindro) + pano (cubo fino vermelho).
            var root = new GameObject("Flag");
            var flag = root.AddComponent<MinesweeperFlag>();

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);

            float size = grid.cellSize;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            Destroy(pole.GetComponent<Collider>());
            pole.transform.SetParent(visual.transform, false);
            pole.transform.localScale = new Vector3(size * 0.04f, size * 0.35f, size * 0.04f);
            pole.transform.localPosition = new Vector3(0f, size * 0.35f, 0f);
            Tint(pole, new Color(0.85f, 0.85f, 0.85f));

            var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cloth.name = "Cloth";
            Destroy(cloth.GetComponent<Collider>());
            cloth.transform.SetParent(visual.transform, false);
            cloth.transform.localScale = new Vector3(size * 0.3f, size * 0.2f, size * 0.01f);
            cloth.transform.localPosition = new Vector3(size * 0.15f, size * 0.58f, 0f);
            Tint(cloth, new Color(0.9f, 0.15f, 0.12f));

            flag.visual = visual;
            visual.SetActive(false);
            return flag;
        }

        static void Tint(GameObject go, Color color)
        {
            var mat = go.GetComponent<MeshRenderer>().material;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);
        }
    }
}
