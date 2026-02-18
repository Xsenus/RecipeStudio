using System;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly RecipeRepository _repo;
    private readonly RecipeExcelService _excel;

    private ViewModelBase _currentPage;

    public MainViewModel(SettingsService settings, RecipeRepository repo, RecipeExcelService excel)
    {
        _settings = settings;
        _repo = repo;
        _excel = excel;

        Dashboard = new DashboardViewModel(_repo, OpenInEditor);
        Editor = new EditorViewModel(_settings, _repo, _excel, NavigateToDashboard);
        Simulation = new SimulationViewModel();
        Settings = new SettingsViewModel(_settings, OnSettingsChanged);

        _currentPage = Dashboard;

        NavigateDashboardCommand = new RelayCommand(() => CurrentPage = Dashboard);
        NavigateEditorCommand = new RelayCommand(() => CurrentPage = Editor);
        NavigateSimulationCommand = new RelayCommand(() => CurrentPage = Simulation);
        NavigateSettingsCommand = new RelayCommand(() => CurrentPage = Settings);
    }

    public DashboardViewModel Dashboard { get; }
    public EditorViewModel Editor { get; }
    public SimulationViewModel Simulation { get; }
    public SettingsViewModel Settings { get; }

    public RelayCommand NavigateDashboardCommand { get; }
    public RelayCommand NavigateEditorCommand { get; }
    public RelayCommand NavigateSimulationCommand { get; }
    public RelayCommand NavigateSettingsCommand { get; }

    public bool IsDashboardActive => CurrentPage == Dashboard;
    public bool IsEditorActive => CurrentPage == Editor;
    public bool IsSimulationActive => CurrentPage == Simulation;
    public bool IsSettingsActive => CurrentPage == Settings;

    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set
        {
            if (SetProperty(ref _currentPage, value))
            {
                RaisePropertyChanged(nameof(IsDashboardActive));
                RaisePropertyChanged(nameof(IsEditorActive));
                RaisePropertyChanged(nameof(IsSimulationActive));
                RaisePropertyChanged(nameof(IsSettingsActive));
            }
        }
    }

    private void OpenInEditor(long recipeId)
    {
        try
        {
            var doc = _repo.Load(recipeId);
            Editor.LoadDocument(doc);
            CurrentPage = Editor;
        }
        catch
        {
            // ignored in prototype
        }
    }

    private void NavigateToDashboard()
    {
        Dashboard.Refresh();
        CurrentPage = Dashboard;
    }

    private void OnSettingsChanged()
    {
        Dashboard.Refresh();
        if (Editor.HasDocument)
        {
            Editor.Recalculate();
        }
    }
}
