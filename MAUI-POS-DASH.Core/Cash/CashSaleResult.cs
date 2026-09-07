namespace MAUI_POS_DASH.Core.Cash;

public record CashSaleResult(Sale Sale, Transaction Transaction, decimal Change);
