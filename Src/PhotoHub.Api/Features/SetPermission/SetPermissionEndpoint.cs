using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Data;
using PhotoHub.API.Shared.Interfaces;
using PhotoHub.API.Shared.Models;

namespace PhotoHub.API.Features.SetPermission;

public class SetPermissionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/folders/{folderId}/permissions", Handle)
            .WithName("SetPermission")
            .WithTags("Folders")
            .WithDescription("Sets or updates folder permissions for a user")
            .AddOpenApiOperationTransformer((operation, context, ct) =>
            {
                operation.Summary = "Set folder permission";
                operation.Description = "Creates or updates folder permissions for a user. If a permission already exists, it will be updated with the new values.";
                return Task.CompletedTask;
            });
    }

    private async Task<IResult> Handle(
        [FromServices] PhotoDbContext dbContext,
        [FromRoute] int folderId,
        [FromBody] SetPermissionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Validate user exists
            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
            
            if (user == null)
            {
                return Results.NotFound(new { error = $"User with ID {request.UserId} not found" });
            }

            // Validate folder exists
            var folder = await dbContext.Folders
                .FirstOrDefaultAsync(f => f.Id == folderId, cancellationToken);
            
            if (folder == null)
            {
                return Results.NotFound(new { error = $"Folder with ID {folderId} not found" });
            }

            // Validate granted by user if provided
            if (request.GrantedByUserId.HasValue)
            {
                var grantedByUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == request.GrantedByUserId.Value, cancellationToken);
                
                if (grantedByUser == null)
                {
                    return Results.BadRequest(new { error = $"GrantedByUser with ID {request.GrantedByUserId.Value} not found" });
                }
            }

            // Check if permission already exists
            var existingPermission = await dbContext.FolderPermissions
                .FirstOrDefaultAsync(
                    p => p.UserId == request.UserId && p.FolderId == folderId,
                    cancellationToken);

            FolderPermission permission;

            if (existingPermission != null)
            {
                // Update existing permission
                existingPermission.CanRead = request.CanRead;
                existingPermission.CanWrite = request.CanWrite;
                existingPermission.CanDelete = request.CanDelete;
                existingPermission.CanManagePermissions = request.CanManagePermissions;
                existingPermission.GrantedByUserId = request.GrantedByUserId;
                existingPermission.GrantedAt = DateTime.UtcNow;
                
                permission = existingPermission;
            }
            else
            {
                // Create new permission
                permission = new FolderPermission
                {
                    UserId = request.UserId,
                    FolderId = folderId,
                    CanRead = request.CanRead,
                    CanWrite = request.CanWrite,
                    CanDelete = request.CanDelete,
                    CanManagePermissions = request.CanManagePermissions,
                    GrantedByUserId = request.GrantedByUserId,
                    GrantedAt = DateTime.UtcNow
                };
                
                dbContext.FolderPermissions.Add(permission);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Load related data for response
            await dbContext.Entry(permission)
                .Reference(p => p.User)
                .LoadAsync(cancellationToken);
            
            await dbContext.Entry(permission)
                .Reference(p => p.Folder)
                .LoadAsync(cancellationToken);

            var grantedByUsername = permission.GrantedByUserId.HasValue
                ? await dbContext.Users
                    .Where(u => u.Id == permission.GrantedByUserId.Value)
                    .Select(u => u.Username)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            var response = new SetPermissionResponse
            {
                Id = permission.Id,
                UserId = permission.UserId,
                Username = permission.User.Username,
                FolderId = permission.FolderId,
                FolderPath = permission.Folder.Path,
                CanRead = permission.CanRead,
                CanWrite = permission.CanWrite,
                CanDelete = permission.CanDelete,
                CanManagePermissions = permission.CanManagePermissions,
                GrantedAt = permission.GrantedAt,
                GrantedByUserId = permission.GrantedByUserId,
                GrantedByUsername = grantedByUsername
            };

            return Results.Ok(response);
        }
        catch (DbUpdateException ex)
        {
            return Results.Problem(
                detail: $"Database error: {ex.Message}",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
        catch (Exception ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError
            );
        }
    }
}

