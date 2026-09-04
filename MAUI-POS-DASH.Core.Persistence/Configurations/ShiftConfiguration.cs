namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure Shift's key, decimal precision, and its relationships to Attendant and Till.</summary>
public class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.HasKey(shift => shift.Id);
        builder.Property(shift => shift.OpeningFloat).HasPrecision(18, 2);
        builder.Property(shift => shift.ClosingCashCounted).HasPrecision(18, 2);

        builder.HasOne(shift => shift.Attendant)
            .WithMany()
            .HasForeignKey(shift => shift.AttendantId);

        builder.HasOne(shift => shift.Till)
            .WithOne()
            .HasForeignKey<Till>(till => till.ShiftId);
    }
}
