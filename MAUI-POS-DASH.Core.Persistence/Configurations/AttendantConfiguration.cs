namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure the Attendant table's key and required/length constraints.</summary>
public class AttendantConfiguration : IEntityTypeConfiguration<Attendant>
{
    public void Configure(EntityTypeBuilder<Attendant> builder)
    {
        builder.HasKey(attendant => attendant.Id);
        builder.Property(attendant => attendant.Name).IsRequired().HasMaxLength(100);
        builder.Property(attendant => attendant.PinHash).IsRequired();
    }
}
