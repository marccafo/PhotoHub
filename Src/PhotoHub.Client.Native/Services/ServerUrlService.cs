namespace PhotoHub.Client.Native.Services;

/// <summary>
/// Persiste la URL del servidor API entre sesiones usando Preferences.
/// La URL se mantiene aunque el usuario cierre sesión, para pre-rellenar
/// el campo en el próximo login (comportamiento tipo Immich/Synology Photos).
/// </summary>
public class ServerUrlService
{
    private const string ServerUrlKey = "serverUrl";
    private string? _cached;

    public string? Url
    {
        get => _cached ??= Preferences.Default.Get<string?>(ServerUrlKey, null);
        set
        {
            _cached = value?.TrimEnd('/');
            if (_cached is null)
                Preferences.Default.Remove(ServerUrlKey);
            else
                Preferences.Default.Set(ServerUrlKey, _cached);
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Url);
}
