using HdLabs.Common.Mvvm;

namespace HdLabs.Memo.Models;

public sealed class MemoItem : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _title = "";
    private string? _titleXaml;
    private string _body = "";
    private DateTimeOffset _createdAt = DateTimeOffset.Now;
    private DateTimeOffset _modifiedAt = DateTimeOffset.Now;
    private string _group = "일반";

    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>제목 서식(선택 글꼴/크기 등) 저장용 XAML. 없으면 <see cref="Title"/>만 사용.</summary>
    public string? TitleXaml
    {
        get => _titleXaml;
        set => SetProperty(ref _titleXaml, value);
    }

    public string Body
    {
        get => _body;
        set => SetProperty(ref _body, value);
    }

    public DateTimeOffset CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    public DateTimeOffset ModifiedAt
    {
        get => _modifiedAt;
        set
        {
            if (!SetProperty(ref _modifiedAt, value))
                return;
            OnPropertyChanged(nameof(ModifiedDisplay));
        }
    }

    public string Group
    {
        get => _group;
        set => SetProperty(ref _group, value);
    }

    public string CreatedDisplay => CreatedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm");

    public string ModifiedDisplay => ModifiedAt.ToLocalTime().ToString("yyyy.MM.dd HH:mm");
}
