using System.Collections.ObjectModel;
using Dragonfly44.Application.Common.Enums;
using Dragonfly44.Application.Common.Models;
using Dragonfly44.Application.Interfaces;
using Dragonfly44.Application.Services;
using Dragonfly44.Application.Synchronization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;

namespace Dragonfly44.Presentation.ViewModels;

public enum MainViewSection
{
    Projects,
    ProjectDetail,
    ChapterDetail,
    MangaCanvas,
    Drafts,
    DraftDetail
}

public class MainViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScanEngineRegistry _scanEngineRegistry;
    private readonly IWindowStateService _windowStateService;
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IKeyboardShortcutService _shortcutService;
    private readonly IWorkspaceSyncCoordinator _syncCoordinator;
    private readonly ILogger<MainViewModel> _logger;

    // Phase 1 properties
    private string _appTitle = "Dragonfly 44 — Projects & Chapter Ingestion";
    private string _statusMessage = "Initializing foundation services...";
    private string _databaseStatus = "Checking...";
    private string _databasePath = string.Empty;
    private string _diStatus = "Operational";
    private string _loggingStatus = "Active";
    private int _projectCount;
    private bool _isInitialized;

    // Phase 2 properties
    private string _windowBoundsText = "Loading...";
    private string _windowStateText = "Normal";
    private string _windowDpiText = "1.0x (96 DPI)";
    private string _shortcutStatusText = "Operational";
    private string _win2DStatusText = "Active (Direct2D GPU)";
    private string _lastActionMessage = "Ready. Manage projects and import manga chapters below.";
    private int _notificationCounter = 0;

    // Phase 3 properties & state
    private MainViewSection _currentSection = MainViewSection.Projects;
    private ProjectItemViewModel? _selectedProject;
    private ChapterItemViewModel? _selectedChapter;
    private DraftItemViewModel? _selectedDraft;

    private string _newProjectName = string.Empty;
    private string _newProjectDesc = string.Empty;
    private bool _isCreatingProject;

    private string _newChapterTitle = string.Empty;
    private string _newChapterNumberText = string.Empty;
    private bool _isCreatingChapter;

    private string _newDraftName = string.Empty;
    private string _newDraftDesc = string.Empty;
    private bool _isCreatingDraft;

    private string _ingestFolderPath = string.Empty;
    private bool _isIngesting;
    private double _ingestProgressPercent;
    private string _ingestStatusText = string.Empty;
    private CancellationTokenSource? _ingestCts;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();
    public ObservableCollection<ChapterItemViewModel> Chapters { get; } = new();
    public ObservableCollection<PageItemViewModel> Pages { get; } = new();
    public ObservableCollection<DraftItemViewModel> Drafts { get; } = new();
    public ObservableCollection<DraftPageItemViewModel> DraftPages { get; } = new();
    public MangaCanvasViewModel CanvasViewModel { get; }
    public DialogueFeedViewModel DialogueFeedViewModel { get; }
    public AiSettingsViewModel AiSettingsViewModel { get; }
    private bool _isDialogueFeedVisible = true;
    private bool _isAiSettingsOpen;

    public bool IsAiSettingsOpen
    {
        get => _isAiSettingsOpen;
        set
        {
            if (SetProperty(ref _isAiSettingsOpen, value))
            {
                OnPropertyChanged(nameof(AiSettingsVisibility));
            }
        }
    }

    public Visibility AiSettingsVisibility => IsAiSettingsOpen ? Visibility.Visible : Visibility.Collapsed;

    public void OpenAiSettings()
    {
        _ = AiSettingsViewModel.InitializeAsync();
        IsAiSettingsOpen = true;
    }

    public void CloseAiSettings()
    {
        IsAiSettingsOpen = false;
    }

    public bool IsDialogueFeedVisible
    {
        get => _isDialogueFeedVisible;
        set
        {
            if (SetProperty(ref _isDialogueFeedVisible, value))
            {
                OnPropertyChanged(nameof(DialogueFeedVisibility));
                OnPropertyChanged(nameof(DialogueFeedToggleText));
            }
        }
    }

    public Visibility DialogueFeedVisibility => IsDialogueFeedVisible ? Visibility.Visible : Visibility.Collapsed;
    public string DialogueFeedToggleText => IsDialogueFeedVisible ? "Hide Feed" : "Show Feed";

    public void ToggleDialogueFeed()
    {
        IsDialogueFeedVisible = !IsDialogueFeedVisible;
    }

    public MainViewSection CurrentSection
    {
        get => _currentSection;
        set
        {
            if (SetProperty(ref _currentSection, value))
            {
                OnPropertyChanged(nameof(IsProjectsSection));
                OnPropertyChanged(nameof(IsProjectDetailSection));
                OnPropertyChanged(nameof(IsChapterDetailSection));
                OnPropertyChanged(nameof(IsMangaCanvasSection));
                OnPropertyChanged(nameof(IsDraftsSection));
                OnPropertyChanged(nameof(IsDraftDetailSection));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(BreadcrumbText));

                OnPropertyChanged(nameof(ProjectsSectionVisibility));
                OnPropertyChanged(nameof(ProjectDetailSectionVisibility));
                OnPropertyChanged(nameof(ChapterDetailSectionVisibility));
                OnPropertyChanged(nameof(MangaCanvasSectionVisibility));
                OnPropertyChanged(nameof(NormalSectionsVisibility));
                OnPropertyChanged(nameof(DraftsSectionVisibility));
                OnPropertyChanged(nameof(DraftDetailSectionVisibility));
                OnPropertyChanged(nameof(CanGoBackVisibility));
            }
        }
    }

    public bool IsProjectsSection => CurrentSection == MainViewSection.Projects;
    public bool IsProjectDetailSection => CurrentSection == MainViewSection.ProjectDetail;
    public bool IsChapterDetailSection => CurrentSection == MainViewSection.ChapterDetail;
    public bool IsMangaCanvasSection => CurrentSection == MainViewSection.MangaCanvas;
    public bool IsDraftsSection => CurrentSection == MainViewSection.Drafts;
    public bool IsDraftDetailSection => CurrentSection == MainViewSection.DraftDetail;
    public bool CanGoBack => CurrentSection != MainViewSection.Projects && CurrentSection != MainViewSection.Drafts;

    public Visibility ProjectsSectionVisibility => IsProjectsSection ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProjectDetailSectionVisibility => IsProjectDetailSection ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ChapterDetailSectionVisibility => IsChapterDetailSection ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MangaCanvasSectionVisibility => IsMangaCanvasSection ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NormalSectionsVisibility => !IsMangaCanvasSection ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DraftsSectionVisibility => IsDraftsSection ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DraftDetailSectionVisibility => IsDraftDetailSection ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CanGoBackVisibility => CanGoBack ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CreatingProjectVisibility => IsCreatingProject ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CreatingChapterVisibility => IsCreatingChapter ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CreatingDraftVisibility => IsCreatingDraft ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IngestingVisibility => IsIngesting ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ProjectsEmptyVisibility => Projects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ChaptersEmptyVisibility => Chapters.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PagesEmptyVisibility => (Pages.Count == 0 && !IsIngesting) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DraftsEmptyVisibility => Drafts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DraftPagesEmptyVisibility => (DraftPages.Count == 0 && !IsIngesting) ? Visibility.Visible : Visibility.Collapsed;

    public string BreadcrumbText => CurrentSection switch
    {
        MainViewSection.Projects => "Projects",
        MainViewSection.ProjectDetail => $"Projects > {SelectedProject?.Name ?? "Project"}",
        MainViewSection.ChapterDetail => $"Projects > {SelectedProject?.Name ?? "Project"} > {SelectedChapter?.FormattedTitle ?? "Chapter"}",
        MainViewSection.MangaCanvas => SelectedChapter != null
            ? $"Projects > {SelectedProject?.Name ?? "Project"} > {SelectedChapter.FormattedTitle} > Canvas"
            : $"Drafts > {SelectedDraft?.Name ?? "Draft"} > Canvas",
        MainViewSection.Drafts => "Independent Drafts",
        MainViewSection.DraftDetail => $"Drafts > {SelectedDraft?.Name ?? "Draft"}",
        _ => "Dragonfly 44"
    };

    public ProjectItemViewModel? SelectedProject
    {
        get => _selectedProject;
        set => SetProperty(ref _selectedProject, value);
    }

    public ChapterItemViewModel? SelectedChapter
    {
        get => _selectedChapter;
        set => SetProperty(ref _selectedChapter, value);
    }

    public DraftItemViewModel? SelectedDraft
    {
        get => _selectedDraft;
        set => SetProperty(ref _selectedDraft, value);
    }

    public string NewProjectName
    {
        get => _newProjectName;
        set => SetProperty(ref _newProjectName, value);
    }

    public string NewProjectDesc
    {
        get => _newProjectDesc;
        set => SetProperty(ref _newProjectDesc, value);
    }

    public bool IsCreatingProject
    {
        get => _isCreatingProject;
        set
        {
            if (SetProperty(ref _isCreatingProject, value))
            {
                OnPropertyChanged(nameof(CreatingProjectVisibility));
            }
        }
    }

    public string NewChapterTitle
    {
        get => _newChapterTitle;
        set => SetProperty(ref _newChapterTitle, value);
    }

    public string NewChapterNumberText
    {
        get => _newChapterNumberText;
        set => SetProperty(ref _newChapterNumberText, value);
    }

    public bool IsCreatingChapter
    {
        get => _isCreatingChapter;
        set
        {
            if (SetProperty(ref _isCreatingChapter, value))
            {
                OnPropertyChanged(nameof(CreatingChapterVisibility));
            }
        }
    }

    public string NewDraftName
    {
        get => _newDraftName;
        set => SetProperty(ref _newDraftName, value);
    }

    public string NewDraftDesc
    {
        get => _newDraftDesc;
        set => SetProperty(ref _newDraftDesc, value);
    }

    public bool IsCreatingDraft
    {
        get => _isCreatingDraft;
        set
        {
            if (SetProperty(ref _isCreatingDraft, value))
            {
                OnPropertyChanged(nameof(CreatingDraftVisibility));
            }
        }
    }

    public string IngestFolderPath
    {
        get => _ingestFolderPath;
        set => SetProperty(ref _ingestFolderPath, value);
    }

    public bool IsIngesting
    {
        get => _isIngesting;
        set
        {
            if (SetProperty(ref _isIngesting, value))
            {
                OnPropertyChanged(nameof(IngestingVisibility));
                OnPropertyChanged(nameof(PagesEmptyVisibility));
                OnPropertyChanged(nameof(DraftPagesEmptyVisibility));
            }
        }
    }

    public double IngestProgressPercent
    {
        get => _ingestProgressPercent;
        set => SetProperty(ref _ingestProgressPercent, value);
    }

    public string IngestStatusText
    {
        get => _ingestStatusText;
        set => SetProperty(ref _ingestStatusText, value);
    }

    public string AppTitle
    {
        get => _appTitle;
        set => SetProperty(ref _appTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string DatabaseStatus
    {
        get => _databaseStatus;
        set => SetProperty(ref _databaseStatus, value);
    }

    public string DatabasePath
    {
        get => _databasePath;
        set => SetProperty(ref _databasePath, value);
    }

    public string DiStatus
    {
        get => _diStatus;
        set => SetProperty(ref _diStatus, value);
    }

    public string LoggingStatus
    {
        get => _loggingStatus;
        set => SetProperty(ref _loggingStatus, value);
    }

    public int ProjectCount
    {
        get => _projectCount;
        set => SetProperty(ref _projectCount, value);
    }

    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetProperty(ref _isInitialized, value);
    }

    public string WindowBoundsText
    {
        get => _windowBoundsText;
        set => SetProperty(ref _windowBoundsText, value);
    }

    public string WindowStateText
    {
        get => _windowStateText;
        set => SetProperty(ref _windowStateText, value);
    }

    public string WindowDpiText
    {
        get => _windowDpiText;
        set => SetProperty(ref _windowDpiText, value);
    }

    public string ShortcutStatusText
    {
        get => _shortcutStatusText;
        set => SetProperty(ref _shortcutStatusText, value);
    }

    public string Win2DStatusText
    {
        get => _win2DStatusText;
        set => SetProperty(ref _win2DStatusText, value);
    }

    public string LastActionMessage
    {
        get => _lastActionMessage;
        set => SetProperty(ref _lastActionMessage, value);
    }

    public MainViewModel(
        IServiceScopeFactory scopeFactory,
        IScanEngineRegistry scanEngineRegistry,
        IWindowStateService windowStateService,
        INotificationService notificationService,
        ISettingsService settingsService,
        IDialogService dialogService,
        IKeyboardShortcutService shortcutService,
        IStorageService storageService,
        DialogueFeedViewModel dialogueFeedViewModel,
        AiSettingsViewModel aiSettingsViewModel,
        IWorkspaceSyncCoordinator syncCoordinator,
        ILogger<MainViewModel> logger)
    {
        _scopeFactory = scopeFactory;
        _scanEngineRegistry = scanEngineRegistry;
        _windowStateService = windowStateService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _dialogService = dialogService;
        _shortcutService = shortcutService;
        _syncCoordinator = syncCoordinator;
        _logger = logger;

        CanvasViewModel = new MangaCanvasViewModel(storageService, _syncCoordinator);
        DialogueFeedViewModel = dialogueFeedViewModel;
        DialogueFeedViewModel.AttachSyncCoordinator(_syncCoordinator);
        AiSettingsViewModel = aiSettingsViewModel;

        // Dynamic overlay lifecycle events (Requirement H)
        DialogueFeedViewModel.DialogueCreated += (s, bubble) =>
        {
            var pageNum = DialogueFeedViewModel.PageGroups.FirstOrDefault(g => g.PageId == bubble.PageId)?.PageNumber ?? 1;
            CanvasViewModel.AddOverlay(bubble, pageNum);
        };

        DialogueFeedViewModel.DialogueDeleted += (s, bubbleId) =>
        {
            CanvasViewModel.RemoveOverlay(bubbleId);
        };

        DialogueFeedViewModel.DialoguesReordered += (s, args) =>
        {
            CanvasViewModel.UpdateOverlaysReadingOrder(args.PageId, args.OrderedIds);
        };

        _windowStateService.BoundsChanged += (s, b) =>
        {
            WindowBoundsText = $"{b.Width} x {b.Height} at ({b.X}, {b.Y})";
        };

        _windowStateService.StateChanged += (s, st) =>
        {
            WindowStateText = st.ToString();
        };

        _windowStateService.DpiChanged += (s, dpi) =>
        {
            WindowDpiText = $"{dpi:F2}x ({dpi * 96:F0} DPI)";
        };

        Projects.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ProjectsEmptyVisibility));
        Chapters.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ChaptersEmptyVisibility));
        Pages.CollectionChanged += (s, e) => OnPropertyChanged(nameof(PagesEmptyVisibility));
        Drafts.CollectionChanged += (s, e) => OnPropertyChanged(nameof(DraftsEmptyVisibility));
        DraftPages.CollectionChanged += (s, e) => OnPropertyChanged(nameof(DraftPagesEmptyVisibility));
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MainViewModel initialization started.");
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                DatabasePath = dbInitializer.GetDatabasePath();
                var canConnect = await dbInitializer.CanConnectAsync(cancellationToken);
                DatabaseStatus = canConnect ? "Connected (SQLite WAL Mode)" : "Connection Failed";
            }

            await LoadProjectsAsync(cancellationToken);
            await LoadDraftsAsync(cancellationToken);

            var b = _windowStateService.CurrentBounds;
            WindowBoundsText = $"{b.Width} x {b.Height} at ({b.X}, {b.Y})";
            WindowStateText = _windowStateService.CurrentState.ToString();
            WindowDpiText = $"{_windowStateService.CurrentDpiScale:F2}x ({_windowStateService.CurrentDpiScale * 96:F0} DPI)";
            ShortcutStatusText = $"{_shortcutService.RegisteredShortcuts.Count} active shortcut(s)";

            StatusMessage = "Dragonfly 44 Phase 3 operational. Projects, chapters, and manga ingestion ready.";
            IsInitialized = true;
            _logger.LogInformation("MainViewModel initialization complete.");
        }
        catch (Exception ex)
        {
            DatabaseStatus = $"Error: {ex.Message}";
            StatusMessage = "Foundation initialization encountered an error.";
            _logger.LogError(ex, "Error initializing MainViewModel");
        }
    }

    // -------------------------------------------------------------
    // NAVIGATION METHODS
    // -------------------------------------------------------------

    public void NavigateToProjects()
    {
        CurrentSection = MainViewSection.Projects;
        SelectedProject = null;
        SelectedChapter = null;
    }

    public void NavigateToDrafts()
    {
        CurrentSection = MainViewSection.Drafts;
        SelectedDraft = null;
    }

    public void NavigateBack()
    {
        switch (CurrentSection)
        {
            case MainViewSection.MangaCanvas:
                _syncCoordinator.Reset();
                CanvasViewModel.Clear();
                DialogueFeedViewModel.Clear();
                if (SelectedChapter != null)
                {
                    CurrentSection = MainViewSection.ChapterDetail;
                }
                else if (SelectedDraft != null)
                {
                    CurrentSection = MainViewSection.DraftDetail;
                }
                else
                {
                    NavigateToProjects();
                }
                break;
            case MainViewSection.ChapterDetail:
                CurrentSection = MainViewSection.ProjectDetail;
                SelectedChapter = null;
                Pages.Clear();
                break;
            case MainViewSection.ProjectDetail:
                CurrentSection = MainViewSection.Projects;
                SelectedProject = null;
                Chapters.Clear();
                break;
            case MainViewSection.DraftDetail:
                CurrentSection = MainViewSection.Drafts;
                SelectedDraft = null;
                DraftPages.Clear();
                break;
        }
    }

    public async Task OpenChapterCanvasAsync(int initialPageIndex = 0)
    {
        if (SelectedChapter == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var pageRepo = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            var pages = await pageRepo.GetByChapterIdAsync(SelectedChapter.Id);

            if (pages.Count == 0)
            {
                _notificationService.Show("This chapter has no imported pages to display.", "Empty Chapter", NotificationSeverity.Warning);
                return;
            }

            _syncCoordinator.SetChapter(SelectedChapter.Id);
            CanvasViewModel.LoadPages(pages, SelectedChapter.Title, initialPageIndex);
            await DialogueFeedViewModel.LoadChapterDialoguesAsync(SelectedChapter.Id, pages);
            _syncCoordinator.SetActivePage(initialPageIndex + 1, SelectionOrigin.External);
            CurrentSection = MainViewSection.MangaCanvas;
            LastActionMessage = $"Opened canvas for '{SelectedChapter.Title}' ({pages.Count} pages)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open manga canvas for chapter {ChapterId}", SelectedChapter.Id);
            _notificationService.Show("Failed to open canvas: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task OpenDraftCanvasAsync(int initialPageIndex = 0)
    {
        if (SelectedDraft == null) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var draftRepo = scope.ServiceProvider.GetRequiredService<IDraftRepository>();
            var draft = await draftRepo.GetByIdAsync(SelectedDraft.Id);

            if (draft == null || draft.Pages.Count == 0)
            {
                _notificationService.Show("This draft has no imported pages to display.", "Empty Draft", NotificationSeverity.Warning);
                return;
            }

            _syncCoordinator.SetChapter(null);
            CanvasViewModel.LoadDraftPages(draft.Pages, SelectedDraft.Name, initialPageIndex);
            DialogueFeedViewModel.Clear();
            _syncCoordinator.SetActivePage(initialPageIndex + 1, SelectionOrigin.External);
            CurrentSection = MainViewSection.MangaCanvas;
            LastActionMessage = $"Opened canvas for draft '{SelectedDraft.Name}' ({draft.Pages.Count} pages)";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open manga canvas for draft {DraftId}", SelectedDraft.Id);
            _notificationService.Show("Failed to open canvas: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    // -------------------------------------------------------------
    // PROJECT CRUD
    // -------------------------------------------------------------

    public async Task LoadProjectsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
            var projects = await projectService.GetAllProjectsAsync(cancellationToken);

            Projects.Clear();
            foreach (var p in projects)
            {
                Projects.Add(new ProjectItemViewModel(p));
            }
            ProjectCount = Projects.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load projects");
            _notificationService.Show("Failed to load projects: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            _notificationService.Show("Please enter a valid project name.", "Validation", NotificationSeverity.Warning);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
            var result = await projectService.CreateProjectAsync(NewProjectName.Trim(), description: NewProjectDesc?.Trim());

            if (result.IsSuccess && result.Value != null)
            {
                var vm = new ProjectItemViewModel(result.Value);
                Projects.Insert(0, vm);
                ProjectCount = Projects.Count;
                NewProjectName = string.Empty;
                NewProjectDesc = string.Empty;
                IsCreatingProject = false;
                LastActionMessage = $"Created project '{vm.Name}'";
                _notificationService.Show($"Project '{vm.Name}' created successfully.", "Success", NotificationSeverity.Success);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to create project", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            _notificationService.Show("Error creating project: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task OpenProjectAsync(ProjectItemViewModel vm)
    {
        SelectedProject = vm;
        CurrentSection = MainViewSection.ProjectDetail;
        await LoadChaptersAsync(vm.Id);
    }

    public async Task RenameProjectAsync(ProjectItemViewModel vm, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
            var result = await projectService.RenameProjectAsync(vm.Id, newName.Trim());

            if (result.IsSuccess && result.Value != null)
            {
                vm.Name = result.Value.Name;
                vm.UpdatedAtUtc = result.Value.UpdatedAtUtc;
                LastActionMessage = $"Renamed project to '{vm.Name}'";
                _notificationService.Show($"Project renamed to '{vm.Name}'", "Success", NotificationSeverity.Information);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to rename project", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming project");
            _notificationService.Show("Error renaming project: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task DeleteProjectAsync(ProjectItemViewModel vm)
    {
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Project",
            $"Are you sure you want to permanently delete project '{vm.Name}'? All chapters, imported pages, and stored images will be removed.",
            "Delete Project",
            "Cancel",
            isDestructive: true);

        if (confirm != DialogResult.Primary) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
            var result = await projectService.DeleteProjectAsync(vm.Id);

            if (result.IsSuccess)
            {
                Projects.Remove(vm);
                ProjectCount = Projects.Count;
                if (SelectedProject == vm)
                {
                    NavigateToProjects();
                }
                LastActionMessage = $"Deleted project '{vm.Name}'";
                _notificationService.Show($"Project '{vm.Name}' deleted.", "Deleted", NotificationSeverity.Information);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to delete project", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project");
            _notificationService.Show("Error deleting project: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    // -------------------------------------------------------------
    // CHAPTER CRUD
    // -------------------------------------------------------------

    public async Task LoadChaptersAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var chapterService = scope.ServiceProvider.GetRequiredService<IChapterService>();
            var chapters = await chapterService.GetChaptersByProjectAsync(projectId, cancellationToken);

            Chapters.Clear();
            foreach (var ch in chapters)
            {
                Chapters.Add(new ChapterItemViewModel(ch));
            }

            if (SelectedProject != null)
            {
                SelectedProject.ChapterCount = Chapters.Count;
                SelectedProject.PageCount = Chapters.Sum(c => c.PageCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chapters for project {ProjectId}", projectId);
            _notificationService.Show("Failed to load chapters: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task CreateChapterAsync()
    {
        if (SelectedProject == null) return;

        if (string.IsNullOrWhiteSpace(NewChapterTitle))
        {
            _notificationService.Show("Please enter a chapter title.", "Validation", NotificationSeverity.Warning);
            return;
        }

        int? chNum = null;
        if (int.TryParse(NewChapterNumberText, out var parsedNum) && parsedNum > 0)
        {
            chNum = parsedNum;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var chapterService = scope.ServiceProvider.GetRequiredService<IChapterService>();
            var result = await chapterService.CreateChapterAsync(SelectedProject.Id, NewChapterTitle.Trim(), chNum);

            if (result.IsSuccess && result.Value != null)
            {
                var vm = new ChapterItemViewModel(result.Value);
                Chapters.Add(vm);
                if (SelectedProject != null)
                {
                    SelectedProject.ChapterCount = Chapters.Count;
                }
                NewChapterTitle = string.Empty;
                NewChapterNumberText = string.Empty;
                IsCreatingChapter = false;
                LastActionMessage = $"Created chapter '{vm.FormattedTitle}'";
                _notificationService.Show($"Chapter '{vm.Title}' created successfully.", "Success", NotificationSeverity.Success);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to create chapter", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chapter");
            _notificationService.Show("Error creating chapter: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task OpenChapterAsync(ChapterItemViewModel vm)
    {
        SelectedChapter = vm;
        CurrentSection = MainViewSection.ChapterDetail;
        await LoadPagesAsync(vm.Id);
    }

    public async Task RenameChapterAsync(ChapterItemViewModel vm, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var chapterService = scope.ServiceProvider.GetRequiredService<IChapterService>();
            var result = await chapterService.RenameChapterAsync(vm.Id, newTitle.Trim());

            if (result.IsSuccess && result.Value != null)
            {
                vm.Title = result.Value.Title;
                vm.UpdatedAtUtc = result.Value.UpdatedAtUtc;
                LastActionMessage = $"Renamed chapter to '{vm.Title}'";
                _notificationService.Show($"Chapter renamed to '{vm.Title}'", "Success", NotificationSeverity.Information);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to rename chapter", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming chapter");
            _notificationService.Show("Error renaming chapter: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task DeleteChapterAsync(ChapterItemViewModel vm)
    {
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Chapter",
            $"Are you sure you want to permanently delete '{vm.FormattedTitle}'? All imported pages and stored image files will be deleted.",
            "Delete Chapter",
            "Cancel",
            isDestructive: true);

        if (confirm != DialogResult.Primary) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var chapterService = scope.ServiceProvider.GetRequiredService<IChapterService>();
            var result = await chapterService.DeleteChapterAsync(vm.Id);

            if (result.IsSuccess)
            {
                Chapters.Remove(vm);
                if (SelectedProject != null)
                {
                    SelectedProject.ChapterCount = Chapters.Count;
                    SelectedProject.PageCount = Chapters.Sum(c => c.PageCount);
                }
                if (SelectedChapter == vm)
                {
                    NavigateBack();
                }
                LastActionMessage = $"Deleted chapter '{vm.FormattedTitle}'";
                _notificationService.Show($"Chapter '{vm.Title}' deleted.", "Deleted", NotificationSeverity.Information);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to delete chapter", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting chapter");
            _notificationService.Show("Error deleting chapter: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    // -------------------------------------------------------------
    // PAGE INGESTION (CHAPTER)
    // -------------------------------------------------------------

    public async Task LoadPagesAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var pageRepo = scope.ServiceProvider.GetRequiredService<IPageRepository>();
            var pages = await pageRepo.GetByChapterIdAsync(chapterId, cancellationToken);

            Pages.Clear();
            foreach (var p in pages)
            {
                Pages.Add(new PageItemViewModel(p));
            }

            if (SelectedChapter != null)
            {
                SelectedChapter.PageCount = Pages.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pages for chapter {ChapterId}", chapterId);
            _notificationService.Show("Failed to load pages: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task IngestChapterFolderAsync(string? specificPath = null)
    {
        if (SelectedProject == null || SelectedChapter == null) return;

        var targetFolder = specificPath ?? IngestFolderPath;
        if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
        {
            _notificationService.Show("Please provide a valid directory containing manga pages.", "Validation", NotificationSeverity.Warning);
            return;
        }

        IsIngesting = true;
        IngestProgressPercent = 0;
        IngestStatusText = "Scanning directory for supported images...";
        _ingestCts = new CancellationTokenSource();

        var progress = new Progress<IngestionProgress>(report =>
        {
            IngestProgressPercent = report.Percent;
            IngestStatusText = $"Importing {report.ProcessedFiles}/{report.TotalFiles}: {report.CurrentFileName}";
        });

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IMangaIngestionService>();

            var result = await ingestionService.IngestChapterFolderAsync(
                SelectedProject.Id,
                SelectedChapter.Id,
                targetFolder,
                progress,
                _ingestCts.Token);

            if (result.IsSuccess && result.Value != null)
            {
                await LoadPagesAsync(SelectedChapter.Id);
                IngestFolderPath = string.Empty;
                LastActionMessage = $"Successfully imported {result.Value.Count} pages into {SelectedChapter.FormattedTitle}";
                _notificationService.Show($"Imported {result.Value.Count} manga pages into '{SelectedChapter.Title}'.", "Import Complete", NotificationSeverity.Success);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Ingestion failed.", "Import Error", NotificationSeverity.Error);
            }
        }
        catch (OperationCanceledException)
        {
            _notificationService.Show("Import cancelled by user. Files rolled back.", "Cancelled", NotificationSeverity.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chapter folder ingestion");
            _notificationService.Show("Import error: " + ex.Message, "Error", NotificationSeverity.Error);
        }
        finally
        {
            IsIngesting = false;
            IngestProgressPercent = 0;
            IngestStatusText = string.Empty;
            _ingestCts?.Dispose();
            _ingestCts = null;
        }
    }

    public void CancelIngestion()
    {
        if (_ingestCts != null && !_ingestCts.IsCancellationRequested)
        {
            IngestStatusText = "Cancelling import and rolling back files...";
            _ingestCts.Cancel();
        }
    }

    // -------------------------------------------------------------
    // DRAFT CRUD & INGESTION
    // -------------------------------------------------------------

    public async Task LoadDraftsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var draftService = scope.ServiceProvider.GetRequiredService<IDraftService>();
            var drafts = await draftService.GetAllDraftsAsync(cancellationToken);

            Drafts.Clear();
            foreach (var d in drafts)
            {
                Drafts.Add(new DraftItemViewModel(d));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load drafts");
        }
    }

    public async Task CreateDraftAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDraftName))
        {
            _notificationService.Show("Please enter a valid draft name.", "Validation", NotificationSeverity.Warning);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var draftService = scope.ServiceProvider.GetRequiredService<IDraftService>();
            var result = await draftService.CreateDraftAsync(NewDraftName.Trim(), NewDraftDesc?.Trim());

            if (result.IsSuccess && result.Value != null)
            {
                var vm = new DraftItemViewModel(result.Value);
                Drafts.Insert(0, vm);
                NewDraftName = string.Empty;
                NewDraftDesc = string.Empty;
                IsCreatingDraft = false;
                LastActionMessage = $"Created draft '{vm.Name}'";
                _notificationService.Show($"Draft '{vm.Name}' created successfully.", "Success", NotificationSeverity.Success);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to create draft", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating draft");
            _notificationService.Show("Error creating draft: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task OpenDraftAsync(DraftItemViewModel vm)
    {
        SelectedDraft = vm;
        CurrentSection = MainViewSection.DraftDetail;
        await LoadDraftPagesAsync(vm.Id);
    }

    public async Task LoadDraftPagesAsync(Guid draftId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var draftRepo = scope.ServiceProvider.GetRequiredService<IDraftRepository>();
            var draft = await draftRepo.GetByIdAsync(draftId, cancellationToken);

            DraftPages.Clear();
            if (draft != null)
            {
                foreach (var p in draft.Pages)
                {
                    DraftPages.Add(new DraftPageItemViewModel(p));
                }

                if (SelectedDraft != null)
                {
                    SelectedDraft.PageCount = DraftPages.Count;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load draft pages for {DraftId}", draftId);
        }
    }

    public async Task RenameDraftAsync(DraftItemViewModel vm, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName)) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var draftService = scope.ServiceProvider.GetRequiredService<IDraftService>();
            var result = await draftService.RenameDraftAsync(vm.Id, newName.Trim());

            if (result.IsSuccess && result.Value != null)
            {
                vm.Name = result.Value.Name;
                vm.UpdatedAtUtc = result.Value.UpdatedAtUtc;
                LastActionMessage = $"Renamed draft to '{vm.Name}'";
                _notificationService.Show($"Draft renamed to '{vm.Name}'", "Success", NotificationSeverity.Information);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to rename draft", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming draft");
            _notificationService.Show("Error renaming draft: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task DeleteDraftAsync(DraftItemViewModel vm)
    {
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Delete Draft",
            $"Are you sure you want to permanently delete draft '{vm.Name}'? All imported pages and stored image files will be removed.",
            "Delete Draft",
            "Cancel",
            isDestructive: true);

        if (confirm != DialogResult.Primary) return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var draftService = scope.ServiceProvider.GetRequiredService<IDraftService>();
            var result = await draftService.DeleteDraftAsync(vm.Id);

            if (result.IsSuccess)
            {
                Drafts.Remove(vm);
                if (SelectedDraft == vm)
                {
                    NavigateToDrafts();
                }
                LastActionMessage = $"Deleted draft '{vm.Name}'";
                _notificationService.Show($"Draft '{vm.Name}' deleted.", "Deleted", NotificationSeverity.Information);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Failed to delete draft", "Error", NotificationSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting draft");
            _notificationService.Show("Error deleting draft: " + ex.Message, "Error", NotificationSeverity.Error);
        }
    }

    public async Task IngestDraftFolderAsync(string? specificPath = null)
    {
        if (SelectedDraft == null) return;

        var targetFolder = specificPath ?? IngestFolderPath;
        if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder))
        {
            _notificationService.Show("Please provide a valid directory containing manga pages.", "Validation", NotificationSeverity.Warning);
            return;
        }

        IsIngesting = true;
        IngestProgressPercent = 0;
        IngestStatusText = "Scanning directory for supported images...";
        _ingestCts = new CancellationTokenSource();

        var progress = new Progress<IngestionProgress>(report =>
        {
            IngestProgressPercent = report.Percent;
            IngestStatusText = $"Importing {report.ProcessedFiles}/{report.TotalFiles}: {report.CurrentFileName}";
        });

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ingestionService = scope.ServiceProvider.GetRequiredService<IMangaIngestionService>();

            var result = await ingestionService.IngestDraftFolderAsync(
                SelectedDraft.Id,
                targetFolder,
                progress,
                _ingestCts.Token);

            if (result.IsSuccess && result.Value != null)
            {
                await LoadDraftPagesAsync(SelectedDraft.Id);
                IngestFolderPath = string.Empty;
                LastActionMessage = $"Successfully imported {result.Value.Count} pages into draft '{SelectedDraft.Name}'";
                _notificationService.Show($"Imported {result.Value.Count} manga pages into draft '{SelectedDraft.Name}'.", "Import Complete", NotificationSeverity.Success);
            }
            else
            {
                _notificationService.Show(result.Error ?? "Ingestion failed.", "Import Error", NotificationSeverity.Error);
            }
        }
        catch (OperationCanceledException)
        {
            _notificationService.Show("Import cancelled by user. Files rolled back.", "Cancelled", NotificationSeverity.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during draft folder ingestion");
            _notificationService.Show("Import error: " + ex.Message, "Error", NotificationSeverity.Error);
        }
        finally
        {
            IsIngesting = false;
            IngestProgressPercent = 0;
            IngestStatusText = string.Empty;
            _ingestCts?.Dispose();
            _ingestCts = null;
        }
    }

    // -------------------------------------------------------------
    // DRAG-AND-DROP HANDLER
    // -------------------------------------------------------------

    public async Task HandleDroppedPathAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        string folderToImport;
        if (Directory.Exists(path))
        {
            folderToImport = path;
        }
        else if (File.Exists(path))
        {
            folderToImport = Path.GetDirectoryName(path) ?? string.Empty;
        }
        else
        {
            return;
        }

        if (CurrentSection == MainViewSection.ChapterDetail && SelectedChapter != null)
        {
            _notificationService.Show($"Importing folder: {folderToImport} into chapter '{SelectedChapter.Title}'...", "Import Started", NotificationSeverity.Information);
            await IngestChapterFolderAsync(folderToImport);
        }
        else if (CurrentSection == MainViewSection.DraftDetail && SelectedDraft != null)
        {
            _notificationService.Show($"Importing folder: {folderToImport} into draft '{SelectedDraft.Name}'...", "Import Started", NotificationSeverity.Information);
            await IngestDraftFolderAsync(folderToImport);
        }
        else
        {
            IngestFolderPath = folderToImport;
            _notificationService.Show($"Folder selected: {folderToImport}. Open a Chapter or Draft to complete import.", "Folder Selected", NotificationSeverity.Information);
        }
    }

    // -------------------------------------------------------------
    // DIAGNOSTIC VERIFICATION TRIGGERS (PRESERVED FROM PHASE 2)
    // -------------------------------------------------------------

    public void TriggerTestNotification()
    {
        _notificationCounter++;
        _notificationService.Show(
            $"Notification #{_notificationCounter} dispatched via centralized INotificationService.",
            "Technical Notification",
            NotificationSeverity.Information);

        LastActionMessage = $"Dispatched test notification #{_notificationCounter}";
    }

    public async Task TriggerTestDialogAsync()
    {
        LastActionMessage = "Awaiting modal dialog response...";
        var result = await _dialogService.ShowConfirmationAsync(
            "Dialog Service Verification",
            "This confirms the decoupled CustomDialogHost and IDialogService infrastructure is operational.",
            "Confirm",
            "Cancel");

        LastActionMessage = $"Dialog closed with result: {result}";
    }
}
