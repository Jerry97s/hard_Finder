using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace HdLabs.Memo.Helpers;

/// <summary>Memo 본문: <see cref="FlowDocument"/> ↔ XAML(저장) · 이전 JSON 일반 텍스트 호환.</summary>
public static class MemoBodyDocumentHelper
{
    public static FlowDocument CreateDefaultEmptyDocument()
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Malgun Gothic"),
            FontSize = 15,
        };
        doc.Blocks.Add(new Paragraph());
        return doc;
    }

    /// <summary>본문이 비어 있거나 공백만인지(저장·새 메모 판정용). XAML은 FlowDocument로 파싱해 평문을 검사.</summary>
    public static bool IsBodyVisuallyEmpty(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return true;
        var t = body!.TrimStart();
        if (t.Length == 0)
            return true;
        if (t[0] != '<')
            return string.IsNullOrWhiteSpace(body);

        try
        {
            var d = UnwrapToFlowDocument(XamlReader.Parse(body!)) ?? throw new InvalidOperationException();
            if (d.Blocks.Count == 0)
                d.Blocks.Add(new Paragraph());
            return string.IsNullOrWhiteSpace(new TextRange(d.ContentStart, d.ContentEnd).Text);
        }
        catch
        {
            return string.IsNullOrWhiteSpace(body);
        }
    }

    public static void ApplyBaseDocumentStyle(FlowDocument? doc, Brush? defaultForeground = null)
    {
        if (doc is null)
            return;
        doc.PagePadding = new Thickness(0);
        doc.TextAlignment = TextAlignment.Left;
        doc.Background = Brushes.Transparent;
        doc.FontFamily = new FontFamily("Malgun Gothic");
        doc.FontSize = 15;
        if (defaultForeground is not null)
            doc.Foreground = defaultForeground;
    }

    /// <summary>저장 문자열( XAML ) 또는 이전 JSON 일반 텍스트.</summary>
    public static FlowDocument FromStorageString(string? body, Brush? defaultForeground = null)
    {
        if (string.IsNullOrEmpty(body))
        {
            var empty = CreateDefaultEmptyDocument();
            ApplyBaseDocumentStyle(empty, defaultForeground);
            return empty;
        }

        var t = body!.TrimStart();
        if (t.Length > 0 && t[0] == '<')
        {
            try
            {
                var fd = UnwrapToFlowDocument(XamlReader.Parse(body!));
                if (fd is not null)
                {
                    ApplyBaseDocumentStyle(fd, defaultForeground);
                    if (fd.Blocks.Count == 0)
                        fd.Blocks.Add(new Paragraph());
                    return fd;
                }
            }
            catch
            {
                // fall through: treat as plain text
            }
        }

        var plain = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left,
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Malgun Gothic"),
            FontSize = 15,
        };
        if (defaultForeground is not null)
            plain.Foreground = defaultForeground;
        plain.Blocks.Add(new Paragraph(new Run(body!)));
        return plain;
    }

    private static FlowDocument? UnwrapToFlowDocument(object? obj) => obj switch
    {
        FlowDocument d => d,
        Section s => MoveBlocksToNewFlow(s),
        Paragraph p => new FlowDocument(p),
        _ => null,
    };

    private static FlowDocument MoveBlocksToNewFlow(Section s)
    {
        var d = new FlowDocument();
        var list = s.Blocks.ToList();
        foreach (var b in list)
        {
            s.Blocks.Remove(b);
            d.Blocks.Add(b);
        }
        return d;
    }

    public static string ToXamlString(FlowDocument document)
    {
        using var sw = new StringWriter();
        XamlWriter.Save(document, sw);
        return sw.ToString();
    }
}
