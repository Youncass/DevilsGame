using System;
using UnityEngine;

/// <summary>
/// Прогресс в PlayerPrefs под одним префиксом. Единственное место в проекте,
/// которое знает имена ключей; они совпадают с прежними, поэтому старые сохранения читаются.
/// </summary>
public sealed class PlayerPrefsGoSaveStore : IGoSaveStore
{
    public const string DefaultKeyPrefix = "Go.Skill";

    private const string KeyLevel = "Level";
    private const string KeyFloor = "Floor";
    private const string KeyGames = "RankedGames";
    private const string KeyHistory = "History";
    private const string KeyTutorial = "TutorialDone";

    private readonly string _keyLevel;
    private readonly string _keyFloor;
    private readonly string _keyGames;
    private readonly string _keyHistory;
    private readonly string _keyTutorial;

    public PlayerPrefsGoSaveStore(string keyPrefix = DefaultKeyPrefix)
    {
        if (string.IsNullOrEmpty(keyPrefix)) keyPrefix = DefaultKeyPrefix;
        if (!keyPrefix.EndsWith(".", StringComparison.Ordinal)) keyPrefix += ".";

        _keyLevel = keyPrefix + KeyLevel;
        _keyFloor = keyPrefix + KeyFloor;
        _keyGames = keyPrefix + KeyGames;
        _keyHistory = keyPrefix + KeyHistory;
        _keyTutorial = keyPrefix + KeyTutorial;
    }

    public GoSkillSave Load()
    {
        return new GoSkillSave(
            PlayerPrefs.GetInt(_keyLevel, 1),
            PlayerPrefs.GetInt(_keyFloor, 0),
            Math.Max(0, PlayerPrefs.GetInt(_keyGames, 0)),
            PlayerPrefs.GetInt(_keyTutorial, 0) == 1,
            PlayerPrefs.GetString(_keyHistory, string.Empty));
    }

    public void Save(GoSkillSave save)
    {
        PlayerPrefs.SetInt(_keyLevel, save.Level);
        PlayerPrefs.SetInt(_keyFloor, save.HighestDefeatedLevel);
        PlayerPrefs.SetInt(_keyGames, save.RankedGames);
        PlayerPrefs.SetInt(_keyTutorial, save.TutorialCompleted ? 1 : 0);
        PlayerPrefs.SetString(_keyHistory, save.History);
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        PlayerPrefs.DeleteKey(_keyLevel);
        PlayerPrefs.DeleteKey(_keyFloor);
        PlayerPrefs.DeleteKey(_keyGames);
        PlayerPrefs.DeleteKey(_keyHistory);
        PlayerPrefs.DeleteKey(_keyTutorial);
        PlayerPrefs.Save();
    }
}
