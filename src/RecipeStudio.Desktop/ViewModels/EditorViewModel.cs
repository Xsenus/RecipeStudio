using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Threading;
using RecipeStudio.Desktop.Models;
using RecipeStudio.Desktop.Services;

namespace RecipeStudio.Desktop.ViewModels;

public sealed class EditorViewModel : ViewModelBase
{
    private readonly SettingsService _settings;
    private readonly RecipeRepository _repo;
    private readonly RecipeExcelService _excel;
    private readonly Action _navigateBack;

    private RecipeDocument? _document;
    private RecipePoint? _selectedPoint;

    // Simulation
    private readonly DispatcherTimer _timer;
    private bool _isPlaying;
    private double _speedMultiplier = 1.0;
    private double _progress;

    private bool _suppressRecalc;

    public EditorViewModel(SettingsService settings, RecipeRepository repo, RecipeExcelService excel, Action navigateBack)
    {
        _settings = settings;
        _repo = repo;
        _excel = excel;
        _navigateBack = navigateBack;

        Points = new ObservableCollection<RecipePoint>();

        SaveCommand = new RelayCommand(Save, () => HasDocument);
        BackCommand = new RelayCommand(() => _navigateBack());
        RecalculateCommand = new RelayCommand(Recalculate, () => HasDocument);

        ExportExcelCommand = new RelayCommand(ExportExcelRequested, () => HasDocument);
        ImportExcelCommand = new RelayCommand(ImportExcelRequested, () => HasDocument);

        AddPointCommand = new RelayCommand(AddPoint, () => HasDocument);
        RemovePointCommand = new RelayCommand(RemoveSelectedPoint, () => HasDocument && SelectedPoint is not null);
        DuplicatePointCommand = new RelayCommand(DuplicateSelectedPoint, () => HasDocument && SelectedPoint is not null);

        MoveUpCommand = new RelayCommand(MoveUp, () => HasDocument && SelectedPoint is not null && SelectedPoint.NPoint > 1);
        MoveDownCommand = new RelayCommand(MoveDown, () => HasDocument && SelectedPoint is not null && SelectedPoint.NPoint < Points.Count);

        PlayPauseCommand = new RelayCommand(TogglePlay, () => HasDocument);
        StopCommand = new RelayCommand(Stop, () => HasDocument);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
        };
        _timer.Tick += (_, __) => Tick();
    }

    public bool HasDocument => _document is not null;

    public AppSettings AppSettings => _settings.Settings;


    public RecipeDocument? Document
    {
        get => _document;
        private set
        {
            if (SetProperty(ref _document, value))
            {
                RaisePropertyChanged(nameof(HasDocument));
                RaisePropertyChanged(nameof(RecipeCode));
                RaisePropertyChanged(nameof(FilePath));
                RaisePropertyChanged(nameof(ContainerPresent));
                RaisePropertyChanged(nameof(DClampForm));
                RaisePropertyChanged(nameof(DClampCont));
                SaveCommand.RaiseCanExecuteChanged();
                RecalculateCommand.RaiseCanExecuteChanged();
                ExportExcelCommand.RaiseCanExecuteChanged();
                ImportExcelCommand.RaiseCanExecuteChanged();
                AddPointCommand.RaiseCanExecuteChanged();
                PlayPauseCommand.RaiseCanExecuteChanged();
                StopCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string RecipeCode
    {
        get => Document?.RecipeCode ?? "";
        set
        {
            if (Document is null) return;
            Document.RecipeCode = value;
            RaisePropertyChanged();
        }
    }

    public string FilePath => Document?.FilePath ?? "";

    public bool ContainerPresent
    {
        get => Document?.ContainerPresent ?? true;
        set
        {
            if (Document is null) return;
            Document.ContainerPresent = value;
            RaisePropertyChanged();
            Recalculate();
        }
    }

    public double DClampForm
    {
        get => Document?.DClampForm ?? 800;
        set
        {
            if (Document is null) return;
            Document.DClampForm = value;
            RaisePropertyChanged();
            Recalculate();
        }
    }

    public double DClampCont
    {
        get => Document?.DClampCont ?? 1600;
        set
        {
            if (Document is null) return;
            Document.DClampCont = value;
            RaisePropertyChanged();
            Recalculate();
        }
    }

    public ObservableCollection<RecipePoint> Points { get; }

    public RecipePoint? SelectedPoint
    {
        get => _selectedPoint;
        set
        {
            if (SetProperty(ref _selectedPoint, value))
            {
                RemovePointCommand.RaiseCanExecuteChanged();
                DuplicatePointCommand.RaiseCanExecuteChanged();
                MoveUpCommand.RaiseCanExecuteChanged();
                MoveDownCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // Commands
    public RelayCommand SaveCommand { get; }
    public RelayCommand BackCommand { get; }
    public RelayCommand RecalculateCommand { get; }

    // Excel commands are triggered by the View (file dialogs).
    public RelayCommand ExportExcelCommand { get; }
    public RelayCommand ImportExcelCommand { get; }

    public event Action? RequestExportExcel;
    public event Action? RequestImportExcel;

    public RelayCommand AddPointCommand { get; }
    public RelayCommand RemovePointCommand { get; }
    public RelayCommand DuplicatePointCommand { get; }
    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }

    public RelayCommand PlayPauseCommand { get; }
    public RelayCommand StopCommand { get; }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    public double SpeedMultiplier
    {
        get => _speedMultiplier;
        set => SetProperty(ref _speedMultiplier, value);
    }

    /// <summary>
    /// 0..1 progress along the tool path.
    /// </summary>
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    public void CloseDocument()
    {
        Stop();
        UnhookPoints();
        Points.Clear();
        SelectedPoint = null;
        Document = null;
    }

    public void LoadDocument(RecipeDocument doc)
    {
        UnhookPoints();

        Document = doc;
        Points.Clear();
        foreach (var p in doc.Points)
        {
            Points.Add(p);
        }

        HookPoints();

        Recalculate();
        SelectedPoint = Points.FirstOrDefault();
        Stop();
    }

    public void Recalculate()
    {
        if (Document is null) return;
        if (_suppressRecalc) return;

        try
        {
            _suppressRecalc = true;

            Document.Points.Clear();
            foreach (var p in Points)
                Document.Points.Add(p);

            RecipeCalculator.Recalculate(Document, _settings.Settings);

            // Refresh computed values in-place; also refresh recipe-level fields
            RaisePropertyChanged(nameof(RecipeCode));
            RaisePropertyChanged(nameof(ContainerPresent));
            RaisePropertyChanged(nameof(DClampForm));
            RaisePropertyChanged(nameof(DClampCont));
        }
        finally
        {
            _suppressRecalc = false;
        }
    }

    private void Save()
    {
        if (Document is null) return;
        Recalculate();
        _repo.Save(Document);
    }

    private void ExportExcelRequested()
        => RequestExportExcel?.Invoke();

    private void ImportExcelRequested()
        => RequestImportExcel?.Invoke();

    public void ExportToExcel(string path)
    {
        if (Document is null) return;
        Recalculate();
        _excel.Export(Document, path);
    }

    public void ImportFromExcel(string path)
    {
        if (Document is null) return;

        var imported = _excel.Import(path);

        // Replace points in the current document (same RecipeId, update code and recipe-level settings).
        imported.RecipeId = Document.RecipeId;
        imported.CreatedUtc = Document.CreatedUtc;
        imported.ModifiedUtc = Document.ModifiedUtc;

        UnhookPoints();

        Document.RecipeCode = imported.RecipeCode;
        Document.ContainerPresent = imported.ContainerPresent;
        Document.DClampForm = imported.DClampForm;
        Document.DClampCont = imported.DClampCont;

        Points.Clear();
        foreach (var p in imported.Points)
            Points.Add(p);

        HookPoints();

        Recalculate();
    }

    private void AddPoint()
    {
        if (Document is null) return;

        var last = Points.LastOrDefault();
        var p = last is null
            ? RecipeDocumentFactory.CreateStarter(Document.RecipeCode).Points.First()
            : last.Clone();

        p.NPoint = Points.Count + 1;
        Points.Add(p);
        SelectedPoint = p;

        Recalculate();
    }

    private void DuplicateSelectedPoint()
    {
        if (SelectedPoint is null) return;

        var idx = SelectedPoint.NPoint - 1;
        var clone = SelectedPoint.Clone();
        clone.NPoint = idx + 2;

        Points.Insert(idx + 1, clone);
        SelectedPoint = clone;
        Recalculate();
    }

    private void RemoveSelectedPoint()
    {
        if (SelectedPoint is null) return;
        var idx = SelectedPoint.NPoint - 1;
        Points.RemoveAt(idx);
        SelectedPoint = Points.ElementAtOrDefault(Math.Clamp(idx, 0, Points.Count - 1));
        Recalculate();
    }

    private void MoveUp()
    {
        if (SelectedPoint is null) return;
        var idx = SelectedPoint.NPoint - 1;
        if (idx <= 0) return;

        Points.Move(idx, idx - 1);
        SelectedPoint = Points[idx - 1];
        Recalculate();
    }

    private void MoveDown()
    {
        if (SelectedPoint is null) return;
        var idx = SelectedPoint.NPoint - 1;
        if (idx >= Points.Count - 1) return;

        Points.Move(idx, idx + 1);
        SelectedPoint = Points[idx + 1];
        Recalculate();
    }

    private void HookPoints()
    {
        foreach (var p in Points)
        {
            p.PropertyChanged += OnPointPropertyChanged;
        }
    }

    private void UnhookPoints()
    {
        foreach (var p in Points)
        {
            p.PropertyChanged -= OnPointPropertyChanged;
        }
    }

    private void OnPointPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressRecalc) return;

        // If user edits an input column (or drags a point), we recalculate.
        switch (e.PropertyName)
        {
            case nameof(RecipePoint.RCrd):
            case nameof(RecipePoint.ZCrd):
            case nameof(RecipePoint.Place):
            case nameof(RecipePoint.ANozzle):
            case nameof(RecipePoint.Alfa):
            case nameof(RecipePoint.Betta):
            case nameof(RecipePoint.SpeedTable):
            case nameof(RecipePoint.IceRate):
            case nameof(RecipePoint.Container):
            case nameof(RecipePoint.DClampForm):
            case nameof(RecipePoint.DClampCont):
            case nameof(RecipePoint.Safe):
                Recalculate();
                break;
        }
    }

    private void TogglePlay()
    {
        if (IsPlaying)
        {
            IsPlaying = false;
            _timer.Stop();
        }
        else
        {
            IsPlaying = true;
            _timer.Start();
        }
    }

    private void Stop()
    {
        IsPlaying = false;
        _timer.Stop();
        Progress = 0;
    }

    private void Tick()
    {
        if (!IsPlaying) return;

        // Basic animation: advance by a small delta, scaled by SpeedMultiplier.
        // In a later iteration this should be time-based per-segment using t_sec.
        var delta = 0.0025 * SpeedMultiplier;
        Progress += delta;
        if (Progress >= 1)
        {
            Progress = 1;
            IsPlaying = false;
            _timer.Stop();
        }
    }
}
