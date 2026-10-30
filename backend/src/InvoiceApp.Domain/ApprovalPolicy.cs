namespace InvoiceApp.Domain;

public static class ApprovalPolicy
{
    public const string ManagerRole = "Manager";
    public const string DirectorRole = "Director";
    public const decimal ManagerLimit = 50_000m;
    public const decimal DirectorLimit = 500_000m;
}
