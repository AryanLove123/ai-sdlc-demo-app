namespace InvoiceApp.Domain;

public class ApprovalDeniedException : Exception
{
    public ApprovalDeniedException(string message) : base(message)
    {
    }
}
