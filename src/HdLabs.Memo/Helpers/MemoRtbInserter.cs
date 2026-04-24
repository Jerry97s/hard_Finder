using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HdLabs.Memo.Helpers;

/// <summary>RichTextBox/FlowDocument에 체크박스 줄·이미지 자리(블록)를 넣는다.</summary>
public static class MemoRtbInserter
{
    public static string? StoreBitmapToAppDataAsPng(BitmapSource bmp)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HdLabs", "Memo", "images");
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.Read);
            enc.Save(fs);
            return dest;
        }
        catch
        {
            return null;
        }
    }
    public static Paragraph? GetParagraphContaining(RichTextBox rtb)
    {
        var pos = rtb.CaretPosition;
        for (var n = (DependencyObject?)pos.Parent; n is not null; n = GetLogicalParent(n))
        {
            if (n is Paragraph p)
                return p;
        }
        return null;
    }

    private static DependencyObject? GetLogicalParent(DependencyObject? d) => d switch
    {
        null => null,
        FrameworkElement fe => fe.Parent,
        FrameworkContentElement c => c.Parent,
        _ => null
    };

    public static void InsertBlockAfter(Paragraph? reference, Block newBlock, FlowDocument doc)
    {
        if (doc is null)
            return;
        if (doc.Blocks.Count == 0)
        {
            doc.Blocks.Add(new Paragraph());
            reference ??= doc.Blocks.FirstBlock as Paragraph;
        }

        if (reference is null)
        {
            doc.Blocks.Add(newBlock);
            return;
        }

        var p = reference.Parent;
        if (p is ListItem li)
            li.Blocks.InsertAfter(reference, newBlock);
        else if (p is TableCell cell)
            cell.Blocks.InsertAfter(reference, newBlock);
        else if (p is Section s)
            s.Blocks.InsertAfter(reference, newBlock);
        else if (p is FlowDocument)
            doc.Blocks.InsertAfter(reference, newBlock);
        else
            doc.Blocks.Add(newBlock);
    }

    /// <summary>현재 커서가 있는 문단 <i>뒤에</i> 체크박스+입력 란이 있는 새 문단.</summary>
    public static void InsertChecklistLine(RichTextBox rtb, Brush? checkBoxForeground, Action afterDocumentChanged)
    {
        if (rtb.Document is not FlowDocument doc)
            return;
        var refPar = GetParagraphContaining(rtb);
        if (refPar is null)
        {
            if (doc.Blocks.Count == 0)
                doc.Blocks.Add(new Paragraph());
            refPar = doc.Blocks.FirstBlock as Paragraph;
        }

        var newPar = new Paragraph { Margin = new Thickness(0, 0, 0, 1) };
        var cb = new CheckBox
        {
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Cursor = Cursors.Hand,
            IsChecked = false,
        };
        if (checkBoxForeground is not null)
            cb.Foreground = checkBoxForeground;
        cb.Checked += (_, _) => afterDocumentChanged();
        cb.Unchecked += (_, _) => afterDocumentChanged();
        newPar.Inlines.Add(new InlineUIContainer(cb));
        newPar.Inlines.Add(new Run(" "));

        InsertBlockAfter(refPar, newPar, doc);

        rtb.CaretPosition = newPar.ContentEnd;
        rtb.Focus();
        afterDocumentChanged();
    }

    /// <summary>이미지 파일을 메모에 삽입(로컬 앱 데이터로 복사해 경로를 안정화). 얇은 테두리+8핸들로 PPT 식 조절.</summary>
    public static void InsertImageFromFilePath(RichTextBox rtb, string sourcePath, Brush? hintBrush, Action afterDocumentChanged)
    {
        if (rtb.Document is not FlowDocument doc)
            return;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        var storedPath = CopyImageToAppDataIfPossible(sourcePath);
        if (string.IsNullOrEmpty(storedPath) || !File.Exists(storedPath))
            return;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(storedPath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.None;
        bitmap.EndInit();
        if (bitmap.CanFreeze)
            bitmap.Freeze();

        var refPar = GetParagraphContaining(rtb);
        if (refPar is null)
        {
            if (doc.Blocks.Count == 0)
                doc.Blocks.Add(new Paragraph());
            refPar = doc.Blocks.FirstBlock as Paragraph;
        }

        // 원본 비율 유지 (긴 변 기준 900, 최소 32)
        const double maxSide = 900;
        const double minSide = 32;
        var natW = Math.Max(1, (double)bitmap.PixelWidth);
        var natH = Math.Max(1, (double)bitmap.PixelHeight);
        var ar = natW / natH;
        var scale = 1.0;
        if (natW > maxSide)
            scale = Math.Min(scale, maxSide / natW);
        if (natH > maxSide)
            scale = Math.Min(scale, maxSide / natH);
        var w0 = Math.Max(minSide, natW * scale);
        var h0 = w0 / ar;
        if (h0 > maxSide)
        {
            h0 = maxSide;
            w0 = h0 * ar;
        }

        var image = new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            SnapsToDevicePixels = true,
            Width = w0,
            Height = h0,
        };

        var hostGrid = new Grid
        {
            Width = w0,
            Height = h0,
            MinWidth = minSide,
            MinHeight = minSide,
            MaxWidth = maxSide * 1.1,
            MaxHeight = maxSide * 1.1,
            SnapsToDevicePixels = true,
            // 핸들이 (구버전) 음수 좌표로 박스 밖에 나갈 때 BlockUIContainer가 잘랐다.
            ClipToBounds = false,
        };

        const double handleSize = 8;
        const double hR = handleSize * 0.5;

        var handleCanvas = new Canvas
        {
            IsHitTestVisible = true,
            SnapsToDevicePixels = true,
            ClipToBounds = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        Panel.SetZIndex(image, 0);
        Panel.SetZIndex(handleCanvas, 1);
        hostGrid.Children.Add(image);
        hostGrid.Children.Add(handleCanvas);

        var handleFill = new SolidColorBrush(Color.FromArgb(0xF8, 0xF6, 0xFA, 0xF2));
        var handleStroke = new SolidColorBrush(Color.FromArgb(0xD0, 0x2D, 0x3D, 0x38));
        handleFill.Freeze();
        handleStroke.Freeze();

        // 테두리 선은 그리지 않음 — 핸들(조절)만 남겨 PPT·Office 이미지 편집에 가깝게.
        var outerFrame = new Border
        {
            BorderBrush = null,
            BorderThickness = new Thickness(0),
            ClipToBounds = false,
            Child = hostGrid,
        };

        Cursor CursorFor(MemoImageResizeHandle k) => k switch
        {
            MemoImageResizeHandle.Nw or MemoImageResizeHandle.Se => Cursors.SizeNWSE,
            MemoImageResizeHandle.Ne or MemoImageResizeHandle.Sw => Cursors.SizeNESW,
            MemoImageResizeHandle.N or MemoImageResizeHandle.S => Cursors.SizeNS,
            _ => Cursors.SizeWE,
        };

        var handles = new Border[8];
        for (var i = 0; i < 8; i++)
        {
            var k = (MemoImageResizeHandle)i;
            // Tag는 int만: enum을 넣으면 FlowDocument XAML 저장/로드 시 형식을 찾지 못해 XamlParseException이 난다.
            var hb = new Border
            {
                Width = handleSize,
                Height = handleSize,
                Tag = i,
                Cursor = CursorFor(k),
                SnapsToDevicePixels = true,
                Background = handleFill,
                BorderBrush = handleStroke,
                BorderThickness = new Thickness(0.8),
            };
            ToolTipService.SetToolTip(hb, "끌어서 크기 조절 (비율 유지)");
            handles[i] = hb;
            handleCanvas.Children.Add(hb);
        }

        void PlaceHandles()
        {
            var w = double.IsNaN(hostGrid.ActualWidth) || hostGrid.ActualWidth < 1
                ? hostGrid.Width
                : hostGrid.ActualWidth;
            var hy = double.IsNaN(hostGrid.ActualHeight) || hostGrid.ActualHeight < 1
                ? hostGrid.Height
                : hostGrid.ActualHeight;
            if (w < 1 || hy < 1)
                return;
            // 박스 **안**에만 배치(이전: 변 중앙에 맞추며 음수 좌표 → BlockUIContainer가 잘랐음)
            var hs = handleSize;
            var maxL = Math.Max(0, w - hs);
            var maxT = Math.Max(0, hy - hs);
            var midX = Math.Max(0, Math.Min(maxL, w * 0.5 - hR));
            var midY = Math.Max(0, Math.Min(maxT, hy * 0.5 - hR));
            for (var i = 0; i < 8; i++)
            {
                var b = handles[i];
                var k = (MemoImageResizeHandle)i;
                var left = 0.0;
                var top = 0.0;
                switch (k)
                {
                    case MemoImageResizeHandle.Nw: left = 0; top = 0; break;
                    case MemoImageResizeHandle.N: left = midX; top = 0; break;
                    case MemoImageResizeHandle.Ne: left = maxL; top = 0; break;
                    case MemoImageResizeHandle.W: left = 0; top = midY; break;
                    case MemoImageResizeHandle.E: left = maxL; top = midY; break;
                    case MemoImageResizeHandle.Sw: left = 0; top = maxT; break;
                    case MemoImageResizeHandle.S: left = midX; top = maxT; break;
                    case MemoImageResizeHandle.Se: left = maxL; top = maxT; break;
                }

                Canvas.SetLeft(b, left);
                Canvas.SetTop(b, top);
            }
        }

        hostGrid.SizeChanged += (_, _) => PlaceHandles();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Loaded,
            new Action(PlaceHandles));

        MemoImageResizeHandle? active = null;
        Point? p0Rtb = null;
        var startW = 0.0;
        var startH = 0.0;

        void ApplySize(double w1)
        {
            w1 = Math.Max(minSide, Math.Min(maxSide, w1));
            var h1 = w1 / ar;
            if (h1 > maxSide)
            {
                h1 = maxSide;
                w1 = h1 * ar;
            }

            w1 = Math.Max(minSide, w1);
            h1 = Math.Max(minSide, h1);
            hostGrid.Width = w1;
            hostGrid.Height = h1;
            image.Width = w1;
            image.Height = h1;
            PlaceHandles();
        }

        void OnDown(object s, MouseButtonEventArgs e)
        {
            if (s is not Border hb)
                return;
            if (hb.Tag is not int handleIdx || handleIdx < 0 || handleIdx > 7)
                return;
            var k = (MemoImageResizeHandle)handleIdx;
            var u = (UIElement)hb;
            u.CaptureMouse();
            e.Handled = true;
            active = k;
            p0Rtb = e.GetPosition(rtb);
            startW = double.IsNaN(hostGrid.ActualWidth) || hostGrid.ActualWidth < 1
                ? hostGrid.Width
                : hostGrid.ActualWidth;
            startH = double.IsNaN(hostGrid.ActualHeight) || hostGrid.ActualHeight < 1
                ? hostGrid.Height
                : hostGrid.ActualHeight;
        }

        void OnMove(object s, MouseEventArgs e)
        {
            if (active is not MemoImageResizeHandle k || p0Rtb is not Point p0 || s is not UIElement u
                || !u.IsMouseCaptured)
                return;
            var p1 = e.GetPosition(rtb);
            var dX = p1.X - p0.X;
            var dY = p1.Y - p0.Y;
            if (dX == 0 && dY == 0)
                return;
            if (startW < 0.1 || startH < 0.1)
                return;

            var w1 = startW;
            var sX = 1.0;
            var sY = 1.0;
            var sA = 1.0;

            switch (k)
            {
                case MemoImageResizeHandle.E:
                    w1 = startW + dX;
                    break;
                case MemoImageResizeHandle.W:
                    w1 = startW - dX;
                    break;
                case MemoImageResizeHandle.S:
                    {
                        var h1 = startH + dY;
                        w1 = h1 * ar;
                        break;
                    }
                case MemoImageResizeHandle.N:
                    {
                        var h1 = startH - dY;
                        w1 = h1 * ar;
                        break;
                    }
                case MemoImageResizeHandle.Se:
                    sX = (startW + dX) / startW;
                    sY = (startH + dY) / startH;
                    sA = (sX + sY) * 0.5;
                    w1 = startW * sA;
                    break;
                case MemoImageResizeHandle.Sw:
                    sX = (startW - dX) / startW;
                    sY = (startH + dY) / startH;
                    sA = (sX + sY) * 0.5;
                    w1 = startW * sA;
                    break;
                case MemoImageResizeHandle.Ne:
                    sX = (startW + dX) / startW;
                    sY = (startH - dY) / startH;
                    sA = (sX + sY) * 0.5;
                    w1 = startW * sA;
                    break;
                case MemoImageResizeHandle.Nw:
                    sX = (startW - dX) / startW;
                    sY = (startH - dY) / startH;
                    sA = (sX + sY) * 0.5;
                    w1 = startW * sA;
                    break;
            }

            ApplySize(w1);
        }

        void EndDrag()
        {
            if (active is null)
                return;
            active = null;
            p0Rtb = null;
            afterDocumentChanged();
        }

        void OnUp(object? s, MouseButtonEventArgs? e)
        {
            if (s is UIElement u && u.IsMouseCaptured)
                u.ReleaseMouseCapture();
            EndDrag();
        }

        void OnLost() => EndDrag();

        foreach (var b in handles)
        {
            b.PreviewMouseLeftButtonDown += OnDown;
            b.PreviewMouseMove += OnMove;
            b.PreviewMouseLeftButtonUp += (ss, e) => OnUp(ss, e);
            b.LostMouseCapture += (_, _) => OnLost();
        }

        // 여러 이미지를 한 줄에 넣을 수 있게 InlineUIContainer로 삽입한다.
        // (기존 BlockUIContainer는 이미지 1개가 문단 1줄을 점유)
        outerFrame.Margin = new Thickness(0, 0, 6, 0);
        var caret = rtb.CaretPosition.GetInsertionPosition(LogicalDirection.Forward);
        var inline = new InlineUIContainer(outerFrame, caret)
        {
            BaselineAlignment = BaselineAlignment.Center,
        };
        // 다음 삽입을 위해 공백 Run을 하나 둔다.
        var space = new Run(" ", inline.ElementEnd);
        rtb.CaretPosition = space.ContentEnd;
        rtb.Focus();
        afterDocumentChanged();
    }

    private static string? CopyImageToAppDataIfPossible(string sourcePath)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HdLabs", "Memo", "images");
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(sourcePath);
            if (string.IsNullOrEmpty(ext) || ext.Length > 8)
                ext = ".img";
            var dest = Path.Combine(dir, Guid.NewGuid().ToString("N") + ext);
            File.Copy(sourcePath, dest, overwrite: true);
            return dest;
        }
        catch
        {
            if (File.Exists(sourcePath))
                return Path.GetFullPath(sourcePath);
            return null;
        }
    }

    /// <summary>빈 selection 에서 ApplyPropertyValue 만으로는 이후 입력이 같은 Run 이어붙이며 서식이 안 묻는 경우가 있어, 캐럿에 U+200B Run 을 두어 이후에 치는 글자가 그 Run 에 붙게 함.</summary>
    public static bool TryApplyEmptySelectionFontToDocument(RichTextBox rtb, FontFamily? newFontFamily, double? newSizeDips)
    {
        if (rtb.Document is not FlowDocument)
            return false;
        if (!rtb.Selection.IsEmpty)
            return false;
        if (newFontFamily is null
            && (newSizeDips is not { } ns || ns <= 0 || double.IsNaN(ns) || double.IsInfinity(ns)))
            return false;

        var caret = rtb.CaretPosition;
        if (GetRunForCaret(caret) is (Run run, int offsetInText) wr)
        {
            return SplitRunForInsertionWithZwspRun(rtb, wr.run, wr.offsetInText, newFontFamily, newSizeDips);
        }

        if (GetParagraphContaining(rtb) is { } par && par.Inlines.Count == 0)
        {
            var r = new Run("\u200b")
            {
                FontFamily = newFontFamily ?? par.FontFamily,
            };
            if (newSizeDips is { } s && s > 0)
                r.FontSize = s;
            else if (par.FontSize > 0)
                r.FontSize = par.FontSize;
            par.Inlines.Add(r);
            rtb.CaretPosition = r.ContentEnd;
            return true;
        }

        return false;
    }

    private static (Run run, int offsetInText)? GetRunForCaret(TextPointer? caret)
    {
        if (caret is null)
            return null;
        for (var d = (DependencyObject?)caret.Parent; d is not null; d = GetLogicalParent(d))
        {
            if (d is Run r)
            {
                var t = new TextRange(r.ContentStart, r.ContentEnd).Text;
                var o = r.ContentStart.GetOffsetToPosition(caret);
                if (o < 0)
                    o = 0;
                if (o > t.Length)
                    o = t.Length;
                return (r, o);
            }
        }
        return null;
    }

    private static InlineCollection? InlinesForRunParent(DependencyObject? parent) => parent switch
    {
        Paragraph p => p.Inlines,
        Span s => s.Inlines,
        _ => null
    };

    private static void CopyRunStyle(Run from, Run to)
    {
        to.FontWeight = from.FontWeight;
        to.FontStyle = from.FontStyle;
        to.FontStretch = from.FontStretch;
        to.FontFamily = from.FontFamily;
        to.FontSize = from.FontSize;
        to.Foreground = from.Foreground;
        to.Background = from.Background;
        to.TextDecorations = from.TextDecorations;
    }

    private static bool SplitRunForInsertionWithZwspRun(
        RichTextBox rtb, Run run, int offset, FontFamily? newFontFamily, double? newSizeDips)
    {
        var t = new TextRange(run.ContentStart, run.ContentEnd).Text;
        if (t.Length < offset)
            offset = t.Length;
        if (t == "\u200b")
        {
            if (newFontFamily is { } f)
                run.FontFamily = f;
            if (newSizeDips is { } s && s > 0)
                run.FontSize = s;
            rtb.CaretPosition = run.ContentEnd;
            return true;
        }
        if (t.Length == 0)
            return false;

        var inlines = InlinesForRunParent(run.Parent);
        if (inlines is null)
            return false;

        var before = t[..offset];
        var after = t[offset..];

        // 템플릿: 아래 run.Text 를 잘라내기 **전** 모양
        var tailFamily = run.FontFamily;
        var tailSize = run.FontSize;
        var tailW = run.FontWeight;
        var tailSt = run.FontStyle;
        var tailStr = run.FontStretch;
        var tailFg = run.Foreground;
        var tailBg = run.Background;
        var tailDec = run.TextDecorations;

        var insert = new Run("\u200b");
        CopyRunStyle(run, insert);
        if (newFontFamily is { } nf)
            insert.FontFamily = nf;
        if (newSizeDips is { } z && z > 0)
            insert.FontSize = z;

        if (before.Length == 0)
        {
            inlines.InsertBefore(run, insert);
            rtb.CaretPosition = insert.ContentEnd;
            return true;
        }

        run.Text = before;

        inlines.InsertAfter(run, insert);

        if (after.Length > 0)
        {
            var tail = new Run(after)
            {
                FontFamily = tailFamily,
                FontSize = tailSize,
                FontWeight = tailW,
                FontStyle = tailSt,
                FontStretch = tailStr,
                Foreground = tailFg,
                Background = tailBg,
                TextDecorations = tailDec
            };
            inlines.InsertAfter(insert, tail);
        }

        rtb.CaretPosition = insert.ContentEnd;
        return true;
    }
}
