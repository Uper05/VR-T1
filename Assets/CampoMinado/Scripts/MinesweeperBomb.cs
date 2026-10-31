using UnityEngine;
using UnityEngine.Events;

namespace CampoMinadoVR
{
    /// <summary>
    /// ESBOCO da bomba. Nada aqui esta finalizado — os campos e a "forma" das
    /// funcoes estao definidos para voce ir preenchendo.
    ///
    /// Ideia de uso: a celula que tem mina instancia um prefab com este componente,
    /// escondido ate ser revelado.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinesweeperBomb : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Celula onde esta bomba esta plantada.")]
        public MinesweeperCell cell;

        [Tooltip("Malha/modelo da bomba. Fica desligado enquanto nao for revelada.")]
        public GameObject visual;

        [Header("Parametros")]
        [Tooltip("Segundos entre acionar e explodir. 0 = instantaneo.")]
        [Min(0f)] public float fuseSeconds = 0f;

        [Tooltip("Raio da explosao, em celulas. Para um campo minado classico, 0.")]
        [Min(0f)] public float blastRadiusInCells = 0f;

        [Tooltip("Forca aplicada em Rigidbodies proximos. So se voce quiser fisica.")]
        [Min(0f)] public float blastForce = 0f;

        [Header("Feedback")]
        public GameObject explosionVfxPrefab;
        public AudioClip explosionSfx;

        [Tooltip("Intensidade do haptic no controle ao explodir (0 a 1).")]
        [Range(0f, 1f)] public float hapticAmplitude = 0.8f;

        [Tooltip("Duracao do haptic, em segundos.")]
        [Min(0f)] public float hapticDuration = 0.3f;

        [Header("Eventos")]
        public UnityEvent onArmed;
        public UnityEvent onDefused;
        public UnityEvent onExploded;

        public bool IsArmed     { get; private set; }
        public bool HasExploded { get; private set; }

        // ------------------------------------------------------------------
        // ESBOCOS
        // ------------------------------------------------------------------

        /// <summary>Planta/arma a bomba nesta celula. ESBOCO.</summary>
        public void Arm()
        {
            if (HasExploded) return;

            IsArmed = true;
            if (cell != null) cell.HasMine = true;

            SetVisualVisible(false);
            onArmed?.Invoke();

            // TODO: registrar a bomba no gerenciador do tabuleiro
        }

        /// <summary>Desarma sem explodir (bandeira correta, modo debug, etc). ESBOCO.</summary>
        public void Defuse()
        {
            if (HasExploded) return;

            IsArmed = false;
            onDefused?.Invoke();

            // TODO: animacao de "desarmada" / contagem de acertos
        }

        /// <summary>
        /// Aciona a bomba. Respeita o fuseSeconds. ESBOCO.
        /// TODO: trocar por corrotina quando o fuse for maior que zero,
        /// com som de pavio e piscada no material.
        /// </summary>
        public void Trigger()
        {
            if (!IsArmed || HasExploded) return;

            SetVisualVisible(true);

            if (fuseSeconds <= 0f)
                Explode();
            // TODO: else StartCoroutine(FuseRoutine());
        }

        /// <summary>
        /// A explosao em si. ESBOCO — hoje so marca o estado e dispara o evento.
        /// TODO: instanciar o VFX, tocar o SFX, aplicar haptic no controle,
        /// empurrar Rigidbodies dentro do blastRadius e avisar o fim de jogo.
        /// </summary>
        void Explode()
        {
            HasExploded = true;
            IsArmed = false;
            onExploded?.Invoke();

            // TODO: if (explosionVfxPrefab != null) Instantiate(...);
            // TODO: if (explosionSfx != null) AudioSource.PlayClipAtPoint(...);
            // TODO: haptic no controller que revelou a celula
            // TODO: avisar o gerenciador do jogo que o jogador perdeu
        }

        /// <summary>Liga/desliga a malha da bomba. ESBOCO.</summary>
        public void SetVisualVisible(bool visible)
        {
            if (visual != null)
                visual.SetActive(visible);

            // TODO: trocar por fade/escala animada em vez de SetActive seco
        }
    }
}
