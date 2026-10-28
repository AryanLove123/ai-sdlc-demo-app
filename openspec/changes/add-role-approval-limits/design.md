# Design

## Context

`ApprovalService.Approve(invoiceId, approver)` approves any pending invoice for any caller and stores one `DecidedBy`. Roles today are `Approver` and `Admin`, set in `appsettings.json`; the session already carries the role. `InMemoryInvoiceRepository.GetById` and the test `FakeInvoiceRepository.GetById` return the stored object itself, so changing an invoice before the last check has passed would leak into storage even if the code then throws.

## Goals / Non-Goals

**Goals:**
- Enforce the Manager and Director limits and the two-approver rule above 500,000.
- Never count one user twice on an invoice (case-insensitive).
- A denial changes nothing and explains itself.
- Keep the existing lifecycle, error mapping and UI working.

**Non-Goals:**
- New invoice status values.
- Changing reject.
- Business logic in controllers, `Program.cs` or the web UI.
- Recording failed attempts anywhere on the invoice.
- Database persistence, bulk or partial approval (UC-1, UC-2).

## Decisions

- **Where:** a small `ApprovalPolicy` in `InvoiceApp.Domain` holds the limits as named constants (`ManagerLimit = 50_000m`, `DirectorLimit = 500_000m`). `ApprovalService.Approve` uses it.
- **Signature:** `Approve(string invoiceId, string approver, string approverRole)`. The controller passes `HttpContext.GetSession().Username` and `.Role`.
- **History:** new `ApprovalRecord(Approver, Role, ApprovedAt)` and `Invoice.Approvals` (a list). Two approvals are needed only above 500,000.
- **Order of checks:** find the invoice (404) → must be pending (409, existing) → approver not blank (400) → role may approve (denied) → amount rules. For the second approval above 500,000 the same-user check runs before the Director check.
- **Validate, then change once:** every check runs before any field is assigned. Only after the last check passes does the code append the record, set the status and call `Update`.
- **Denials:** a new `ApprovalDeniedException` carries the reason. It does not derive from `InvalidOperationException`, so 409 keeps meaning "wrong state". `ApiControllerBase.Handle` maps it to 403 with `{ error }`, which the UI already shows in the Approvals tab.
- **Roles:** `Manager` and `Director` are added. `Approver` is an alias for Manager. `Admin` stays allowed through `[RequireRole]` on `ApprovalsController`, so an Admin gets a clear domain reason instead of a bare 403.
- **Identity:** usernames are trimmed and compared with `StringComparison.OrdinalIgnoreCase`; the stored name keeps its original spelling.
- **Concurrency:** the check-and-write section of `Approve` runs under one lock, so two simultaneous requests cannot both pass the same-user check.
- **Increments:** increment 1 implements the single-approver limits and denies anything above 500,000 with "requires two approvers (not yet supported)". Increment 2 replaces that denial with the two-approver rule.

## Risks / Trade-offs

- [Risk] Changing the invoice before validating leaks into storage because repositories return live objects. → All checks first; tests compare the whole invoice before and after a denial.
- [Risk] Admin loses approval authority, which changes today's behaviour. → Called out as BREAKING in the proposal; confirm before merging.
- [Risk] The first approval finalises the invoice by mistake. → Status becomes Approved only when the second approval is recorded; tested.
- [Risk] Same-user and Director rules hide each other in tests. → Separate scenarios, with a different user for the Director test.
- [Risk] A lock is process-wide and only valid for the in-memory store. → Accepted for this app; revisit if a database is added.

## Migration Plan

1. Increment 1 (own commit): policy, exception, `Approvals` list, role parameter, limits, interim denial above 500,000, controller and demo users, updated existing tests, new tests. `dotnet test` green.
2. Increment 2 (own commit): two-approver rule, lock, new tests. `dotnet test` green.
3. Playwright test for a rejected over-limit approval.
4. Mutation testing: baseline taken before step 1, final after step 3.

## Open Questions

Assumptions taken as decided (change them here if the instructor says otherwise):
- `Approver` = Manager; `Admin` cannot approve; any other role is denied.
- The first approver above 500,000 may be a Manager or a Director.
- Limits apply to approving only.
- A denied attempt is not recorded on the invoice.
- Increment 1 denies everything above 500,000 until increment 2 lands.
