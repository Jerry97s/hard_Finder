using System.IO;
using System.Windows.Documents;
using System.Windows.Markup;

namespace HdLabs.Memo.Helpers;

/// <summary>제목(한 줄): FlowDocument ↔ XAML(저장). 줄바꿈은 제거.</summary>
public static class MemoTitleDocumentHelper
{
    public static FlowDocument CreateDefaultDocument()
    {
        var doc = MemoBodyDocumentHelper.CreateDefaultEmptyDocument();
        // 제목은 한 문단만
        doc.Blocks.Clear();
        doc.Blocks.Add(new Paragraph());
        return doc;
    }

    public static FlowDocument FromStorageString(string? xamlOrPlain)
    {
        if (string.IsNullOrEmpty(xamlOrPlain))
            return CreateDefaultDocument();

        var t = xamlOrPlain.TrimStart();
        if (t.Length > 0 && t[0] == '<')
        {
            try
            {
                var fd = (FlowDocument?)XamlReader.Parse(xamlOrPlain);
                if (fd is not null)
                {
                    MemoBodyDocumentHelper.ApplyBaseDocumentStyle(fd, defaultForeground: null);
                    return fd;
                }
            }
            catch
            {
                // fall through
            }
        }

        var doc = CreateDefaultDocument();
        if (doc.Blocks.FirstBlock is Paragraph p)
            p.Inlines.Add(new Run(SanitizePlain(xamlOrPlain)));
        return doc;
    }

    public static string ToXamlString(FlowDocument document)
    {
        using var sw = new StringWriter();
        XamlWriter.Save(document, sw);
        return sw.ToString();
    }

    public static string ToPlainText(FlowDocument document)
    {
        var text = new TextRange(document.ContentStart, document.ContentEnd).Text;
        return SanitizePlain(text);
    }

    private static string SanitizePlain(string s) =>
        (s ?? "").Replace("\r", "").Replace("\n", "");
}

