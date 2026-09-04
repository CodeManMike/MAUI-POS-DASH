namespace MAUI_POS_DASH.Core.Persistence.Configurations;

/// <summary>We configure Transaction's key, amount precision, and its relationship to Sale.</summary>
public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.HasKey(transaction => transaction.Id);
        builder.Property(transaction => transaction.Amount).HasPrecision(18, 2);

        builder.HasOne(transaction => transaction.Sale)
            .WithMany()
            .HasForeignKey(transaction => transaction.SaleId);
    }
}
