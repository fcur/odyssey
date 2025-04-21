using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Odyssey.HRMS.Dal.SQLite.Entities;

[Table("Journeys")]
internal sealed class JourneyDal
{
    [Key]
    public required Guid Id { get; set; }
    public required string Name { get; set; } = null!;
    public required JourneyActivityDal[] Activities { get; set; } = [];
    public required string Status { get; set; } = null!;
    public JourneyStartupDal? Startup { get; set; }
    public Dictionary<string, JsonElement>? InitializationData { get; set; }
    public required DateTimeOffset ChangedAt { get; set; }
    public required long Version { get; set; }
    public required long RowVersion { get; set; }
    
    internal static void Setup(EntityTypeBuilder<JourneyDal> builder)
    {
        builder.Property(v => v.Id).HasColumnType("BLOB").ValueGeneratedOnAdd().HasConversion<GuidToBlobConverter>().IsRequired();
        builder.Property(v => v.Name).HasMaxLength(50).IsRequired();
        builder.OwnsMany<JourneyActivityDal>(v => v.Activities, cb =>
        {
            cb.ToJson();
            cb.OwnsMany<JourneyActivityEventDal>(v=>v.Events);
        });
        builder.Property(v => v.Status).HasMaxLength(10).IsRequired();
        builder.OwnsOne(v => v.Startup).ToJson();
        builder.OwnsOne(v => v.InitializationData).ToJson();
        builder.Property(v => v.ChangedAt).HasConversion<DateTimeOffsetToBinaryConverter>().IsRequired();
        builder.Property(v => v.Version).IsRequired();
        builder.Property(v => v.RowVersion).IsRowVersion().HasDefaultValue(0);
    }
}

[Owned]
internal sealed class JourneyActivityDal
{
    public Guid Id { get; set; }
    
    public required string Name { get; set; }
    
    public required string Status { get; set; }
    public required JourneyActivityEventDal[] Events { get; set; } = [];
}

[Owned]
internal sealed class JourneyActivityEventDal
{
    public required string Name { get; set; }
    
    public required string Type { get; set; }
    
    public Guid? NextActivityId { get; set; }
}

[Owned]
internal sealed class JourneyStartupDal
{
    public DateTimeOffset? StartAt { get; set; }
    public string? RepeatingRule { get; set; }
}

public sealed class GuidToBlobConverter : ValueConverter<Guid, byte[]>
{
    // ReSharper disable once ConvertToPrimaryConstructor
    public GuidToBlobConverter() : base(x => x.ToByteArray(), y => new Guid(y)) { }
}