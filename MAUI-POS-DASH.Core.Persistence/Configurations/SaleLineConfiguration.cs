namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure SaleLine's key and the decimal precision of price and quantity.</summary>
public class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> builder)
    {
        builder.HasKey(line => line.Id);
        builder.Property(line => line.Description).IsRequired().HasMaxLength(200);
        builder.Property(line => line.UnitPrice).HasPrecision(18, 2);
        builder.Property(line => line.Quantity).HasPrecision(18, 3);
    }
}
