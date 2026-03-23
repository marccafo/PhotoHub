namespace PhotoHub.Client.Native.Services;

/// <summary>
/// Prepone la URL del servidor configurada por el usuario a todos los requests
/// con URI relativa. Permite que HttpClient funcione sin BaseAddress fija,
/// haciendo la URL del servidor configurable en tiempo de ejecución.
/// </summary>
public class ServerUrlHandler : DelegatingHandler
{
    private readonly ServerUrlService _serverUrlService;

    public ServerUrlHandler(ServerUrlService serverUrlService)
    {
        _serverUrlService = serverUrlService;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var baseUrl = _serverUrlService.Url;
        if (!string.IsNullOrWhiteSpace(baseUrl) && request.RequestUri != null)
        {
            // HttpClient requiere BaseAddress para URIs relativas, por lo que usamos una URL placeholder.
            // Aquí reemplazamos el host placeholder con la URL real del servidor configurada por el usuario.
            var pathAndQuery = request.RequestUri.PathAndQuery;
            request.RequestUri = new Uri(baseUrl.TrimEnd('/') + pathAndQuery);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
