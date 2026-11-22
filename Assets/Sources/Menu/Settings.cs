using UnityEngine;
using UnityEngine.UI;

namespace DevilsGame.Menu
{
    /// <summary>
    /// Панель настроек. Живёт на корне префаба SettingsMenu.
    /// Громкость музыки управляет AudioListener, звуки — пока сохраняются в PlayerPrefs.
    /// </summary>
    public class Settings : MonoBehaviour
    {
        [Header("Элементы управления")]
        public Slider musicSlider;
        public Slider sfxSlider;
        public Toggle fullscreenToggle;
        public Button backButton;

        [Header("Звуковые источники для SFX (необязательно)")]
        public AudioSource[] sfxSources;

        private const string MusicKey = "MusicVolume";
        private const string SfxKey = "SfxVolume";
        private const string FullscreenKey = "Fullscreen";

        private void Awake()
        {
            // Сначала выставляем значения, потом подписываемся, чтобы не сохранять дефолт
            float music = PlayerPrefs.GetFloat(MusicKey, 1f);
            float sfx = PlayerPrefs.GetFloat(SfxKey, 1f);
            bool fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;

            if (musicSlider != null) musicSlider.value = music;
            if (sfxSlider != null) sfxSlider.value = sfx;
            if (fullscreenToggle != null) fullscreenToggle.isOn = fullscreen;

            ApplyMusicVolume(music);
            ApplySfxVolume(sfx);
            ApplyFullscreen(fullscreen);

            if (musicSlider != null) musicSlider.onValueChanged.AddListener(ApplyMusicVolume);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(ApplySfxVolume);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(ApplyFullscreen);
            if (backButton != null) backButton.onClick.AddListener(Back);
        }

        private void ApplyMusicVolume(float value)
        {
            AudioListener.volume = value;
            PlayerPrefs.SetFloat(MusicKey, value);
        }

        private void ApplySfxVolume(float value)
        {
            if (sfxSources != null)
            {
                foreach (AudioSource source in sfxSources)
                {
                    if (source != null)
                        source.volume = value;
                }
            }
            PlayerPrefs.SetFloat(SfxKey, value);
        }

        private void ApplyFullscreen(bool fullscreen)
        {
            if (Screen.fullScreen != fullscreen)
                Screen.fullScreen = fullscreen;
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        }

        private void Back()
        {
            PlayerPrefs.Save();

            MainMenu menu = FindAnyObjectByType<MainMenu>();
            if (menu != null)
                menu.ClosePanel(gameObject);
        }
    }
}
