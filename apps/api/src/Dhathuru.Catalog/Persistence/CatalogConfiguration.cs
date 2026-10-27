using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dhathuru.Catalog.Persistence;

internal static class JsonbComparer
{
    /// <summary>Postgres jsonb rewrites whitespace and key order, so compare parsed JSON, not text.</summary>
    public static readonly Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<string> Instance = new(
        (a, b) => Equal(a, b), v => System.Text.Json.Nodes.JsonNode.Parse(v)!.ToJsonString().GetHashCode(), v => v);

    private static bool Equal(string? a, string? b) =>
        a is not null && b is not null && System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(a), System.Text.Json.Nodes.JsonNode.Parse(b));
}

internal sealed class PlaceConfiguration : IEntityTypeConfiguration<PlaceRow>
{
    public void Configure(EntityTypeBuilder<PlaceRow> b)
    {
        b.ToTable("places");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(30);
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.Property(x => x.Type).HasMaxLength(20).IsRequired();
        b.Property(x => x.NamesJson).HasColumnType("jsonb").IsRequired().Metadata.SetValueComparer(JsonbComparer.Instance);
        b.Property(x => x.Location).HasColumnType("geometry(Point,4326)").IsRequired()
            .Metadata.SetValueComparer(new Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer<NetTopologySuite.Geometries.Point>(
                (a, c) => a != null && c != null && a.X == c.X && a.Y == c.Y,
                p => HashCode.Combine(p.X, p.Y),
                p => p.Copy() as NetTopologySuite.Geometries.Point ?? p));
        b.Property(x => x.AtollId).HasMaxLength(30);
        b.Property(x => x.ParentId).HasMaxLength(30);
        b.Property(x => x.Code).HasMaxLength(8);
        b.Property(x => x.Source).HasMaxLength(40).IsRequired();
        b.Property(x => x.ZoneKey).HasMaxLength(40);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => x.Type);
        b.HasIndex(x => x.Location).HasMethod("gist");
    }
}

internal sealed class OperatorConfiguration : IEntityTypeConfiguration<OperatorRow>
{
    public void Configure(EntityTypeBuilder<OperatorRow> b)
    {
        b.ToTable("operators");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(30);
        b.Property(x => x.Slug).HasMaxLength(80).IsRequired();
        b.Property(x => x.NamesJson).HasColumnType("jsonb").IsRequired().Metadata.SetValueComparer(JsonbComparer.Instance);
        b.Property(x => x.ProviderId).HasMaxLength(60).IsRequired();
        b.Property(x => x.ProviderKind).HasMaxLength(10).IsRequired();
        b.Property(x => x.Website).HasMaxLength(200);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

internal sealed class ServiceConfiguration : IEntityTypeConfiguration<ServiceRow>
{
    public void Configure(EntityTypeBuilder<ServiceRow> b)
    {
        b.ToTable("services");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(30);
        b.Property(x => x.Slug).HasMaxLength(120).IsRequired();
        b.Property(x => x.OperatorId).HasMaxLength(30).IsRequired();
        b.Property(x => x.Mode).HasMaxLength(20).IsRequired();
        b.Property(x => x.FromId).HasMaxLength(30);
        b.Property(x => x.ToId).HasMaxLength(30);
        b.Property(x => x.Pricing).HasMaxLength(10).IsRequired();
        b.Property(x => x.FrequencyFirst).HasMaxLength(5);
        b.Property(x => x.FrequencyLast).HasMaxLength(5);
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasIndex(x => new { x.FromId, x.ToId });
    }
}

internal sealed class MinTransferConfiguration : IEntityTypeConfiguration<MinTransferRow>
{
    public void Configure(EntityTypeBuilder<MinTransferRow> b)
    {
        b.ToTable("min_transfers");
        b.HasKey(x => new { x.PlaceId, x.FromMode, x.ToMode });
        b.Property(x => x.PlaceId).HasMaxLength(30);
        b.Property(x => x.FromMode).HasMaxLength(20);
        b.Property(x => x.ToMode).HasMaxLength(20);
    }
}

internal sealed class LinkConfiguration : IEntityTypeConfiguration<LinkRow>
{
    public void Configure(EntityTypeBuilder<LinkRow> b)
    {
        b.ToTable("place_links");
        b.HasKey(x => new { x.FromId, x.ToId, x.Mode });
        b.Property(x => x.FromId).HasMaxLength(30);
        b.Property(x => x.ToId).HasMaxLength(30);
        b.Property(x => x.Mode).HasMaxLength(20);
        b.Property(x => x.Pricing).HasMaxLength(10).IsRequired();
    }
}

internal sealed class ReliabilityPriorConfiguration : IEntityTypeConfiguration<ReliabilityPriorRow>
{
    public void Configure(EntityTypeBuilder<ReliabilityPriorRow> b)
    {
        b.ToTable("reliability_priors");
        b.HasKey(x => x.Mode);
        b.Property(x => x.Mode).HasMaxLength(20);
    }
}

internal sealed class TaxiFareConfiguration : IEntityTypeConfiguration<TaxiFareRow>
{
    public void Configure(EntityTypeBuilder<TaxiFareRow> b)
    {
        b.ToTable("taxi_fares");
        b.HasKey(x => new { x.FromZone, x.ToZone });
        b.Property(x => x.FromZone).HasMaxLength(40);
        b.Property(x => x.ToZone).HasMaxLength(40);
    }
}

internal sealed class TaxiDurationConfiguration : IEntityTypeConfiguration<TaxiDurationRow>
{
    public void Configure(EntityTypeBuilder<TaxiDurationRow> b)
    {
        b.ToTable("taxi_durations");
        b.HasKey(x => new { x.FromZone, x.ToZone, x.Band });
        b.Property(x => x.FromZone).HasMaxLength(40);
        b.Property(x => x.ToZone).HasMaxLength(40);
        b.Property(x => x.Band).HasMaxLength(20);
    }
}

internal sealed class ModelParamConfiguration : IEntityTypeConfiguration<ModelParamRow>
{
    public void Configure(EntityTypeBuilder<ModelParamRow> b)
    {
        b.ToTable("model_params");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(60);
        b.Property(x => x.JsonValue).HasColumnType("jsonb").IsRequired().Metadata.SetValueComparer(JsonbComparer.Instance);
    }
}
