using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using DevilsGame.Menu;

namespace DevilsGame.EditorTools
{
    /// <summary>
    /// Автоматически собирает главное меню в сцене Assets/Scenes/Menu.unity:
    /// Canvas, кнопки со спрайтами из UI_Asset, аниматоры, панели и префаб настроек.
    /// Запускается один раз после импорта скриптов и вручную: Tools -> Собрать главное меню.
    /// </summary>
    public static class MenuBuilder
    {
        private const int BuildVersion = 5;
        private const string VersionPref = "DevilsGame.MenuBuilder.BuildVersion";

        private const string AssetRoot = "Assets/UI_Asset/PNG Files (Direct use)/Icons_128 px/Buttons";
        private const string ButtonTexturePath = AssetRoot + "/Button (12).png";
        private const string FrameTexturePath = AssetRoot + "/Button (17).png";
        private const string EmblemTexturePath = AssetRoot + "/Button (8).png";

        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string AnimDir = "Assets/Sources/Menu/Animations";
        private const string SettingsPrefabPath = "Assets/Sources/Menu/SettingsMenu.prefab";

        private static Sprite buttonSprite;
        private static Sprite frameSprite;
        private static Sprite emblemSprite;
        private static Font font;

        [InitializeOnLoad]
        private static class AutoRun
        {
            static AutoRun()
            {
                EditorApplication.delayCall += AutoRunOnce;
            }

            private static void AutoRunOnce()
            {
                if (EditorPrefs.GetInt(VersionPref, 0) >= BuildVersion)
                    return;
                try
                {
                    BuildAll();
                    // Версию фиксируем только после успешной сборки —
                    // иначе сборка, пропущенная в Play mode, теряется навсегда
                    EditorPrefs.SetInt(VersionPref, BuildVersion);
                }
                catch (Exception e)
                {
                    Debug.LogError("[MenuBuilder] Ошибка автосборки: " + e);
                }
            }
        }

        [MenuItem("Tools/Собрать главное меню")]
        public static void BuildAll()
        {
            if (Application.isPlaying)
                return;

            Debug.Log("[MenuBuilder] Сборка главного меню...");

            EnsureFolder(AnimDir);
            buttonSprite = ImportSprite(ButtonTexturePath, true);
            frameSprite = ImportSprite(FrameTexturePath, true);
            emblemSprite = ImportSprite(EmblemTexturePath, false);
            font = LoadFont();

            AnimatorController buttonController = CreateButtonController();
            AnimatorController panelController = CreatePanelController();

            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            var scene = EditorSceneManager.GetActiveScene();

            DecorateScene();

            Canvas canvas = FindOrCreateCanvas();
            EnsureEventSystem();

            // Удаляем ВСЕ объекты Canvas, кроме корня Menu: панели и оверлеи прошлых
            // сборок копятся слоями и перехватывают мышь, даже будучи невидимыми.
            // (Transform.Find находит только первый двойник, поэтому обходим всех детей)
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = canvas.transform.GetChild(i);
                if (child.name != "Menu")
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            // --- Корень меню ---
            Transform menuTransform = canvas.transform.Find("Menu");
            if (menuTransform == null)
            {
                var go = new GameObject("Menu", typeof(RectTransform));
                menuTransform = go.transform;
                menuTransform.SetParent(canvas.transform, false);
                Stretch((RectTransform)menuTransform);
            }
            for (int i = menuTransform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(menuTransform.GetChild(i).gameObject);

            RectTransform menu = (RectTransform)menuTransform;
            Stretch(menu);

            // --- Фон-рамочка ---
            var backdrop = NewRect("Backdrop", menu);
            SetAnchors(backdrop, new Vector2(0.02f, 0.03f), new Vector2(0.98f, 0.97f), Vector2.zero, Vector2.zero);
            AddImage(backdrop, frameSprite, new Color(0.14f, 0.075f, 0.09f, 0.97f), true);

            // --- Эмблема (врата) ---
            var emblem = NewRect("Emblem", menu);
            SetAnchors(emblem, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-150f, -640f), new Vector2(150f, -300f));
            AddImage(emblem, emblemSprite, new Color(1f, 1f, 1f, 0.30f), false);

            // --- Заголовок ---
            var title = NewRect("Title", menu);
            SetAnchors(title, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-700f, -370f), new Vector2(700f, -195f));
            AddText(title, "DEVILS GAME", 110, new Color(0.84f, 0.13f, 0.13f, 1f), TextAnchor.MiddleCenter);

            var subtitle = NewRect("Subtitle", menu);
            SetAnchors(subtitle, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-420f, -452f), new Vector2(420f, -385f));
            AddText(subtitle, "г л а в н о е   м е н ю", 30, new Color(0.74f, 0.62f, 0.65f, 1f), TextAnchor.MiddleCenter);

            // --- Кнопки ---
            var buttonsRect = NewRect("Buttons", menu);
            SetAnchors(buttonsRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300f, -430f), new Vector2(300f, 160f));
            var layout = buttonsRect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 22f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            var newGameButton = CreateMenuButton(buttonsRect, "NewGameButton", "Новая игра", buttonSprite, buttonController, true);
            var continueButton = CreateMenuButton(buttonsRect, "ContinueButton", "Продолжить", buttonSprite, buttonController, true);
            var settingsButton = CreateMenuButton(buttonsRect, "SettingsButton", "Настройки", buttonSprite, buttonController, true);
            var authorsButton = CreateMenuButton(buttonsRect, "AuthorsButton", "Авторы", buttonSprite, buttonController, true);
            var exitButton = CreateMenuButton(buttonsRect, "ExitButton", "Выйти", buttonSprite, buttonController, true);

            // --- Панель продолжения ---
            var continuePanel = CreatePanel("ContinuePanel", canvas.transform, "Продолжить", panelController);
            var continueContent = NewRect("Buttons", continuePanel);
            SetAnchors(continueContent, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f),
                new Vector2(-320f, -80f), new Vector2(320f, -200f));
            var continueLayout = continueContent.gameObject.AddComponent<VerticalLayoutGroup>();
            continueLayout.childAlignment = TextAnchor.UpperCenter;
            continueLayout.spacing = 18f;
            continueLayout.childForceExpandWidth = true;
            continueLayout.childForceExpandHeight = false;
            continueLayout.childControlWidth = true;
            continueLayout.childControlHeight = false;

            var stageTemplate = CreateMenuButton(continueContent, "StageButtonTemplate", "Этап 1", buttonSprite, buttonController, true);
            stageTemplate.gameObject.SetActive(false);
            var continueBack = CreateMenuButton(continuePanel, "BackButton", "Назад", buttonSprite, buttonController, false);
            SetAnchors((RectTransform)continueBack.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-170f, 50f), new Vector2(170f, 130f));

            // --- Панель авторов ---
            var authorsPanel = CreatePanel("AuthorsPanel", canvas.transform, "Авторы", panelController);
            var authorsText = NewRect("Text", authorsPanel);
            SetAnchors(authorsText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-500f, -60f), new Vector2(500f, 220f));
            AddText(authorsText,
                "DevilsGame\n\nРазработка и дизайн:\nкоманда DevilsGame\n\nUI-ассеты: набор из папки UI_Asset\n\nСпасибо, что играете!",
                36, new Color(0.88f, 0.82f, 0.83f, 1f), TextAnchor.MiddleCenter);
            var authorsBack = CreateMenuButton(authorsPanel, "BackButton", "Назад", buttonSprite, buttonController, false);
            SetAnchors((RectTransform)authorsBack.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-170f, 50f), new Vector2(170f, 130f));

            // --- Панель настроек (становится префабом) ---
            var settingsPanel = CreatePanel("SettingsPanel", canvas.transform, "Настройки", panelController);
            var rowMusic = NewRect("RowMusic", settingsPanel);
            SetAnchors(rowMusic, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-560f, -280f), new Vector2(560f, -200f));
            var musicLabel = NewRect("Label", rowMusic);
            SetAnchors(musicLabel, new Vector2(0f, 0f), new Vector2(0.35f, 1f), Vector2.zero, Vector2.zero);
            AddText(musicLabel, "Музыка", 36, new Color(0.88f, 0.82f, 0.83f, 1f), TextAnchor.MiddleLeft);
            var musicSliderRect = NewRect("Slider", rowMusic);
            SetAnchors(musicSliderRect, new Vector2(0.42f, 0.1f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
            Slider musicSlider = AddSlider(musicSliderRect);

            var rowSfx = NewRect("RowSfx", settingsPanel);
            SetAnchors(rowSfx, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-560f, -400f), new Vector2(560f, -320f));
            var sfxLabel = NewRect("Label", rowSfx);
            SetAnchors(sfxLabel, new Vector2(0f, 0f), new Vector2(0.35f, 1f), Vector2.zero, Vector2.zero);
            AddText(sfxLabel, "Звуки", 36, new Color(0.88f, 0.82f, 0.83f, 1f), TextAnchor.MiddleLeft);
            var sfxSliderRect = NewRect("Slider", rowSfx);
            SetAnchors(sfxSliderRect, new Vector2(0.42f, 0.1f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);
            Slider sfxSlider = AddSlider(sfxSliderRect);

            var rowFullscreen = NewRect("RowFullscreen", settingsPanel);
            SetAnchors(rowFullscreen, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-560f, -520f), new Vector2(560f, -440f));
            var fsLabel = NewRect("Label", rowFullscreen);
            SetAnchors(fsLabel, new Vector2(0f, 0f), new Vector2(0.35f, 1f), Vector2.zero, Vector2.zero);
            AddText(fsLabel, "Полный экран", 36, new Color(0.88f, 0.82f, 0.83f, 1f), TextAnchor.MiddleLeft);
            var fsToggleRect = NewRect("Toggle", rowFullscreen);
            SetAnchors(fsToggleRect, new Vector2(0.45f, 0.05f), new Vector2(0.62f, 0.95f), Vector2.zero, Vector2.zero);
            Toggle fullscreenToggle = AddToggle(fsToggleRect);

            var settingsBack = CreateMenuButton(settingsPanel, "BackButton", "Назад", buttonSprite, buttonController, false);
            SetAnchors((RectTransform)settingsBack.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-170f, 50f), new Vector2(170f, 130f));

            var settingsComponent = settingsPanel.gameObject.AddComponent<Settings>();
            settingsComponent.musicSlider = musicSlider;
            settingsComponent.sfxSlider = sfxSlider;
            settingsComponent.fullscreenToggle = fullscreenToggle;
            settingsComponent.backButton = settingsBack;
            PrefabUtility.SaveAsPrefabAssetAndConnect(settingsPanel.gameObject, SettingsPrefabPath, InteractionMode.AutomatedAction);
            Debug.Log("[MenuBuilder] Префаб настроек сохранён: " + SettingsPrefabPath);

            // --- Затемнение при загрузке ---
            var fade = NewRect("FadeOverlay", canvas.transform);
            Stretch(fade);
            var fadeImage = AddImage(fade, null, new Color(0f, 0f, 0f, 1f), false);
            fadeImage.raycastTarget = true;
            var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
            fadeGroup.alpha = 0f;
            fadeGroup.blocksRaycasts = false;
            fadeGroup.interactable = false;
            fade.SetAsLastSibling();

            // --- Компонент MainMenu и связи ---
            var menuComponent = menu.gameObject.GetComponent<MainMenu>();
            if (menuComponent == null)
                menuComponent = menu.gameObject.AddComponent<MainMenu>();
            menuComponent.newGameButton = newGameButton;
            menuComponent.continueButton = continueButton;
            menuComponent.settingsButton = settingsButton;
            menuComponent.authorsButton = authorsButton;
            menuComponent.exitButton = exitButton;
            menuComponent.continueCloseButton = continueBack;
            menuComponent.authorsCloseButton = authorsBack;
            menuComponent.continuePanel = continuePanel.gameObject;
            menuComponent.continueButtonsContainer = continueContent;
            menuComponent.stageButtonTemplate = stageTemplate.gameObject;
            menuComponent.settingsPanel = settingsPanel.gameObject;
            menuComponent.authorsPanel = authorsPanel.gameObject;
            menuComponent.buttonSprite = buttonSprite;
            menuComponent.newGameScene = "SampleScene";
            menuComponent.levelScenes = new string[] { "SampleScene" };
            menuComponent.maxReachedLevel = 0;
            menuComponent.fadeOverlay = fadeGroup;
            EditorUtility.SetDirty(menuComponent);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[MenuBuilder] Главное меню собрано и сохранено. Готово!");
        }

        // ---------- Оформление сцены ----------

        private static void DecorateScene()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.07f, 0.035f, 0.05f, 1f);
            }

            var lightGO = GameObject.Find("Directional Light");
            if (lightGO != null)
            {
                Light light = lightGO.GetComponent<Light>();
                if (light != null)
                {
                    light.color = new Color(1f, 0.8f, 0.72f, 1f);
                    light.intensity = 1.15f;
                }
            }
        }

        private static Canvas FindOrCreateCanvas()
        {
            var existing = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (existing != null)
            {
                existing.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = existing.GetComponent<CanvasScaler>();
                if (scaler == null)
                    scaler = existing.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                if (existing.GetComponent<GraphicRaycaster>() == null)
                    existing.gameObject.AddComponent<GraphicRaycaster>();
                return existing;
            }

            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var newScaler = go.GetComponent<CanvasScaler>();
            newScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            newScaler.referenceResolution = new Vector2(1920f, 1080f);
            newScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            newScaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
                return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
        }

        // ---------- Ассеты ----------

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static Sprite ImportSprite(string path, bool nineSlice)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                if (nineSlice)
                    importer.spriteBorder = new Vector4(42f, 42f, 42f, 42f);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Font LoadFont()
        {
            try
            {
                var legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacy != null)
                    return legacy;
            }
            catch (Exception) { }
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        // ---------- Аниматоры ----------

        private static AnimatorController CreateButtonController()
        {
            string path = AnimDir + "/MenuButton.controller";
            AssetDatabase.DeleteAsset(path); // пересоздаём начисто, иначе состояния дублируются при повторной сборке
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;

            string[] states = { "Normal", "Highlighted", "Pressed", "Selected", "Disabled" };
            foreach (var stateName in states)
                controller.AddParameter(stateName, AnimatorControllerParameterType.Trigger);

            var created = new System.Collections.Generic.Dictionary<string, AnimatorState>();
            int column = 0;
            foreach (var stateName in states)
            {
                float scale = 1f;
                float alpha = 1f;
                // Базовый оттенок — тот же, что у кнопок в CreateMenuButton; при наведении кнопка подсвечивается
                Color tint = new Color(0.95f, 0.85f, 0.85f, 1f);
                if (stateName == "Highlighted") { scale = 1.12f; tint = new Color(1f, 0.97f, 0.97f, 1f); }
                if (stateName == "Pressed") { scale = 0.90f; tint = new Color(0.80f, 0.70f, 0.70f, 1f); }
                if (stateName == "Disabled") alpha = 0.55f;

                var clip = new AnimationClip();
                AddConstantCurve(clip, "", typeof(RectTransform), "m_LocalScale.x", scale);
                AddConstantCurve(clip, "", typeof(RectTransform), "m_LocalScale.y", scale);
                AddConstantCurve(clip, "", typeof(RectTransform), "m_LocalScale.z", scale);
                AddConstantCurve(clip, "", typeof(Image), "m_Color.r", tint.r);
                AddConstantCurve(clip, "", typeof(Image), "m_Color.g", tint.g);
                AddConstantCurve(clip, "", typeof(Image), "m_Color.b", tint.b);
                AddConstantCurve(clip, "", typeof(Image), "m_Color.a", alpha);
                AssetDatabase.AddObjectToAsset(clip, controller);

                var state = sm.AddState(stateName, new Vector3(220f + column * 260f, 80f, 0f));
                state.motion = clip;
                created[stateName] = state;
                column++;
            }

            sm.defaultState = created["Normal"];
            foreach (var from in states)
            {
                foreach (var to in states)
                {
                    if (from == to) continue;
                    var transition = created[from].AddTransition(created[to]);
                    transition.hasExitTime = false;
                    transition.duration = 0.12f;
                    transition.AddCondition(AnimatorConditionMode.If, 0f, to);
                }
            }
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorController CreatePanelController()
        {
            string path = AnimDir + "/MenuPanel.controller";
            AssetDatabase.DeleteAsset(path); // пересоздаём начисто при каждой сборке
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;
            controller.AddParameter("Open", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Close", AnimatorControllerParameterType.Trigger);

            var closedClip = new AnimationClip();
            AddConstantCurve(closedClip, "", typeof(CanvasGroup), "m_Alpha", 0f);
            AddConstantCurve(closedClip, "", typeof(RectTransform), "m_LocalScale.x", 0.98f);
            AddConstantCurve(closedClip, "", typeof(RectTransform), "m_LocalScale.y", 0.98f);
            AssetDatabase.AddObjectToAsset(closedClip, controller);

            var openClip = new AnimationClip();
            AddConstantCurve(openClip, "", typeof(CanvasGroup), "m_Alpha", 1f);
            AddConstantCurve(openClip, "", typeof(RectTransform), "m_LocalScale.x", 1f);
            AddConstantCurve(openClip, "", typeof(RectTransform), "m_LocalScale.y", 1f);
            AssetDatabase.AddObjectToAsset(openClip, controller);

            var closed = sm.AddState("Closed", new Vector3(240f, 80f, 0f));
            closed.motion = closedClip;
            var open = sm.AddState("Open", new Vector3(560f, 80f, 0f));
            open.motion = openClip;
            sm.defaultState = closed;

            var toOpen = closed.AddTransition(open);
            toOpen.hasExitTime = false;
            toOpen.duration = 0.18f;
            toOpen.AddCondition(AnimatorConditionMode.If, 0f, "Open");

            var toClosed = open.AddTransition(closed);
            toClosed.hasExitTime = false;
            toClosed.duration = 0.18f;
            toClosed.AddCondition(AnimatorConditionMode.If, 0f, "Close");

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void AddConstantCurve(AnimationClip clip, string path, Type type, string property, float value)
        {
            clip.SetCurve(path, type, property, AnimationCurve.Constant(0f, 1f, value));
        }

        // ---------- UI-хелперы ----------

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static RectTransform Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color, bool sliced)
        {
            var image = rect.gameObject.AddComponent<Image>();
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            }
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Text AddText(RectTransform rect, string text, int size, Color color, TextAnchor anchor)
        {
            var label = rect.gameObject.AddComponent<Text>();
            label.text = text;
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var shadow = rect.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return label;
        }

        private static Button CreateMenuButton(RectTransform parent, string name, string label, Sprite sprite, AnimatorController controller, bool inLayout)
        {
            var rect = NewRect(name, parent);
            if (inLayout)
            {
                var element = rect.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 96f;
                element.minHeight = 96f;
            }
            var image = AddImage(rect, sprite, new Color(0.95f, 0.85f, 0.85f, 1f), true);
            image.raycastTarget = true;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.Animation;
            var animator = rect.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            var textRect = NewRect("Text", rect);
            Stretch(textRect);
            AddText(textRect, label, 42, new Color(0.94f, 0.89f, 0.87f, 1f), TextAnchor.MiddleCenter);
            return button;
        }

        private static RectTransform CreatePanel(string name, Transform canvas, string title, AnimatorController controller)
        {
            var root = NewRect(name, canvas);
            SetAnchors(root, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f), Vector2.zero, Vector2.zero);
            var image = AddImage(root, frameSprite, new Color(0.10f, 0.055f, 0.065f, 0.995f), true);
            image.raycastTarget = true;
            // Панель стартует закрытой и НЕ перехватывает мышь, пока не открыта:
            // иначе невидимая панель блокирует наведение и клики по кнопкам меню
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var animator = root.gameObject.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;

            var titleRect = NewRect("Title", root);
            SetAnchors(titleRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-420f, -160f), new Vector2(420f, -30f));
            AddText(titleRect, title, 64, new Color(0.85f, 0.18f, 0.18f, 1f), TextAnchor.MiddleCenter);
            return root;
        }

        private static Slider AddSlider(RectTransform rect)
        {
            var bgRect = NewRect("Background", rect);
            Stretch(bgRect);
            var bgImage = AddImage(bgRect, null, new Color(0.16f, 0.12f, 0.13f, 1f), false);
            bgImage.raycastTarget = true;

            var fillArea = NewRect("Fill Area", rect);
            SetAnchors(fillArea, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(8f, -4f), new Vector2(-8f, 4f));
            var fillRect = NewRect("Fill", fillArea);
            Stretch(fillRect);
            AddImage(fillRect, null, new Color(0.55f, 0.12f, 0.14f, 1f), false);

            var handleArea = NewRect("Handle Slide Area", rect);
            SetAnchors(handleArea, new Vector2(0f, 0f), new Vector2(1f, 1f),
                new Vector2(-14f, 0f), new Vector2(14f, 0f));
            var handleRect = NewRect("Handle", handleArea);
            SetAnchors(handleRect, new Vector2(0f, 0f), new Vector2(0f, 1f),
                new Vector2(-14f, 0f), new Vector2(14f, 0f));
            var handleImage = AddImage(handleRect, null, new Color(0.85f, 0.25f, 0.25f, 1f), false);
            handleImage.raycastTarget = true;

            var slider = rect.gameObject.AddComponent<Slider>();
            slider.targetGraphic = handleImage;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        private static Toggle AddToggle(RectTransform rect)
        {
            var bgRect = NewRect("Background", rect);
            SetAnchors(bgRect, new Vector2(0f, 0.15f), new Vector2(0.45f, 0.85f), Vector2.zero, Vector2.zero);
            var bgImage = AddImage(bgRect, null, new Color(0.16f, 0.12f, 0.13f, 1f), false);
            bgImage.raycastTarget = true;

            var checkRect = NewRect("Checkmark", bgRect);
            Stretch(checkRect);
            var checkImage = AddImage(checkRect, null, new Color(0.85f, 0.2f, 0.2f, 1f), false);

            var toggle = rect.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            return toggle;
        }
    }
}
