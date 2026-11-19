using System;
using UnityEngine;

/// <summary>
/// Точка входа партии в сцене: собирает подсистемы, принимает ввод и отдаёт события наружу.
/// Правила и ходы живут в MatchLoop, PlayerTurn, BotTurn и GameProgress.
/// </summary>
public class GameSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Board _board;
    [SerializeField] private PlayerInput _input;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private GoConfig _botConfig;
    [SerializeField] private PieceColor _playerColor = PieceColor.White;

    [Header("Search budgets")]
    [SerializeField, Min(1)] private float _botTimeBudgetMs = 1500f;
    [Tooltip("Сколько миллисекунд поиска можно съесть в одном кадре")]
    [SerializeField, Min(0.25f)] private float _frameBudgetMs = 4f;
    [SerializeField, Min(1)] private int _simsPerFrame = 128;

    [Header("Progression")]
    [SerializeField] private GoDifficultyProfile _difficulty = new();

    [Header("Between games")]
    [SerializeField] private bool _autoStartNewGame = true;
    [SerializeField, Min(0)] private float _newGameDelaySeconds = 2f;

    private MatchLoop _loop;
    private GameProgress _progress;

    public event Action<GoTutorialEvent> TutorialEventRaised;
    public event Action<GoGameResult> GameFinished;
    public event Action<GoSkillSnapshot> SkillChanged;
    public event Action NewGameStarted;

    private void Start()
    {
        GoConfig config = _botConfig;
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<GoConfig>();
            config.name = "Runtime GoConfig";
        }
        _board.Config = config;
        _difficulty ??= new GoDifficultyProfile();

        _progress = new GameProgress(_difficulty);
        _progress.TutorialRaised += OnTutorialEvent;
        _progress.SkillChanged += OnSkillChanged;
        _progress.Load();

        var raycaster = new BoardRaycaster(_mainCamera, _board);
        var settings = new MatchSettings(
            _playerColor,
            _botTimeBudgetMs,
            _frameBudgetMs,
            _simsPerFrame,
            _autoStartNewGame,
            _newGameDelaySeconds);

        _loop = new MatchLoop(
            _board,
            new PlayerTurn(raycaster),
            new BotTurn(_board, config, _progress),
            _progress,
            settings);

        _loop.GameFinished += OnGameFinished;
        _loop.NewGameStarted += OnNewGameStarted;

        _input.OnClick.AddListener(OnClick);
        _input.OnPass.AddListener(OnPass);

        _loop.StartNewGame();
    }

    private void Update()
    {
        _loop?.Tick(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (_input != null)
        {
            _input.OnClick.RemoveListener(OnClick);
            _input.OnPass.RemoveListener(OnPass);
        }

        if (_loop != null)
        {
            _loop.GameFinished -= OnGameFinished;
            _loop.NewGameStarted -= OnNewGameStarted;
        }

        if (_progress != null)
        {
            _progress.TutorialRaised -= OnTutorialEvent;
            _progress.SkillChanged -= OnSkillChanged;
        }
    }

    private void OnClick(Vector2 screenPosition) => _loop?.QueueClick(screenPosition);

    private void OnPass() => _loop?.QueuePass();

    private void OnTutorialEvent(GoTutorialEvent e)
    {
        MatchLog.Tutorial(e);
        TutorialEventRaised?.Invoke(e);
    }

    private void OnSkillChanged(GoSkillSnapshot snapshot) => SkillChanged?.Invoke(snapshot);

    private void OnGameFinished(GoGameResult result) => GameFinished?.Invoke(result);

    private void OnNewGameStarted() => NewGameStarted?.Invoke();
}
