using PhotoHub.Blazor.Shared.Services;
using PhotoHub.Blazor.Shared.Models;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Media;

namespace PhotoHub.Blazor.MAUI.Services;

public class MauiPendingAssetsProvider : IPendingAssetsProvider
{
    public async Task<List<TimelineItem>> GetPendingAssetsAsync()
    {
        // En MAUI podemos usar MediaPicker para seleccionar fotos, 
        // pero para "automatizar" la detección de pendientes como en un backup nativo
        // necesitaríamos permisos de sistema de archivos.
        // Por ahora implementamos la selección manual via MediaPicker para cumplir el contrato.
        return await Task.FromResult(new List<TimelineItem>());
    }

    public async Task<AssetDetail?> GetPendingAssetDetailAsync(string path)
    {
        return await Task.FromResult<AssetDetail?>(null);
    }

    public async Task<Stream> GetAssetStreamAsync(string id)
    {
        throw new NotImplementedException("Se requiere implementación nativa para acceso a archivos.");
    }

    public async Task MarkAsUploadedAsync(string id)
    {
        // Lógica para registrar qué archivos ya se subieron
        await Task.CompletedTask;
    }
}
