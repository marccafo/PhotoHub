using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace PhotoHub.Blazor.WASM.Services;

public sealed class ApiErrorHandler : DelegatingHandler
{
    private readonly ApiErrorNotifier _notifier;

    public ApiErrorHandler(ApiErrorNotifier notifier)
    {
        _notifier = notifier;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode && (int)response.StatusCode >= 500)
            {
                var content = await ReadContentAsync(response);
                var (message, traceId) = ExtractMessageAndTraceId(content);
                var info = new ApiErrorInfo
                {
                    Title = "Error: algo salió mal",
                    Message = message ?? "La API devolvió un error.",
                    StatusCode = (int)response.StatusCode,
                    ReasonPhrase = response.ReasonPhrase,
                    Url = request.RequestUri?.ToString(),
                    Method = request.Method.Method,
                    RawContent = content,
                    TraceId = traceId
                };

                _notifier.Notify(info);
            }

            return response;
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            throw;
        }
    }

    private static async Task<string?> ReadContentAsync(HttpResponseMessage response)
    {
        if (response.Content == null)
        {
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "text/plain";
        var encoding = response.Content.Headers.ContentType?.CharSet is { } charset
            ? Encoding.GetEncoding(charset)
            : Encoding.UTF8;

        response.Content = new StringContent(content, encoding, mediaType);
        return content;
    }

    private static (string? message, string? traceId) ExtractMessageAndTraceId(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (content.Trim(), null);
            }

            string? message = TryGetString(document.RootElement, "error")
                ?? TryGetString(document.RootElement, "message")
                ?? TryGetString(document.RootElement, "detail")
                ?? TryGetString(document.RootElement, "title");

            string? traceId = TryGetString(document.RootElement, "traceId")
                ?? TryGetString(document.RootElement, "traceID")
                ?? TryGetString(document.RootElement, "trace_id");

            return (message ?? content.Trim(), traceId);
        }
        catch
        {
            return (content.Trim(), null);
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }
}
