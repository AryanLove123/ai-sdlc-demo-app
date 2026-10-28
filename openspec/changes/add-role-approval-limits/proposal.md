# Proposal

## Why

The invoice approval flow does not enforce role-based approval limits. Today any user with the `Approver` or `Admin` role can approve an invoice of any amount, and an invoice has no record of who approved it beyond a single `DecidedBy` value. The business rules require limits by role and a second distinct Director approval for the largest invoices.

## What Changes

- Add two approval roles, `Manager` and `Director`, with demo users for each. The legacy `Approver` role is treated as `Manager`.
- Enforce limits at approval time: a Manager may approve up to and including 50,000; a Director up to and including 500,000.
- Above 500,000, require two distinct approvers. The first may be a Manager or a Director. The second must be a Director. The invoice stays `PendingApproval` after the first approval and becomes `Approved` only when the second is recorded.
- Compare approver usernames case-insensitively, so the same user never counts twice on one invoice.
- Keep an approval history on the invoice (who, in which role, when).
- A denied approval returns a clear reason (HTTP 403 with an `error` message) and changes nothing on the invoice.
- **BREAKING:** the `Admin` role no longer has approval authority (it is denied with a reason). `ApprovalService.Approve` takes the approver's role as a new parameter.
- Reject behaviour, invoice statuses and the 404/409/400 error mapping are unchanged.

## Capabilities

### New Capabilities

- `invoice-approval-limits`: role-based approval limits, two-approver rule, approval history and denial behaviour for invoice approvals.

### Modified Capabilities

- None

## Impact

- `backend/src/InvoiceApp.Domain`: `ApprovalService.cs` (new role parameter, rules), `Invoice.cs` (approval history), new `ApprovalPolicy.cs`, `ApprovalRecord.cs`, `ApprovalDeniedException.cs`.
- `backend/src/InvoiceApp.Api`: `ApprovalsController.cs` (passes the session role), `ApiControllerBase.cs` (maps the denial to 403), `appsettings.json` (demo Manager and Director users).
- `backend/tests/InvoiceApp.Tests`: `ApprovalServiceTests.cs` (existing `Approve` calls gain a role argument; new tests).
- `e2e/`: one Playwright test for a rejected over-limit approval.
- No change to `DiscountCalculator`, reject logic, or the invoice status enum.
