using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace DevilsGame.Menu
{
    /// <summary>
    /// Логика главного меню: кнопки, панели, затемнение и загрузка уровней.
    /// Вешается на объект Menu (дочерний объект Canvas) в сцене Menu.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        [Header("Загрузка уровней")]
        [Tooltip("Сцена, которая загружается при нажатии 'Новая игра'")]
        public string newGameScene = "SampleScene";

        [Tooltip("Сцены этапов: индекс = номер этапа - 1")]
        public string[] levelScenes = new string[] { "SampleScene" };

        [Tooltip("Максимальный этап, до которого доходил игрок. Пока сохранений нет — задаётся вручную здесь")]
        public int maxReachedLevel = 0;

        [Header("Кнопки главного экрана")]
        public Button newGameButton;
        public Button continueButton;
        public Button settingsButton;
        public Button authorsButton;
        public Button exitButton;
        public Button continueCloseButton;
        public Button authorsCloseButton;
        [Tooltip("Спрайт кнопок — используется для создаваемых в рантайме кнопок этапов")]
        public Sprite buttonSprite;

        [Header("Панели")]
        [Tooltip("Панель выбора этапа (открывается кнопкой 'Продолжить')")]
        public GameObject continuePanel;
        [Tooltip("Контейнер (VerticalLayoutGroup), куда добавляются кнопки этапов")]
        public RectTransform continueButtonsContainer;
        [Tooltip("Шаблон кнопки этапа (лежит внутри контейнера, выключен)")]
        public GameObject stageButtonTemplate;
        [Tooltip("Панель настроек (экземпляр префаба SettingsMenu)")]
        public GameObject settingsPanel;
        [Tooltip("Панель авторов")]
        public GameObject authorsPanel;

        [Header("Затемнение при загрузке")]
        public CanvasGroup fadeOverlay;
        public float fadeDuration = 0.8f;

        private GameObject currentPanel;

        private void Awake()
        {
            // Все панели стартуют скрытыми и не перехватывают мышь,
            // пока не открыты через OpenPanel
            foreach (GameObject panel in new[] { continuePanel, settingsPanel, authorsPanel })
            {
                if (panel == null)
                    continue;
                CanvasGroup group = panel.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 0f;
                    group.blocksRaycasts = false;
                    group.interactable = false;
                }
            }

            if (newGameButton != null) newGameButton.onClick.AddListener(NewGame);
            if (continueButton != null) continueButton.onClick.AddListener(() => OpenPanel(continuePanel));
            if (settingsButton != null) settingsButton.onClick.AddListener(() => OpenPanel(settingsPanel));
            if (authorsButton != null) authorsButton.onClick.AddListener(() => OpenPanel(authorsPanel));
            if (exitButton != null) exitButton.onClick.AddListener(ExitGame);
            if (continueCloseButton != null) continueCloseButton.onClick.AddListener(() => ClosePanel(continuePanel));
            if (authorsCloseButton != null) authorsCloseButton.onClick.AddListener(() => ClosePanel(authorsPanel));

            // Кнопка "Продолжить" имеет смысл только если игрок уже доходил до какого-то этапа
            if (continueButton != null)
                continueButton.gameObject.SetActive(maxReachedLevel > 0);

            BuildContinueButtons();
        }

        /// <summary>
        /// Создаёт кнопки этапов: по одной для каждого этапа от 1 до maxReachedLevel.
        /// Пока сохранений нет, прогресс задаётся полем maxReachedLevel.
        /// </summary>
        private void BuildContinueButtons()
        {
            if (continueButtonsContainer == null || stageButtonTemplate == null)
                return;

            stageButtonTemplate.SetActive(false);

            // Удаляем кнопки, созданные в прошлый раз (шаблон не трогаем)
            for (int i = continueButtonsContainer.childCount - 1; i >= 0; i--)
            {
                GameObject child = continueButtonsContainer.GetChild(i).gameObject;
                if (child != stageButtonTemplate)
                    Destroy(child);
            }

            for (int stage = 1; stage <= maxReachedLevel; stage++)
            {
                int capturedStage = stage; // захват номера для замыкания

                GameObject buttonGO = Instantiate(stageButtonTemplate, continueButtonsContainer);
                buttonGO.name = "Этап " + stage;
                buttonGO.SetActive(true);

                Text label = buttonGO.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = "Этап " + stage;

                Button button = buttonGO.GetComponent<Button>();
                if (button != null)
                    button.onClick.AddListener(() => ContinueGame(capturedStage));
            }
        }

        /// <summary>Новая игра: затемняет экран и загружает сцену из поля newGameScene.</summary>
        public void NewGame()
        {
            StartCoroutine(FadeAndLoad(newGameScene));
        }

        /// <summary>Продолжить: затемняет экран и загружает сцену выбранного этапа.</summary>
        public void ContinueGame(int stage)
        {
            StartCoroutine(FadeAndLoad(GetSceneForStage(stage)));
        }

        private string GetSceneForStage(int stage)
        {
            if (levelScenes != null && stage >= 1 && stage <= levelScenes.Length)
                return levelScenes[stage - 1];
            return newGameScene;
        }

        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OpenPanel(GameObject panel)
        {
            if (panel == null)
                return;

            if (currentPanel != null && currentPanel != panel)
                ClosePanel(currentPanel);

            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = true;
                group.interactable = true;
            }

            Animator animator = panel.GetComponent<Animator>();
            if (animator != null)
                animator.SetTrigger("Open");

            currentPanel = panel;
        }

        public void ClosePanel(GameObject panel)
        {
            if (panel == null)
                return;

            Animator animator = panel.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("Close");
                StartCoroutine(DisableRaycastAfterDelay(panel, 0.3f));
            }

            if (currentPanel == panel)
                currentPanel = null;
        }

        private IEnumerator DisableRaycastAfterDelay(GameObject panel, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (panel == null) yield break;
            CanvasGroup group = panel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
            }
        }

        private IEnumerator FadeAndLoad(string sceneName)
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.blocksRaycasts = true; // блокируем клики во время затемнения
                float t = 0f;
                float start = fadeOverlay.alpha;
                while (t < fadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    fadeOverlay.alpha = Mathf.Lerp(start, 1f, t / fadeDuration);
                    yield return null;
                }
                fadeOverlay.alpha = 1f;
            }

            SceneManager.LoadScene(sceneName);
        }
    }
}
