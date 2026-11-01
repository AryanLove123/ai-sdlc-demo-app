using InvoiceApp.Domain;
using Xunit;

namespace InvoiceApp.Tests;

public class ApprovalServiceTests
{
    private static ApprovalService ServiceFor(params Invoice[] invoices) =>
        new(new FakeInvoiceRepository(invoices));

    private static (ApprovalService Service, FakeInvoiceRepository Repository) ServiceForWithRepository(params Invoice[] invoices)
    {
        var repository = new FakeInvoiceRepository(invoices);
        return (new ApprovalService(repository), repository);
    }

    private static Invoice InvoiceWith(string id, InvoiceStatus status, decimal amount = 100m) =>
    new() { Id = id, CustomerName = "Test School", Amount = amount, Status = status };

    private static Invoice PendingInvoice(decimal amount) =>
    InvoiceWith("INV-1", InvoiceStatus.PendingApproval, amount);

    private sealed record InvoiceSnapshot(
        InvoiceStatus Status, string? DecidedBy, DateTime? DecidedAt, string? DecisionNote,
        List<ApprovalRecord> Approvals);

    private static InvoiceSnapshot Snapshot(Invoice invoice) =>
        new(invoice.Status, invoice.DecidedBy, invoice.DecidedAt, invoice.DecisionNote, invoice.Approvals.ToList());

    private static void AssertUnchanged(InvoiceSnapshot before, Invoice invoice)
    {
        Assert.Equal(before.Status, invoice.Status);
        Assert.Equal(before.DecidedBy, invoice.DecidedBy);
        Assert.Equal(before.DecidedAt, invoice.DecidedAt);
        Assert.Equal(before.DecisionNote, invoice.DecisionNote);
        Assert.Equal(before.Approvals, invoice.Approvals);
    }

    private static void AssertDenied(Invoice invoice, string expectedMessage, Action attempt)
    {
        var before = Snapshot(invoice);

        var exception = Assert.Throws<ApprovalDeniedException>(attempt);

        Assert.Equal(expectedMessage, exception.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Submit_MovesDraftToPendingApproval()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);

        var result = ServiceFor(invoice).Submit("INV-1");

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
    }

    [Fact]
    public void Submit_RejectsInvoiceThatIsNotADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Approved));

        var exception = Assert.Throws<InvalidOperationException>(() => service.Submit("INV-1"));
        Assert.Equal("Invoice INV-1 is Approved; only drafts can be submitted.", exception.Message);
    }

    [Fact]
    public void Approve_SetsStatusApproverAndTimestamp()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Approve("INV-1", "demo.approver", ApprovalPolicy.ManagerRole);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.approver", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
    }

    [Fact]
    public void Approve_RejectsInvoiceThatIsStillADraft()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.Draft));

        Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.approver", ApprovalPolicy.ManagerRole));
    }

    [Fact]
    public void Approve_ThrowsWhenInvoiceDoesNotExist()
    {
        var service = ServiceFor();

        var exception = Assert.Throws<KeyNotFoundException>(() => service.Approve("INV-404", "demo.approver", ApprovalPolicy.ManagerRole));
        Assert.Equal("Invoice INV-404 was not found.", exception.Message);
    }

    [Fact]
    public void Reject_RecordsReasonAndApprover()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var result = service.Reject("INV-1", "demo.approver", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }

    [Fact]
    public void Reject_RequiresAReason()
    {
        var service = ServiceFor(InvoiceWith("INV-1", InvoiceStatus.PendingApproval));

        var exception = Assert.Throws<ArgumentException>(() => service.Reject("INV-1", "demo.approver", "  "));
        Assert.Contains("A reason is required to reject an invoice.", exception.Message);
    }

    [Fact]
    public void Approve_ManagerAt50000_IsApproved()
    {
        var invoice = PendingInvoice(50_000m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.manager", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Single(result.Approvals);
        Assert.Equal("demo.manager", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.ManagerRole, result.Approvals[0].Role);
    }

    [Fact]
    public void Approve_ManagerAt50000Point01_IsDenied()
    {
        var invoice = PendingInvoice(50_000.01m);
        var service = ServiceFor(invoice);

        AssertDenied(invoice, "A Manager may approve invoices up to 50,000.",
            () => service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole));
    }

    [Fact]
    public void Approve_ManagerAt50001_IsDenied()
    {
        var invoice = PendingInvoice(50_001m);
        var service = ServiceFor(invoice);

        AssertDenied(invoice, "A Manager may approve invoices up to 50,000.",
            () => service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole));
    }

    [Fact]
    public void Approve_DirectorAt50001_IsApproved()
    {
        var invoice = PendingInvoice(50_001m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Single(result.Approvals);
        Assert.Equal("demo.director", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[0].Role);
    }

    [Fact]
    public void Approve_DirectorAt500000_IsApproved()
    {
        var invoice = PendingInvoice(500_000m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Single(result.Approvals);
        Assert.Equal("demo.director", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[0].Role);
    }

    [Fact]
    public void Approve_ManagerAt500000_IsDenied()
    {
        var invoice = PendingInvoice(500_000m);
        var service = ServiceFor(invoice);

        AssertDenied(invoice, "A Manager may approve invoices up to 50,000.",
            () => service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole));
    }

    [Fact]
    public void Approve_Admin_IsDenied()
    {
        var invoice = PendingInvoice(100m);
        var service = ServiceFor(invoice);

        AssertDenied(invoice, "Role 'Admin' may not approve invoices.",
            () => service.Approve("INV-1", "demo.admin", "Admin"));
    }

    [Fact]
    public void Approve_ApproverRole_IsDenied()
    {
        var invoice = PendingInvoice(100m);
        var service = ServiceFor(invoice);

        AssertDenied(invoice, "Role 'Approver' may not approve invoices.",
            () => service.Approve("INV-1", "demo.approver", "Approver"));
    }

    [Fact]
    public void Approve_LowercaseManagerRole_IsDenied()
    {
        var invoice = PendingInvoice(100m);
        var service = ServiceFor(invoice);

        AssertDenied(invoice, "Role 'manager' may not approve invoices.",
            () => service.Approve("INV-1", "demo.manager", "manager"));
    }

    [Fact]
    public void Approve_UnknownRole_IsDenied()
    {
        var invoice = PendingInvoice(100m);
        var service = ServiceFor(invoice);

        AssertDenied(invoice, "Role 'Clerk' may not approve invoices.",
            () => service.Approve("INV-1", "demo.clerk", "Clerk"));
    }

    [Fact]
    public void Approve_AdminOnLargeInvoice_UsesRoleMessageBeforeAmount()
    {
        var invoice = PendingInvoice(1_000_000m);
        var service = ServiceFor(invoice);

        AssertDenied(invoice, "Role 'Admin' may not approve invoices.",
            () => service.Approve("INV-1", "demo.admin", "Admin"));
    }

    [Fact]
    public void Approve_BlankApproverName_ThrowsAndLeavesInvoiceUnchanged()
    {
        var invoice = PendingInvoice(100m);
        var service = ServiceFor(invoice);
        var before = Snapshot(invoice);

        var exception = Assert.Throws<ArgumentException>(() => service.Approve("INV-1", "  ", ApprovalPolicy.ManagerRole));

        Assert.Contains("Approver username is required.", exception.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Approve_ApprovedInvoiceByManager_ThrowsAndLeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Approved, 100m);
        var service = ServiceFor(invoice);
        var before = Snapshot(invoice);

        var exception = Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole));

        Assert.Equal("Invoice INV-1 is Approved; only pending invoices can be decided.", exception.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Approve_ApprovedInvoiceByAdmin_ThrowsInvalidOperationBeforeRoleCheck()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Approved, 100m);
        var service = ServiceFor(invoice);
        var before = Snapshot(invoice);

        var exception = Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.admin", "Admin"));

        Assert.Equal("Invoice INV-1 is Approved; only pending invoices can be decided.", exception.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Approve_DirectorFirstApprovalAbove500000_KeepsInvoicePending()
    {
        var invoice = PendingInvoice(500_001m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Null(result.DecisionNote);
        Assert.Single(result.Approvals);
        Assert.Equal("demo.director", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[0].Role);
    }

    [Fact]
    public void Approve_ManagerFirstApprovalAbove500000_KeepsInvoicePending()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);

        var result = service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole);

        Assert.Equal(InvoiceStatus.PendingApproval, result.Status);
        Assert.Null(result.DecidedBy);
        Assert.Null(result.DecidedAt);
        Assert.Null(result.DecisionNote);
        Assert.Single(result.Approvals);
        Assert.Equal("demo.manager", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.ManagerRole, result.Approvals[0].Role);
    }

    [Fact]
    public void Approve_ManagerThenDifferentDirectorAbove500000_CompletesApproval()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);

        service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole);
        var result = service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(2, result.Approvals.Count);
        Assert.Equal("demo.manager", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.ManagerRole, result.Approvals[0].Role);
        Assert.Equal("demo.director", result.Approvals[1].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[1].Role);
    }

    [Fact]
    public void Approve_DirectorThenDifferentDirectorAbove500000_CompletesApproval()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);

        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);
        var result = service.Approve("INV-1", "demo.director2", ApprovalPolicy.DirectorRole);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director2", result.DecidedBy);
        Assert.NotNull(result.DecidedAt);
        Assert.Equal(2, result.Approvals.Count);
        Assert.Equal("demo.director", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[0].Role);
        Assert.Equal("demo.director2", result.Approvals[1].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[1].Role);
    }

    [Fact]
    public void Approve_SameDirectorSecondApprovalAbove500000_IsDenied()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        AssertDenied(invoice, "User 'demo.director' has already approved invoice INV-1.",
            () => service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole));
    }

    [Fact]
    public void Approve_SameDirectorSecondApprovalDifferentCaseAbove500000_IsDenied()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        AssertDenied(invoice, "User 'DEMO.Director' has already approved invoice INV-1.",
            () => service.Approve("INV-1", " DEMO.Director ", ApprovalPolicy.DirectorRole));
    }

    [Fact]
    public void Approve_DifferentUserManagerSecondApprovalAbove500000_IsDenied()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        AssertDenied(invoice, "The second approver of an invoice above 500,000 must be a Director.",
            () => service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole));
    }

    [Fact]
    public void Approve_SameManagerSecondApprovalAbove500000_UsesSameUserMessage()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole);

        AssertDenied(invoice, "User 'demo.manager' has already approved invoice INV-1.",
            () => service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole));
    }

    [Fact]
    public void Approve_DeniedManagerSecondApprovalAbove500000_IsFollowedByValidDirectorApproval()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        AssertDenied(invoice, "The second approver of an invoice above 500,000 must be a Director.",
            () => service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole));

        var result = service.Approve("INV-1", "demo.director2", ApprovalPolicy.DirectorRole);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director2", result.DecidedBy);
        Assert.Equal(2, result.Approvals.Count);
        Assert.Equal("demo.director", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[0].Role);
        Assert.Equal("demo.director2", result.Approvals[1].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[1].Role);
    }

    [Fact]
    public void Approve_DeniedSameUserSecondApprovalAbove500000_IsFollowedByValidDirectorApproval()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        AssertDenied(invoice, "User 'demo.director' has already approved invoice INV-1.",
            () => service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole));

        var result = service.Approve("INV-1", "demo.director2", ApprovalPolicy.DirectorRole);

        Assert.Equal(InvoiceStatus.Approved, result.Status);
        Assert.Equal("demo.director2", result.DecidedBy);
        Assert.Equal(2, result.Approvals.Count);
        Assert.Equal("demo.director", result.Approvals[0].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[0].Role);
        Assert.Equal("demo.director2", result.Approvals[1].Approver);
        Assert.Equal(ApprovalPolicy.DirectorRole, result.Approvals[1].Role);
    }

    [Fact]
    public void Approve_ThirdApprovalAfterApprovalIsAlreadyComplete_ThrowsAndLeavesInvoiceUnchanged()
    {
        var invoice = PendingInvoice(600_000m);
        var service = ServiceFor(invoice);
        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);
        service.Approve("INV-1", "demo.director2", ApprovalPolicy.DirectorRole);
        var before = Snapshot(invoice);

        var exception = Assert.Throws<InvalidOperationException>(() => service.Approve("INV-1", "demo.director3", ApprovalPolicy.DirectorRole));

        Assert.Equal("Invoice INV-1 is Approved; only pending invoices can be decided.", exception.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Reject_OnApprovedInvoice_ThrowsAndLeavesInvoiceUnchanged()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Approved, 100m);
        var service = ServiceFor(invoice);
        var before = Snapshot(invoice);

        var exception = Assert.Throws<InvalidOperationException>(() => service.Reject("INV-1", "demo.manager", "Duplicate invoice"));

        Assert.Equal("Invoice INV-1 is Approved; only pending invoices can be decided.", exception.Message);
        AssertUnchanged(before, invoice);
    }

    [Fact]
    public void Submit_SavesInvoiceOnce()
    {
        var invoice = InvoiceWith("INV-1", InvoiceStatus.Draft);
        var (service, repository) = ServiceForWithRepository(invoice);

        service.Submit("INV-1");

        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_ManagerAt40000_SavesInvoiceOnce()
    {
        var invoice = PendingInvoice(40_000m);
        var (service, repository) = ServiceForWithRepository(invoice);

        service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole);

        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_ManagerFirstApprovalAbove500000_SavesInvoiceOnce()
    {
        var invoice = PendingInvoice(600_000m);
        var (service, repository) = ServiceForWithRepository(invoice);

        service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole);

        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_TwoValidApprovalsAbove500000_SavesInvoiceTwice()
    {
        var invoice = PendingInvoice(600_000m);
        var (service, repository) = ServiceForWithRepository(invoice);

        service.Approve("INV-1", "demo.manager", ApprovalPolicy.ManagerRole);
        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        Assert.Equal(2, repository.UpdateCallCount);
    }

    [Fact]
    public void Reject_SavesInvoiceOnce()
    {
        var invoice = PendingInvoice(75_000m);
        var (service, repository) = ServiceForWithRepository(invoice);

        service.Reject("INV-1", "demo.manager", "Duplicate invoice");

        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public void Approve_DeniedSecondApprovalAbove500000_DoesNotSave()
    {
        var invoice = PendingInvoice(600_000m);
        var (service, repository) = ServiceForWithRepository(invoice);
        service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole);

        Assert.Throws<ApprovalDeniedException>(() => service.Approve("INV-1", "demo.director", ApprovalPolicy.DirectorRole));

        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public void Reject_IsNotLimitedByAmount()
    {
        var invoice = PendingInvoice(75_000m);
        var service = ServiceFor(invoice);

        var result = service.Reject("INV-1", "demo.manager", "Duplicate invoice");

        Assert.Equal(InvoiceStatus.Rejected, result.Status);
        Assert.Equal("Duplicate invoice", result.DecisionNote);
    }
}
