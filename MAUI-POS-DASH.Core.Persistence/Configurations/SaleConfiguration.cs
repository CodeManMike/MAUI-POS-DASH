namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure Sale's key and its one-to-many relationship to SaleLine.</summary>
public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.HasKey(sale => sale.Id);
        builder.HasMany(sale => sale.Lines)
            .WithOne()
            .HasForeignKey(line => line.SaleId);
    }
}
