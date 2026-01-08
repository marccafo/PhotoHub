using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Models;
using PhotoHub.API.Shared.Services;

namespace PhotoHub.API.Shared.Services;

public class MlJobProcessorService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MlJobProcessorService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
    
    public MlJobProcessorService(
        IServiceProvider serviceProvider,
        ILogger<MlJobProcessorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ML Job Processor Service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ML jobs");
            }
            
            await Task.Delay(_pollInterval, stoppingToken);
        }
        
        _logger.LogInformation("ML Job Processor Service stopped");
    }
    
    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var mlJobService = scope.ServiceProvider.GetRequiredService<IMlJobService>();
        
        var pendingJobs = await mlJobService.GetPendingJobsAsync(cancellationToken);
        
        if (!pendingJobs.Any())
            return;
        
        _logger.LogInformation("Processing {Count} pending ML jobs", pendingJobs.Count);
        
        foreach (var job in pendingJobs.Take(5)) // Process max 5 at a time
        {
            try
            {
                await ProcessJobAsync(job, dbContext, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing job {JobId}", job.Id);
                job.Status = MlJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
    
    private async Task ProcessJobAsync(
        AssetMlJob job, 
        ApplicationDbContext dbContext, 
        CancellationToken cancellationToken)
    {
        job.Status = MlJobStatus.Processing;
        job.StartedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        
        // Load asset
        var asset = await dbContext.Assets
            .Include(a => a.Exif)
            .FirstOrDefaultAsync(a => a.Id == job.AssetId, cancellationToken);
        
        if (asset == null || !File.Exists(asset.FullPath))
        {
            throw new FileNotFoundException($"Asset {job.AssetId} not found");
        }
        
        // Process based on job type
        string? resultJson = null;
        
        switch (job.JobType)
        {
            case MlJobType.FaceDetection:
                resultJson = await ProcessFaceDetectionAsync(asset, cancellationToken);
                break;
            case MlJobType.ObjectRecognition:
                resultJson = await ProcessObjectRecognitionAsync(asset, cancellationToken);
                break;
            case MlJobType.SceneClassification:
                resultJson = await ProcessSceneClassificationAsync(asset, cancellationToken);
                break;
            case MlJobType.TextRecognition:
                resultJson = await ProcessTextRecognitionAsync(asset, cancellationToken);
                break;
        }
        
        job.Status = MlJobStatus.Completed;
        job.ResultJson = resultJson;
        job.CompletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        
        _logger.LogInformation("ML job completed: JobId={JobId}, AssetId={AssetId}, JobType={JobType}", 
            job.Id, job.AssetId, job.JobType);
    }
    
    private async Task<string> ProcessFaceDetectionAsync(Asset asset, CancellationToken cancellationToken)
    {
        // TODO: Integrate with ML library (ML.NET, TensorFlow.NET, etc.)
        // For now, return empty JSON
        await Task.CompletedTask;
        _logger.LogInformation("Face detection processing for asset {AssetId} - placeholder implementation", asset.Id);
        return "{}";
    }
    
    private async Task<string> ProcessObjectRecognitionAsync(Asset asset, CancellationToken cancellationToken)
    {
        // TODO: Integrate with ML library
        await Task.CompletedTask;
        _logger.LogInformation("Object recognition processing for asset {AssetId} - placeholder implementation", asset.Id);
        return "{}";
    }
    
    private async Task<string> ProcessSceneClassificationAsync(Asset asset, CancellationToken cancellationToken)
    {
        // TODO: Integrate with ML library
        await Task.CompletedTask;
        _logger.LogInformation("Scene classification processing for asset {AssetId} - placeholder implementation", asset.Id);
        return "{}";
    }
    
    private async Task<string> ProcessTextRecognitionAsync(Asset asset, CancellationToken cancellationToken)
    {
        // TODO: Integrate with ML library
        await Task.CompletedTask;
        _logger.LogInformation("Text recognition processing for asset {AssetId} - placeholder implementation", asset.Id);
        return "{}";
    }
}
