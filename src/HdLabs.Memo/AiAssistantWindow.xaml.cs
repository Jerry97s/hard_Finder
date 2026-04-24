using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HdLabs.Memo.Ai;

namespace HdLabs.Memo;

public partial class AiAssistantWindow : Window
{
    private readonly Func<AiMemoContext> _getContext;
    private readonly ObservableCollection<ChatLine> _lines = new();
    private CancellationTokenSource? _cts;

    public AiAssistantWindow(Func<AiMemoContext> getContext)
    {
        InitializeComponent();
        _getContext = getContext;
        ChatItems.ItemsSource = _lines;
        AddSystem("질문을 입력하고 전송하세요. 필요하면 '메모 내용 포함'을 켜면 됩니다.");
    }

    private void AddUser(string text) =>
        _lines.Add(ChatLine.User(text));

    private void AddAssistant(string text) =>
        _lines.Add(ChatLine.Assistant(text));

    private void AddSystem(string text) =>
        _lines.Add(ChatLine.System(text));

    private async Task SendAsync(string message, string? mode)
    {
        var server = (ServerUrlBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(server))
        {
            AddSystem("서버 주소가 비어 있습니다.");
            return;
        }

        if (!server.EndsWith("/", StringComparison.Ordinal))
            server += "/";

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        SendButton.IsEnabled = false;
        try
        {
            var include = IncludeMemoCheck.IsChecked == true;
            var ctx = include ? _getContext() : null;

            using var http = new HttpClient { BaseAddress = new Uri(server) };
            http.Timeout = TimeSpan.FromSeconds(60);
            var client = new AiClient(http);

            var req = new AiChatRequest(
                Message: message,
                IncludeMemo: include,
                Memo: ctx,
                Mode: mode);

            var res = await client.ChatAsync(req, _cts.Token);
            AddAssistant(res.Reply ?? "");
        }
        catch (OperationCanceledException)
        {
            AddSystem("요청이 취소되었습니다.");
        }
        catch (Exception ex)
        {
            AddSystem($"요청 실패: {ex.Message}");
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var msg = (InputBox.Text ?? "").Trim();
        if (msg.Length == 0)
            return;
        InputBox.Clear();
        AddUser(msg);
        await SendAsync(msg, mode: "chat");
    }

    private async void Summarize_Click(object sender, RoutedEventArgs e)
    {
        AddUser("현재 메모를 요약해줘.");
        await SendAsync("현재 메모를 요약해줘. 핵심 bullet로 정리하고, TODO가 있으면 마지막에 TODO로 묶어줘.", mode: "summarize");
    }

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            return;
        e.Handled = true;
        Send_Click(sender, e);
    }

    private sealed record ChatLine(string Role, string Text, Brush BubbleBackground)
    {
        public static ChatLine User(string t) => new("나", t, new SolidColorBrush(Color.FromArgb(0x26, 0x70, 0x9E, 0x8B)));
        public static ChatLine Assistant(string t) => new("AI", t, new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)));
        public static ChatLine System(string t) => new("시스템", t, new SolidColorBrush(Color.FromArgb(0x18, 0x9B, 0xB5, 0xAA)));
    }
}

