# Project conventions for AI agents

Invoice approval demo app: ASP.NET Core 10 Web API with a small static web UI.

## Layout

- `backend/src/InvoiceApp.Domain`: business rules, no web or storage code. `ApprovalService` owns the invoice lifecycle (Draft, PendingApproval, Approved, Rejected).
- `backend/src/InvoiceApp.Api`: controllers, sign-in (`Auth/`), in-memory repository, and the web UI under `wwwroot/`.
- `backend/tests/InvoiceApp.Tests`: xUnit tests, one test class per class under test, named `<ClassUnderTest>Tests`.
- `e2e`: Playwright end-to-end tests (`tests/`) and page objects (`pages/`).
- `docs/use-cases`: short briefs for features we may build.

## Conventions

- Controllers live in `Controllers/`, one per resource, route `api/[controller]`. Request bodies are `record` types declared inside the controller.
- Controllers inherit `ApiControllerBase` and use `Handle(...)` so service errors map to 404, 409 and 400.
- Every endpoint is behind sign-in. Restrict actions with `[RequireRole("...")]`: destructive actions are Admin only, approvals are Approver or Admin.
- Business rules go in the Domain project, never in controllers.
- Register services in `Program.cs`. Repositories are interfaces in Domain with an in-memory implementation in Api.
- Every new rule or endpoint needs tests. Use `FakeInvoiceRepository` in tests.
- Use clear names; no one-letter variables or `flag` parameters.
- End-to-end tests use page objects and role-based locators (`getByRole`, `getByLabel`), never CSS or XPath selectors.


## Approval rules (target behaviour for the role-limits feature)
 
Spec: `openspec/changes/add-role-approval-limits/`. Built in two increments, each its own commit.
 
1. A Manager may approve an invoice up to and including 50,000.
2. A Director may approve an invoice up to and including 500,000.
3. Above 500,000 two distinct approvers are needed. The first may be a Manager or a Director. The second must be a Director.
4. The same user never counts twice on one invoice. Compare usernames case-insensitively.
5. A denied attempt returns a clear reason and changes nothing on the invoice: `Status`, `DecidedBy`, `DecidedAt`, `DecisionNote` and the approvals list stay as they were.


Assumptions (confirm in the spec if they change):
 
- Role names are the strings `Manager` and `Director`. The legacy `Approver` role is treated as Manager. `Admin` has no approval authority. Any other role is denied.
- Limits apply to approving only. Reject is unchanged.
- A first approval above 500,000 keeps the status `PendingApproval`. Status becomes `Approved` only when the second approval lands. Do not add a new status value.
- A denied attempt is not recorded anywhere on the invoice.


Where the code goes:
 
- Limits and decision logic: `InvoiceApp.Domain` (`ApprovalPolicy`, `ApprovalService.Approve`). `Approve` receives the approver's username and role.
- The controller only passes `HttpContext.GetSession()` values to the service.
- Denials throw `ApprovalDeniedException` with the reason as its message. `ApiControllerBase.Handle` maps it to 403 with `{ "error": "<reason>" }`. The web UI already shows `error` in the Approvals tab.
- Existing errors stay as they are: 404 unknown invoice, 409 invoice not pending, 400 blank reject reason.


How a denial must behave:
 
- Validate everything first. Change the invoice only after the last check has passed. The repositories return live objects, so changing the invoice before a check can leak into storage even if the code then throws.
- Keep the check and the change together under one lock.


## Tests expected
 
- xUnit in `ApprovalServiceTests`, using `FakeInvoiceRepository`.
- One test per boundary: exactly 50,000, just above 50,000, exactly 500,000, just above 500,000.
- Separate tests for the two negative rules: same user twice, and a non-Director second approver. Test them separately so one cannot hide the other.
- Every denial test asserts the whole invoice is unchanged, not only the status.
- After a denied second approval, a valid second approval must still succeed.
- One Playwright test for a rejected over-limit approval through the UI, built with page objects.
- A test that cannot fail is not a test. Check by temporarily breaking the rule.


## Review expectations
 
- Explain a plan and the list of files before editing. Wait for approval.
- One increment per request. Never ask for the whole feature at once.
- No unrelated refactors. Do not touch `DiscountCalculator`. Do not implement UC-1 or UC-2.
- Do not add frameworks or packages without asking.
- Never say a test passed unless you ran it and saw the output.
- Do not invent files, methods or commands. Read the code first.


## Commands

- Run the app: `dotnet run --project backend/src/InvoiceApp.Api` then open http://localhost:5000
- Run the tests: `dotnet test backend/InvoiceApp.sln`
- Run end-to-end tests: `cd e2e && npm install && npx playwright install chromium && npm test`

## Demo accounts (not real credentials)

- `demo.approver` / `ChangeMe123!` (role Approver)
- `demo.admin` / `ChangeMe123!` (role Admin)

## Never

- Commit real credentials, tokens or customer data.
- Add business logic to `Program.cs` or controllers.
