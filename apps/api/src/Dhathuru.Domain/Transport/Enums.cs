namespace Dhathuru.Domain.Transport;

public enum TransportMode { Ferry, Speedboat, Seaplane, DomesticFlight, Bus, Taxi, Walk, ResortTransfer }

/// <summary>How a provider is reached. Swapped by configuration, never by code.</summary>
public enum ProviderKind { Static, Mock, Live }

public enum PricingKind { Fixed, Estimate }

public enum ServiceRunState { OnTime, Delayed, Cancelled, Unknown }
