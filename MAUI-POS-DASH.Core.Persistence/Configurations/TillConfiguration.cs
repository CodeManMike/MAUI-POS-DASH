namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure Till's key and the decimal precision of its three running totals.</summary>
public class TillConfiguration : IEntityTypeConfiguration<Till>
{
    public void Configure(EntityTypeBuilder<Till> builder)
    {
        builder.HasKey(till => till.Id);
        builder.Property(till => till.CashTotal).HasPrecision(18, 2);
        builder.Property(till => till.FleetCardTotal).HasPrecision(18, 2);
        builder.Property(till => till.MobileMoneyTotal).HasPrecision(18, 2);
    }
}
