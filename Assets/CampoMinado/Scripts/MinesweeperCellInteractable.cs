using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CampoMinadoVR
{
    /// <summary>
    /// Liga a celula ao XR Interaction Toolkit.
    ///
    /// Selecionar e soltar rapido = revelar.
    /// Segurar a selecao por holdToFlagSeconds = colocar/tirar bandeira.
    ///
    /// "Selecionar" e o botao de select do interactor: no XR Origin dos Starter
    /// Assets e o grip do controle (mirando com o raio ou tocando de perto)
    /// e o gesto de pinca no hand tracking.
    ///
    /// O MinesweeperGame adiciona este componente sozinho em todas as celulas no Play.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MinesweeperCell))]
    public class MinesweeperCellInteractable : MonoBehaviour
    {
        [Tooltip("Segundos segurando para marcar bandeira em vez de revelar.")]
        [Min(0.1f)] public float holdToFlagSeconds = 0.5f;

        [Tooltip("Quanto a celula clareia quando o raio passa por cima.")]
        [Range(0f, 1f)] public float hoverBrighten = 0.35f;

        [Header("Haptic")]
        [Range(0f, 1f)] public float revealHaptic = 0.25f;
        [Range(0f, 1f)] public float flagHaptic = 0.6f;

        MinesweeperCell cell;
        XRSimpleInteractable interactable;
        MeshRenderer meshRenderer;
        MaterialPropertyBlock block;

        IXRSelectInteractor holdingInteractor;
        float holdStartTime;
        bool flagFiredThisHold;
        int hoverCount;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        void Awake()
        {
            cell = GetComponent<MinesweeperCell>();
            meshRenderer = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();

            interactable = GetComponent<XRSimpleInteractable>();
            if (interactable == null)
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
        }

        void OnEnable()
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }

        void OnDisable()
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        void Update()
        {
            if (holdingInteractor == null || flagFiredThisHold) return;

            if (Time.time - holdStartTime >= holdToFlagSeconds)
            {
                flagFiredThisHold = true;
                if (!cell.Revealed)
                {
                    cell.ToggleFlag();
                    Haptic(holdingInteractor, flagHaptic, 0.12f);
                }
            }
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            holdingInteractor = args.interactorObject;
            holdStartTime = Time.time;
            flagFiredThisHold = false;
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            if (args.interactorObject != holdingInteractor) return;

            // Soltou antes do tempo de bandeira: revela.
            // isCanceled = selecao interrompida (ex.: controle perdeu tracking), ignora.
            if (!flagFiredThisHold && !args.isCanceled && !cell.Revealed && !cell.Flagged)
            {
                cell.Reveal();
                Haptic(args.interactorObject, revealHaptic, 0.05f);
            }

            holdingInteractor = null;
            RefreshHighlight();
        }

        void OnHoverEntered(HoverEnterEventArgs args)
        {
            hoverCount++;
            RefreshHighlight();
        }

        void OnHoverExited(HoverExitEventArgs args)
        {
            hoverCount = Mathf.Max(0, hoverCount - 1);
            RefreshHighlight();
        }

        /// <summary>
        /// Clareia a celula enquanto alguem aponta para ela. Recalcula a partir do
        /// material atual porque ele muda quando a celula e revelada.
        /// </summary>
        void RefreshHighlight()
        {
            if (meshRenderer == null) return;

            bool highlight = hoverCount > 0 && !cell.Revealed;
            if (!highlight)
            {
                meshRenderer.SetPropertyBlock(null);
                return;
            }

            var mat = meshRenderer.sharedMaterial;
            if (mat == null) return;

            int id = mat.HasProperty(BaseColorId) ? BaseColorId : ColorId;
            if (!mat.HasProperty(id)) return;

            var color = Color.Lerp(mat.GetColor(id), Color.white, hoverBrighten);
            meshRenderer.GetPropertyBlock(block);
            block.SetColor(id, color);
            meshRenderer.SetPropertyBlock(block);
        }

        static void Haptic(IXRSelectInteractor interactor, float amplitude, float duration)
        {
            if (amplitude <= 0f) return;
            if (interactor is XRBaseInputInteractor input)
                input.SendHapticImpulse(amplitude, duration);
        }
    }
}
