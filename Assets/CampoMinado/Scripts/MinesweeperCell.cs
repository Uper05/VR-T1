using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CampoMinadoVR
{
    /// <summary>
    /// Uma celula da grid.
    ///
    /// A celula guarda o proprio estado e cuida do proprio visual. As regras
    /// (onde ficam as minas, cascata, vitoria/derrota) ficam no MinesweeperGame.
    /// Reveal() e ToggleFlag() sao as entradas publicas usadas pela interacao:
    /// elas pedem ao jogo para agir, e o jogo chama os metodos internos de volta.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinesweeperCell : MonoBehaviour
    {
        [Header("Posicao na grid")]
        [SerializeField] int x;
        [SerializeField] int y;

        [Header("Estado")]
        [SerializeField] bool hasMine;
        [SerializeField] int adjacentMines;
        [SerializeField] bool revealed;
        [SerializeField] bool flagged;

        [Header("Referencias")]
        [Tooltip("Bomba instanciada nesta celula, se houver.")]
        public MinesweeperBomb bomb;

        [Tooltip("Bandeira fincada nesta celula, se houver.")]
        public MinesweeperFlag flag;

        [Tooltip("Objeto que mostra o numero de minas vizinhas. Criado em runtime.")]
        public GameObject numberVisual;

        [Header("Eventos")]
        public UnityEvent<MinesweeperCell> onRevealed;
        public UnityEvent<MinesweeperCell> onFlagChanged;

        MinesweeperGrid grid;
        MinesweeperGame game;
        MeshRenderer meshRenderer;
        Material hiddenMaterial;
        GameObject fallbackMineVisual;

        public int X => x;
        public int Y => y;

        public bool HasMine       { get => hasMine;       set => hasMine = value; }
        public int  AdjacentMines { get => adjacentMines; set => adjacentMines = value; }
        public bool Revealed      => revealed;
        public bool Flagged       => flagged;

        /// <summary>Grid dona desta celula (resolvida sob demanda, sobrevive a recarga da cena).</summary>
        public MinesweeperGrid Grid =>
            grid != null ? grid : (grid = GetComponentInParent<MinesweeperGrid>());

        public MinesweeperGame Game => game;

        /// <summary>Topo da placa em coordenadas de mundo. Util para posicionar coisas em cima.</summary>
        public Vector3 TopPosition =>
            transform.position + Vector3.up * (transform.lossyScale.y * 0.5f);

        public void Setup(int gridX, int gridY)
        {
            x = gridX;
            y = gridY;
        }

        /// <summary>Liga a celula ao jogo e guarda o material original (escondido).</summary>
        public void Bind(MinesweeperGame owner)
        {
            game = owner;
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && hiddenMaterial == null)
                hiddenMaterial = meshRenderer.sharedMaterial;
        }

        // ------------------------------------------------------------------
        // Entradas publicas (chamadas pela interacao VR ou por UnityEvents)
        // ------------------------------------------------------------------

        /// <summary>Pede para revelar esta celula.</summary>
        public void Reveal()
        {
            if (game != null) game.RequestReveal(this);
            else MarkRevealed(null, Color.white, 0f);
        }

        /// <summary>Pede para marcar/desmarcar a bandeira nesta celula.</summary>
        public void ToggleFlag()
        {
            if (game != null) game.RequestToggleFlag(this);
            else SetFlagged(!flagged);
        }

        // ------------------------------------------------------------------
        // Chamados pelo MinesweeperGame
        // ------------------------------------------------------------------

        /// <summary>Marca como revelada, troca o material e mostra o numero.</summary>
        public void MarkRevealed(Material revealedMaterial, Color numberColor, float numberFontSize)
        {
            if (revealed) return;

            revealed = true;

            if (meshRenderer != null && revealedMaterial != null)
                meshRenderer.sharedMaterial = revealedMaterial;

            if (!hasMine && adjacentMines > 0 && numberFontSize > 0f)
                ShowNumber(numberColor, numberFontSize);

            onRevealed?.Invoke(this);
        }

        public void SetFlagged(bool value)
        {
            if (revealed || flagged == value) return;

            flagged = value;
            onFlagChanged?.Invoke(this);
        }

        /// <summary>Mostra a mina desta celula (fim de jogo).</summary>
        public void ShowMine(Material tileMaterial, bool exploded)
        {
            if (!hasMine) return;

            revealed = true;
            if (meshRenderer != null && tileMaterial != null)
                meshRenderer.sharedMaterial = tileMaterial;

            if (bomb != null)
            {
                if (exploded) bomb.Trigger();
                else bomb.SetVisualVisible(true);
                return;
            }

            // Sem prefab de bomba: usa uma esfera simples.
            if (fallbackMineVisual == null)
            {
                fallbackMineVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                fallbackMineVisual.name = $"Mine_{x}_{y}";
                Destroy(fallbackMineVisual.GetComponent<Collider>());

                // Parenteia no GridRoot (escala 1) para nao herdar a escala achatada da placa.
                fallbackMineVisual.transform.SetParent(transform.parent, true);

                float size = Mathf.Min(transform.lossyScale.x, transform.lossyScale.z) * 0.5f;
                fallbackMineVisual.transform.localScale = Vector3.one * size;
                fallbackMineVisual.transform.position = TopPosition + Vector3.up * size * 0.5f;

                var mat = fallbackMineVisual.GetComponent<MeshRenderer>().material;
                var mineColor = new Color(0.08f, 0.08f, 0.08f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", mineColor);
                if (mat.HasProperty("_Color"))     mat.SetColor("_Color", mineColor);
            }
            fallbackMineVisual.SetActive(true);
        }

        /// <summary>Volta a celula ao estado inicial.</summary>
        public void ResetCell()
        {
            revealed = false;
            flagged = false;
            hasMine = false;
            adjacentMines = 0;

            if (meshRenderer != null && hiddenMaterial != null)
                meshRenderer.sharedMaterial = hiddenMaterial;

            if (numberVisual != null) numberVisual.SetActive(false);
            if (fallbackMineVisual != null) fallbackMineVisual.SetActive(false);
            if (bomb != null) bomb.SetVisualVisible(false);
        }

        void ShowNumber(Color color, float fontSize)
        {
            TextMeshPro text;

            if (numberVisual == null)
            {
                numberVisual = new GameObject($"Number_{x}_{y}");
                // Mesmo motivo da mina: GridRoot tem escala 1, a placa nao.
                numberVisual.transform.SetParent(transform.parent, false);

                text = numberVisual.AddComponent<TextMeshPro>();
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = FontStyles.Bold;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.rectTransform.sizeDelta = new Vector2(transform.lossyScale.x, transform.lossyScale.z);
            }
            else
            {
                text = numberVisual.GetComponent<TextMeshPro>();
            }

            // Deitado sobre a placa, levemente acima para nao brigar com a superficie.
            numberVisual.transform.position = TopPosition + Vector3.up * 0.002f;
            numberVisual.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            text.text = adjacentMines.ToString();
            text.fontSize = fontSize;
            text.color = color;
            numberVisual.SetActive(true);
        }

        public override string ToString() => $"Cell({x},{y})";
    }
}
