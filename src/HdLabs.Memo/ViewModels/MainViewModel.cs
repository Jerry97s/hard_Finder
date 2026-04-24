using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using HdLabs.Common.Mvvm;
using HdLabs.Memo.Helpers;
using HdLabs.Memo.Models;
using HdLabs.Memo.Services;

namespace HdLabs.Memo.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly MemoDataService _data = new();
    private MemoDataRoot _root = new();
    private string _newTitle = "";
    private string _newTitleXaml = "";
    private string _newBody = "";
    private MemoItem? _selected;
    private string _currentView = "Editor";
    private Guid? _editingId;
    private bool _isDirty;
    private bool _suppressDirty;
    private readonly DispatcherTimer _autoSave = new() { Interval = TimeSpan.FromMilliseconds(900) };

    public string Title => "HdLabs Memo";

    public ObservableCollection<MemoItem> Items { get; } = new();

    public string CurrentView
    {
        get => _currentView;
        set
        {
            if (!SetProperty(ref _currentView, value))
                return;
            if (string.Equals(_currentView, "List", StringComparison.OrdinalIgnoreCase))
                FlushEditorToItem();

            OnPropertyChanged(nameof(IsEditorView));
            OnPropertyChanged(nameof(IsListView));
        }
    }

    public bool IsEditorView => string.Equals(CurrentView, "Editor", StringComparison.OrdinalIgnoreCase);
    public bool IsListView => string.Equals(CurrentView, "List", StringComparison.OrdinalIgnoreCase);

    public string NewTitle
    {
        get => _newTitle;
        set
        {
            if (!SetProperty(ref _newTitle, value))
                return;
            if (!_suppressDirty)
                _isDirty = true;
            RestartAutoSave();
        }
    }

    public string NewTitleXaml
    {
        get => _newTitleXaml;
        set
        {
            if (!SetProperty(ref _newTitleXaml, value))
                return;
            if (!_suppressDirty)
                _isDirty = true;
            RestartAutoSave();
        }
    }

    public string NewBody
    {
        get => _newBody;
        set
        {
            if (!SetProperty(ref _newBody, value))
                return;
            if (!_suppressDirty)
                _isDirty = true;
            RestartAutoSave();
        }
    }

    public MemoItem? Selected
    {
        get => _selected;
        set
        {
            if (!SetProperty(ref _selected, value))
                return;
            OpenSelectedCommand.RaiseCanExecuteChanged();
            DeleteSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    public bool WindowTopmost
    {
        get => _root.Settings.WindowTopmost;
        set
        {
            if (_root.Settings.WindowTopmost == value)
                return;
            _root.Settings.WindowTopmost = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsPinned));
            Persist();
        }
    }

    public bool IsPinned => WindowTopmost;

    public string? CardTintHex
    {
        get => _root.Settings.CardTintHex;
        set
        {
            if (_root.Settings.CardTintHex == value)
                return;
            _root.Settings.CardTintHex = value;
            OnPropertyChanged();
            Persist();
        }
    }

    public RelayCommand NewCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ShowEditorCommand { get; }
    public RelayCommand ShowListCommand { get; }
    public RelayCommand OpenSelectedCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand TogglePinCommand { get; }

    public MainViewModel()
    {
        NewCommand = new RelayCommand(New, () => true);
        SaveCommand = new RelayCommand(CommitAndPersist);
        ShowEditorCommand = new RelayCommand(() => CurrentView = "Editor");
        ShowListCommand = new RelayCommand(GoList);
        OpenSelectedCommand = new RelayCommand(OpenSelected, () => Selected is not null);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => Selected is not null);
        TogglePinCommand = new RelayCommand(() => WindowTopmost = !WindowTopmost);

        _autoSave.Tick += (_, _) =>
        {
            _autoSave.Stop();
            FlushEditorToItem();
            Persist();
        };

        _root = _data.Load();
        if (_root.Items.Count == 0)
            Seed();
        else
        {
            foreach (var m in _root.Items.OrderByDescending(x => x.ModifiedAt))
                Items.Add(m);
        }
    }

    public void OnWindowClosing()
    {
        _autoSave.Stop();
        FlushEditorToItem();
        Persist();
    }

    public void SetCardTintFromUi(string? hex8)
    {
        if (string.IsNullOrWhiteSpace(hex8))
            return;
        _root.Settings.CardTintHex = hex8.Trim();
        OnPropertyChanged(nameof(CardTintHex));
        Persist();
    }

    public void SetTopmostFromUi(bool topmost)
    {
        if (_root.Settings.WindowTopmost == topmost)
            return;
        _root.Settings.WindowTopmost = topmost;
        OnPropertyChanged(nameof(WindowTopmost));
        OnPropertyChanged(nameof(IsPinned));
        Persist();
    }

    private void GoList()
    {
        FlushEditorToItem();
        Persist();
        CurrentView = "List";
    }

    private void New()
    {
        if (_isDirty && (NewTitle.Length > 0 || !MemoBodyDocumentHelper.IsBodyVisuallyEmpty(NewBody)))
        {
            var r = MessageBox.Show(
                "편집 중인 내용이 있습니다. 저장하지 않고 새 메모를 시작할까요?\n(아니요: 취소, 예: 지우고 새로)",
                "HdLabs Memo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (r == MessageBoxResult.No)
                return;
        }

        _editingId = null;
        _suppressDirty = true;
        try
        {
            _newTitle = "";
            _newTitleXaml = "";
            _newBody = "";
            OnPropertyChanged(nameof(NewTitle));
            OnPropertyChanged(nameof(NewTitleXaml));
            OnPropertyChanged(nameof(NewBody));
            _isDirty = false;
        }
        finally
        {
            _suppressDirty = false;
        }

        _autoSave.Stop();
        CurrentView = "Editor";
    }

    public void OpenSelected()
    {
        if (Selected is null)
            return;
        if (_isDirty)
            FlushEditorToItem();
        _editingId = Selected.Id;
        _suppressDirty = true;
        try
        {
            _newTitle = Selected.Title;
            _newTitleXaml = Selected.TitleXaml ?? "";
            _newBody = Selected.Body;
            OnPropertyChanged(nameof(NewTitle));
            OnPropertyChanged(nameof(NewTitleXaml));
            OnPropertyChanged(nameof(NewBody));
            _isDirty = false;
        }
        finally
        {
            _suppressDirty = false;
        }

        _autoSave.Stop();
        CurrentView = "Editor";
    }

    private void DeleteSelected()
    {
        if (Selected is null)
            return;
        if (MessageBox.Show($"\"{Selected.Title}\" 메모를 삭제할까요?", "삭제", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var id = Selected.Id;
        if (_editingId == id)
        {
            _editingId = null;
            _suppressDirty = true;
            try
            {
                _newTitle = "";
                _newTitleXaml = "";
                _newBody = "";
                OnPropertyChanged(nameof(NewTitle));
                OnPropertyChanged(nameof(NewTitleXaml));
                OnPropertyChanged(nameof(NewBody));
                _isDirty = false;
            }
            finally
            {
                _suppressDirty = false;
            }
        }

        for (var i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i].Id == id)
            {
                Items.RemoveAt(i);
                break;
            }
        }

        Selected = null;
        Persist();
        OpenSelectedCommand.RaiseCanExecuteChanged();
        DeleteSelectedCommand.RaiseCanExecuteChanged();
    }

    private void CommitAndPersist()
    {
        FlushEditorToItem();
        Persist();
    }

    private void FlushEditorToItem()
    {
        if (!_isDirty)
            return;
        var title = string.IsNullOrWhiteSpace(NewTitle) ? "제목 없음" : NewTitle.Trim();
        var body = NewBody ?? "";
        var now = DateTimeOffset.Now;

        if (_editingId is Guid eid)
        {
            var item = Items.FirstOrDefault(x => x.Id == eid);
            if (item is not null)
            {
                item.Title = title;
                item.TitleXaml = string.IsNullOrWhiteSpace(NewTitleXaml) ? null : NewTitleXaml;
                item.Body = body;
                item.ModifiedAt = now;
            }
        }
        else if (!string.IsNullOrEmpty(NewTitle) || !MemoBodyDocumentHelper.IsBodyVisuallyEmpty(NewBody))
        {
            var item = new MemoItem
            {
                Id = Guid.NewGuid(),
                Title = title,
                TitleXaml = string.IsNullOrWhiteSpace(NewTitleXaml) ? null : NewTitleXaml,
                Body = body,
                CreatedAt = now,
                ModifiedAt = now,
            };
            Items.Insert(0, item);
            _editingId = item.Id;
        }

        _isDirty = false;
    }

    private void RestartAutoSave()
    {
        if (CurrentView != "Editor" || _suppressDirty)
            return;
        if (_editingId is null && string.IsNullOrEmpty(NewTitle) && string.IsNullOrEmpty(NewTitleXaml)
            && MemoBodyDocumentHelper.IsBodyVisuallyEmpty(NewBody))
            return;
        _autoSave.Stop();
        _autoSave.Start();
    }

    private void Persist()
    {
        _root.Items = Items.ToList();
        _data.Save(_root);
    }

    private void Seed()
    {
        var now = DateTimeOffset.Now;
        Items.Add(new MemoItem
        {
            Title = "할일 목록",
            Body = "3월 결산" + Environment.NewLine + "보고 자료 작성" + Environment.NewLine + "업무 일지 작성",
            CreatedAt = now.AddDays(-2),
            ModifiedAt = now.AddHours(-3),
            Group = "업무"
        });
        Items.Add(new MemoItem
        {
            Title = "고객 미팅",
            Body = "오후 4시 화상 회의",
            CreatedAt = now.AddDays(-1),
            ModifiedAt = now.AddMinutes(-20),
            Group = "일정"
        });
        _root.Items = Items.ToList();
        _data.Save(_root);
    }
}
