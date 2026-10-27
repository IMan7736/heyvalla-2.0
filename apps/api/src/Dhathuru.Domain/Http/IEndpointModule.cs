namespace Dhathuru.Domain.Http;

/// <summary>
/// A module's HTTP surface. Register with <c>services.AddSingleton&lt;IEndpointModule, ...&gt;()</c>;
/// the API host maps every registered module under <c>/v1</c> (so the host never has to know module internals).
/// </summary>
public interface IEndpointModule
{
    void MapEndpoints(RouteGroupBuilder v1);
}
