/// <summary>
/// Хранилище прогресса. Реализация решает, где лежат данные: PlayerPrefs, файл или память.
/// </summary>
public interface IGoSaveStore
{
    GoSkillSave Load();

    void Save(GoSkillSave save);

    void Clear();
}
