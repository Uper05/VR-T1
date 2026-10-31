using UnityEngine;
using UnityEngine.Events;

namespace CampoMinadoVR
{
    /// <summary>
    /// Uma celula da grid.
    ///
    /// ESBOCO: o estado e os eventos ja estao aqui, mas a logica de verdade
    /// (revelar em cascata, contar minas, ganhar/perder) ainda nao foi escrita.
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

        [Header("Referencias (opcionais por enquanto)")]
        [Tooltip("Bomba instanciada nesta celula, se houver.")]
        public MinesweeperBomb bomb;

        [Tooltip("Bandeira instanciada nesta celula, se houver.")]
        public MinesweeperFlag flag;

        [Tooltip("Objeto que mostra o numero de minas vizinhas. Ainda nao usado.")]
        public GameObject numberVisual;

        [Header("Eventos")]
        public UnityEvent<MinesweeperCell> onRevealed;
        public UnityEvent<MinesweeperCell> onFlagChanged;

        MinesweeperGrid grid;

        public int X => x;
        public int Y => y;

        public bool HasMine       { get => hasMine;       set => hasMine = value; }
        public int  AdjacentMines { get => adjacentMines; set => adjacentMines = value; }
        public bool Revealed      => revealed;
        public bool Flagged       => flagged;

        /// <summary>Grid dona desta celula (resolvida sob demanda, sobrevive a recarga da cena).</summary>
        public MinesweeperGrid Grid =>
            grid != null ? grid : (grid = GetComponentInParent<MinesweeperGrid>());

        public void Setup(int gridX, int gridY)
        {
            x = gridX;
            y = gridY;
        }

        // ------------------------------------------------------------------
        // ESBOCOS - preencher conforme a logica do jogo for definida
        // ------------------------------------------------------------------

        /// <summary>
        /// Revela a celula. ESBOCO.
        /// TODO: trocar material/animar a placa, mostrar o numero de vizinhos,
        /// disparar a bomba se houver uma, e propagar em cascata quando
        /// adjacentMines == 0.
        /// </summary>
        public void Reveal()
        {
            if (revealed || flagged) return;

            revealed = true;
            onRevealed?.Invoke(this);

            // TODO: feedback visual da celula revelada
            // TODO: if (hasMine) bomb?.Trigger();
            // TODO: if (adjacentMines == 0) revelar vizinhos recursivamente
        }

        /// <summary>
        /// Marca / desmarca a bandeira. ESBOCO.
        /// TODO: instanciar ou esconder o objeto da bandeira e atualizar o contador da HUD.
        /// </summary>
        public void ToggleFlag()
        {
            if (revealed) return;

            flagged = !flagged;
            onFlagChanged?.Invoke(this);

            // TODO: flag?.SetPlanted(flagged);
        }

        /// <summary>Volta a celula ao estado inicial. ESBOCO.</summary>
        public void ResetCell()
        {
            revealed = false;
            flagged = false;
            hasMine = false;
            adjacentMines = 0;

            // TODO: restaurar material original e esconder numero/bandeira/bomba
        }

        public override string ToString() => $"Cell({x},{y})";
    }
}
