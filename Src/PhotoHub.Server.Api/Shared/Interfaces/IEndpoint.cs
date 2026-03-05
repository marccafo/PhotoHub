namespace PhotoHub.Server.Api.Shared.Interfaces;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder routeGroupBuilder);
}