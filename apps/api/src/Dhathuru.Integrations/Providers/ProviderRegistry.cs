using System.Collections.Concurrent;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Dhathuru.Integrations.Providers;

/// <summary>A real operator integration. Register one per provider id with <c>services.AddSingleton&lt;ILiveProviderFactory, ...&gt;()</c>.</summary>
public interface ILiveProviderFactory
{
    string ProviderId { get; }

    ITransportProvider Create(IServiceProvider services);
}

public sealed class ProviderRegistry(
    IConfiguration configuration,
    TimetableDirectory directory,
    DisruptionBoard disruptions,
    IEnumerable<ILiveProviderFactory> liveFactories,
    IServiceProvider services,
    TimeProvider time,
    ILoggerFactory loggers) : IProviderRegistry
{
    private readonly ConcurrentDictionary<string, ITransportProvider> providers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ILiveProviderFactory> live = liveFactories.ToDictionary(f => f.ProviderId, StringComparer.Ordinal);

    public ITransportProvider Get(string providerId) => providers.GetOrAdd(providerId, Create);

    public ProviderKind ConfiguredKind(string providerId)
    {
        var value = configuration[$"Providers:{providerId}:Kind"] ?? configuration["Providers:Default:Kind"] ?? "mock";
        return Enum.TryParse<ProviderKind>(value, ignoreCase: true, out var kind)
            ? kind
            : throw new InvalidOperationException($"Providers:{providerId}:Kind '{value}' is not static, mock or live.");
    }

    private ITransportProvider Create(string providerId)
    {
        var @static = new StaticTimetableProvider(providerId, directory, time);
        switch (ConfiguredKind(providerId))
        {
            case ProviderKind.Static:
                return @static;
            case ProviderKind.Mock:
                return new MockTransportProvider(providerId, directory, disruptions, time);
            default:
                if (!live.TryGetValue(providerId, out var factory))
                {
                    loggers.CreateLogger<ProviderRegistry>().LogWarning(
                        "Provider {ProviderId} is configured as live but no live adapter is registered; using the static timetable", providerId);
                    return @static;
                }
                var options = configuration.GetSection($"Providers:{providerId}:Resilience").Get<ResilientOptions>() ?? new ResilientOptions();
                return new ResilientProvider(factory.Create(services), @static, options, time, loggers.CreateLogger<ResilientProvider>());
        }
    }
}
