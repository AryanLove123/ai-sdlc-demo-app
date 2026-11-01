using System.Globalization;
namespace InvoiceApp.Domain;

/// <summary>
/// Moves an invoice through its lifecycle: Draft -> PendingApproval -> Approved or Rejected.
/// One invoice at a time, decided as a whole by a single approver.
/// </summary>
public class ApprovalService
{
    private static readonly object ApprovalLock = new();
    private readonly IInvoiceRepository _repository;

    public ApprovalService(IInvoiceRepository repository)
    {
        _repository = repository;
    }

    public Invoice Submit(string invoiceId)
    {
        var invoice = Find(invoiceId);

        if (invoice.Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Invoice {invoiceId} is {invoice.Status}; only drafts can be submitted.");
        }

        invoice.Status = InvoiceStatus.PendingApproval;
        _repository.Update(invoice);
        return invoice;
    }

    public Invoice Approve(string invoiceId, string approver, string approverRole)
    {
        lock (ApprovalLock)
        {
            var invoice = Find(invoiceId);
            EnsurePending(invoice);

            if (string.IsNullOrWhiteSpace(approver))
            {
                throw new ArgumentException("Approver username is required.", nameof(approver));
            }

            var trimmedApprover = approver.Trim();
            ValidateRoleCanApprove(approverRole);

            if (invoice.Amount <= ApprovalPolicy.DirectorLimit)
            {
                ValidateAmountCanApprove(approverRole, invoice.Amount);

                var approvedAt = DateTime.UtcNow;
                invoice.Status = InvoiceStatus.Approved;
                invoice.DecidedBy = trimmedApprover;
                invoice.DecidedAt = approvedAt;
                invoice.Approvals.Add(new ApprovalRecord(trimmedApprover, approverRole, approvedAt));
                _repository.Update(invoice);
                return invoice;
            }

            if (invoice.Approvals.Count == 0)
            {
                invoice.Approvals.Add(new ApprovalRecord(trimmedApprover, approverRole, DateTime.UtcNow));
                _repository.Update(invoice);
                return invoice;
            }

            var firstApprover = invoice.Approvals[0].Approver;
            if (string.Equals(trimmedApprover, firstApprover, StringComparison.OrdinalIgnoreCase))
            {
                throw new ApprovalDeniedException($"User '{trimmedApprover}' has already approved invoice {invoice.Id}.");
            }

            if (approverRole != ApprovalPolicy.DirectorRole)
            {
                throw new ApprovalDeniedException(
                    "The second approver of an invoice above 500,000 must be a Director.");
            }

            var secondApprovedAt = DateTime.UtcNow;
            invoice.Status = InvoiceStatus.Approved;
            invoice.DecidedBy = trimmedApprover;
            invoice.DecidedAt = secondApprovedAt;
            invoice.Approvals.Add(new ApprovalRecord(trimmedApprover, approverRole, secondApprovedAt));
            _repository.Update(invoice);
            return invoice;
        }
    }
    public Invoice Reject(string invoiceId, string approver, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject an invoice.", nameof(reason));
        }

        var invoice = Find(invoiceId);
        EnsurePending(invoice);

        invoice.Status = InvoiceStatus.Rejected;
        invoice.DecidedBy = approver;
        invoice.DecidedAt = DateTime.UtcNow;
        invoice.DecisionNote = reason;
        _repository.Update(invoice);
        return invoice;
    }

    private Invoice Find(string invoiceId) =>
        _repository.GetById(invoiceId)
        ?? throw new KeyNotFoundException($"Invoice {invoiceId} was not found.");

    private static void EnsurePending(Invoice invoice)
    {
        if (invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException(
                $"Invoice {invoice.Id} is {invoice.Status}; only pending invoices can be decided.");
        }
    }

    private static void ValidateRoleCanApprove(string approverRole)
    {
        if (approverRole != ApprovalPolicy.ManagerRole && approverRole != ApprovalPolicy.DirectorRole)
        {
            throw new ApprovalDeniedException($"Role '{approverRole}' may not approve invoices.");
        }
    }

    private static void ValidateAmountCanApprove(string approverRole, decimal invoiceAmount)
    {
        var limit = approverRole == ApprovalPolicy.DirectorRole
            ? ApprovalPolicy.DirectorLimit
            : ApprovalPolicy.ManagerLimit;

        if (invoiceAmount > limit)
        {
            throw new ApprovalDeniedException(
                $"A {approverRole} may approve invoices up to {FormatAmount(limit)}.");
        }
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture);
}
