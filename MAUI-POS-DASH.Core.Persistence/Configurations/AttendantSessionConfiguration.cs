namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure AttendantSession's key and its relationship to Attendant.</summary>
public class AttendantSessionConfiguration : IEntityTypeConfiguration<AttendantSession>
{
    public void Configure(EntityTypeBuilder<AttendantSession> builder)
    {
        builder.HasKey(session => session.Id);

        builder.HasOne(session => session.Attendant)
            .WithMany()
            .HasForeignKey(session => session.AttendantId);
    }
}
