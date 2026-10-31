using UnityEngine;
using UnityEngine.Events;

namespace CampoMinadoVR
{
    /// <summary>
    /// ESBOCO da bandeira. Nada aqui esta finalizado — os campos e a "forma" das
    /// funcoes estao definidos para voce ir preenchendo.
    ///
    /// Ideia de uso: o jogador pega a bandeira com a mao virtual (XR Grab) e
    /// encaixa numa celula, ou aponta e aperta um botao para plantar.
    /// </summary>
    [DisallowMultipleComponent]
    public class MinesweeperFlag : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Celula onde a bandeira esta fincada. Null = na mao ou guardada.")]
        public MinesweeperCell cell;

        [Tooltip("Mastro + pano. Fica desligado enquanto a bandeira nao e plantada.")]
        public GameObject visual;

        [Header("Encaixe")]
        [Tooltip("Altura da base da bandeira em relacao ao topo da celula.")]
        public float heightOffset = 0f;

        [Tooltip("Se a bandeira deve girar para ficar de frente para o jogador.")]
        public bool faceThePlayer = false;

        [Tooltip("Tempo da animacao de fincar/remover, em segundos.")]
        [Min(0f)] public float plantDuration = 0.15f;

        [Header("Feedback")]
        public AudioClip plantSfx;
        public AudioClip removeSfx;

        [Header("Eventos")]
        public UnityEvent<MinesweeperCell> onPlanted;
        public UnityEvent<MinesweeperCell> onRemoved;

        public bool IsPlanted { get; private set; }

        // ------------------------------------------------------------------
        // ESBOCOS
        // ------------------------------------------------------------------

        /// <summary>
        /// Finca a bandeira numa celula. ESBOCO.
        /// TODO: animar a descida ate a posicao, tocar o SFX, travar o grab
        /// e bloquear o Reveal daquela celula.
        /// </summary>
        public void Plant(MinesweeperCell target)
        {
            if (target == null || IsPlanted) return;

            cell = target;
            IsPlanted = true;

            SnapToCell(target);
            SetVisualVisible(true);
            onPlanted?.Invoke(target);

            // TODO: AudioSource.PlayClipAtPoint(plantSfx, transform.position);
            // TODO: atualizar o contador de bandeiras na HUD
        }

        /// <summary>Tira a bandeira da celula. ESBOCO.</summary>
        public void Remove()
        {
            if (!IsPlanted) return;

            var previous = cell;
            cell = null;
            IsPlanted = false;

            SetVisualVisible(false);
            onRemoved?.Invoke(previous);

            // TODO: devolver a bandeira para o "estoque" ou para a mao do jogador
        }

        /// <summary>Atalho para plantar/remover na mesma celula. ESBOCO.</summary>
        public void Toggle(MinesweeperCell target)
        {
            if (IsPlanted && cell == target) Remove();
            else Plant(target);
        }

        /// <summary>
        /// Posiciona a bandeira em cima da celula. ESBOCO.
        ///
        /// Nao parenteia na celula de proposito: a placa da celula tem escala
        /// nao-uniforme (0.6, 0.02, 0.6) e achataria a bandeira. Usa o GridRoot,
        /// que tem escala 1.
        ///
        /// TODO: trocar o posicionamento seco por um lerp usando plantDuration,
        /// e aplicar o faceThePlayer olhando para a camera do XR Origin.
        /// </summary>
        public void SnapToCell(MinesweeperCell target)
        {
            if (target == null) return;

            var gridRoot = target.transform.parent;
            transform.SetParent(gridRoot, true);

            float halfTileHeight = target.transform.lossyScale.y * 0.5f;
            transform.position = target.transform.position
                               + Vector3.up * (halfTileHeight + heightOffset);
            transform.rotation = Quaternion.identity;

            // TODO: faceThePlayer
        }

        /// <summary>Liga/desliga a malha da bandeira. ESBOCO.</summary>
        public void SetVisualVisible(bool visible)
        {
            if (visual != null)
                visual.SetActive(visible);
        }
    }
}
