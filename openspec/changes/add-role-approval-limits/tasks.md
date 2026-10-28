# Tasks

## 1. Single-approver limits (increment 1, own commit)

- [ ] 1.1 Add `ApprovalPolicy` (named limit constants), `ApprovalRecord`, `Invoice.Approvals` and `ApprovalDeniedException` in `InvoiceApp.Domain`.
- [ ] 1.2 Change `ApprovalService.Approve` to take the approver's role. Apply the check order from design.md. Enforce Manager up to 50,000 (inclusive) and Director up to 500,000 (inclusive), treat `Approver` as Manager, and deny any other role. Deny anything above 500,000 with "requires two approvers (not yet supported)". Change the invoice only after every check has passed.
- [ ] 1.3 In the API: pass the session role from `ApprovalsController`, map `ApprovalDeniedException` to 403 `{ error }` in `ApiControllerBase`, and add `demo.manager`, `demo.director` and `demo.director2` to `appsettings.json` and `[RequireRole]`.
- [ ] 1.4 Update the three existing `Approve` tests for the new role argument without changing what they check.
- [ ] 1.5 Add unit tests for spec scenarios in requirement 1: exactly 50,000, 50,000.01, 50,001, Director at 50,001, exactly 500,000, Manager at 500,000, legacy Approver, Admin and unknown role, the interim denial above 500,000. Each denial test asserts the whole invoice is unchanged and checks the reason text.
- [ ] 1.6 Run `dotnet test backend/InvoiceApp.sln` (all green), then check by hand in the UI as `demo.manager`: a small invoice is approved, an invoice over 50,000 shows the reason. Break the `<=` temporarily to see a test fail, then restore it.

## 2. Two-approver rule (increment 2, own commit)

- [ ] 2.1 Replace the interim denial above 500,000 with the rule: first approval by a Manager or Director is recorded and the invoice stays PendingApproval; the second must be a different user and a Director; then set Approved, DecidedBy and DecidedAt. Run the check-and-write section under one lock.
- [ ] 2.2 Add unit tests for the scenarios in requirement 2: first approval by Director and by Manager, completion by a different Director, same user twice, case-variant username, Manager as second approver (different user), Manager A again (same-user reason wins), and a valid completion after a denied attempt. Each denial asserts the whole invoice is unchanged.
- [ ] 2.3 Add tests for requirement 3 and 4: not-pending invoice still gives the existing 409 error, and reject is not limited by amount.
- [ ] 2.4 Run `dotnet test backend/InvoiceApp.sln` (all green). Delete the same-user check, then the Director check, and confirm a different test fails each time.

## 3. Verification

- [ ] 3.1 Add one Playwright test with page objects: sign in as `demo.manager`, create a 75,000 invoice, submit it, try to approve it in the Approvals tab, expect the limit reason, and confirm the invoice is still Pending Approval.
- [ ] 3.2 Run mutation testing on the approval code with the same scope as the baseline, record both scores, and close surviving mutants at the boundaries and identity checks.

## Workflow follow-up

- Review the generated artifacts for alignment with the spec and design constraints.
- Archive the change when the project review and approval process is complete.
