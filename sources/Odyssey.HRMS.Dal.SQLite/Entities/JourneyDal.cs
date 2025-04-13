using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Odyssey.HRMS.Dal.SQLite.Entities;

[Table("Journeys")]
internal sealed class JourneyDal
{
    [Key] 
    [Required]
    public Guid Id { get; set; }
    [Required]
    [StringLength(50)] 
    public string Name { get; set; } = null!;
    
    // [Column(TypeName = "jsonb")]
    // [DefaultValue("'[]'")]
    public JourneyActivityDal[] Activities { get; set; } = null!;
    [StringLength(10)] 
    public string Status { get; set; } = null!;
    // [Column(TypeName = "jsonb")]
    public JourneyStartupDal? Startup { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public long Version { get; set; }
    [DefaultValue(0)] 
    public int RowVersion { get; set; }

    internal static void Setup(EntityTypeBuilder<JourneyDal> builder)
    {
        // builder.Property(v=>v.Id).ValueGeneratedOnAdd();
        builder.Property(v => v.RowVersion).IsRowVersion();
        builder.Property(v => v.ChangedAt).HasConversion<DateTimeOffsetToBinaryConverter>();
        // builder.Property(v => v.Activities).HasDefaultValue(Array.Empty<JourneyActivityDal>());
        builder.OwnsMany(v => v.Activities).ToJson();
        
        // builder.OwnsMany(v => v.Activities, cb =>
        // {
        //     cb.ToJson();
        // });

        builder.OwnsOne(v => v.Startup, cb =>
        {
            cb.ToJson();
        });
    }
}

[Owned]
internal sealed class JourneyActivityDal
{
    public Guid ActivityId { get; set; }
}

[Owned]
internal sealed class JourneyStartupDal
{
    
}
