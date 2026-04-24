using System.Net.Http;
using System.Net.Http.Json;

namespace HdLabs.Memo.Ai;

public sealed class AiClient
{
    private readonly HttpClient _http;

    public AiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<AiChatResponse> ChatAsync(AiChatRequest req, CancellationToken ct)
    {
        // FastAPI 쪽 기본을 /chat 로 가정 (필요 시 서버에서 라우트 맞추면 됨)
        using var res = await _http.PostAsJsonAsync("chat", req, ct);
        res.EnsureSuccessStatusCode();

        var body = await res.Content.ReadFromJsonAsync<AiChatResponse>(cancellationToken: ct);
        return body ?? new AiChatResponse("서버 응답을 읽지 못했습니다.");
    }
}

