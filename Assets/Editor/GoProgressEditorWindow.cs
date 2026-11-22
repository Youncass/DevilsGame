using UnityEditor;
using UnityEngine;

/// <summary>
/// Правка сохранённого прогресса (PlayerPrefs) до запуска Play Mode.
/// Меню: Go > Progress Editor.
/// </summary>
public sealed class GoProgressEditorWindow : EditorWindow
{
    private static readonly GoDifficultyProfile Difficulty = new();

    private int _level = 1;
    private int _highestDefeatedLevel;
    private int _rankedGames;
    private bool _tutorialCompleted;
    private string _history = string.Empty;

    [MenuItem("Go/Progress Editor")]
    private static void Open()
    {
        var window = GetWindow<GoProgressEditorWindow>("Go Progress");
        window.minSize = new Vector2(380f, 260f);
        window.Reload();
        window.Show();
    }

    private void OnEnable() => Reload();

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            $"PlayerPrefs: {PlayerPrefsGoSaveStore.DefaultKeyPrefix}.*", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        _tutorialCompleted = EditorGUILayout.Toggle("Обучение пройдено", _tutorialCompleted);
        _level = EditorGUILayout.IntSlider("Уровень", _level, 1, GoSkillTracker.MaxLevel);
        _highestDefeatedLevel = EditorGUILayout.IntSlider(
            "Пол (побеждённый уровень)", _highestDefeatedLevel, 0, Mathf.Max(1, _level));
        _rankedGames = Mathf.Max(0, EditorGUILayout.IntField("Партий в рейтинге", _rankedGames));
        _history = EditorGUILayout.TextField(
            new GUIContent("История W/L", "1 — победа, 0 — поражение; от старой к новой, учитываются последние 3"),
            _history);

        EditorGUILayout.Space();
        int difficultyLevel = _tutorialCompleted ? _level : 0;
        int completedGames = _tutorialCompleted ? _rankedGames + 1 : 0;
        EditorGUILayout.HelpBox(
            $"Что получит игра: подсказки {(_tutorialCompleted ? "выключены" : "включены")}, " +
            $"завершено партий {completedGames}, " +
            $"симуляций бота {Difficulty.GetSimulations(difficultyLevel)}, " +
            $"шанс слабого хода {Difficulty.GetBlunderChance(difficultyLevel):P0}",
            MessageType.None);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Сохранить")) Save();
            if (GUILayout.Button("Перечитать")) Reload();
            if (GUILayout.Button("Сбросить прогресс")) ResetProgress();
        }

        EditorGUILayout.HelpBox(
            "Изменения подхватываются при следующем запуске Play Mode (или после перезагрузки сцены).",
            MessageType.Info);
    }

    private void Reload()
    {
        GoSkillSave save = new PlayerPrefsGoSaveStore().Load();

        _level = Mathf.Clamp(save.Level, 1, GoSkillTracker.MaxLevel);
        _highestDefeatedLevel = Mathf.Clamp(save.HighestDefeatedLevel, 0, _level);
        _rankedGames = Mathf.Max(0, save.RankedGames);
        _tutorialCompleted = save.TutorialCompleted;
        _history = save.History;

        Repaint();
    }

    private void Save()
    {
        int level = Mathf.Clamp(_level, 1, GoSkillTracker.MaxLevel);
        int highestDefeated = Mathf.Clamp(_highestDefeatedLevel, 0, level);

        new PlayerPrefsGoSaveStore().Save(new GoSkillSave(
            level,
            highestDefeated,
            Mathf.Max(0, _rankedGames),
            _tutorialCompleted,
            SanitizeHistory(_history)));

        Reload();
        ShowNotification(new GUIContent("Прогресс сохранён"));
    }

    private void ResetProgress()
    {
        if (!EditorUtility.DisplayDialog(
                "Сброс прогресса",
                "Удалить сохранённый прогресс из PlayerPrefs?",
                "Сбросить",
                "Отмена"))
            return;

        new PlayerPrefsGoSaveStore().Clear();
        Reload();
    }

    /// <summary>Оставляет только 0/1 и не больше HistoryWindow последних результатов.</summary>
    private static string SanitizeHistory(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var result = new System.Text.StringBuilder(GoSkillTracker.HistoryWindow);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c != '0' && c != '1') continue;

            result.Append(c);
            if (result.Length > GoSkillTracker.HistoryWindow)
                result.Remove(0, result.Length - GoSkillTracker.HistoryWindow);
        }

        return result.ToString();
    }
}
