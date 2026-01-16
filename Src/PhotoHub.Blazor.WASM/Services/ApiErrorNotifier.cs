using System.Text.Json;

namespace PhotoHub.Blazor.WASM.Services;

public sealed class ApiErrorInfo
{
    public string Title { get; init; } = "Error de API";
    public string? Message { get; init; }
    public int? StatusCode { get; init; }
    public string? ReasonPhrase { get; init; }
    public string? Url { get; init; }
    public string? Method { get; init; }
    public string? RawContent { get; init; }
    public string? TraceId { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    public string ToJson()
        => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class ApiErrorNotifier
{
    public event Action<ApiErrorInfo>? OnError;

    public void Notify(ApiErrorInfo error)
    {
        OnError?.Invoke(error);
    }
}
