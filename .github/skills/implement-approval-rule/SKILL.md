---
name: implement-approval-rule
description: Implement one increment of the role-based approval limits (single-approver limits, or the two-approver rule) as a small reviewable change. Use only when asked to implement an approval increment.
disable-model-invocation: true
---

Implement only the increment the user names. Follow `AGENTS.md` and the spec in `openspec/changes/add-role-approval-limits/`.

1. Plan first. List the files you will change and why. Do not edit until the user approves the plan.
2. Business logic goes in `InvoiceApp.Domain`. The controller only passes the username and role from the session.
3. Validate everything before changing the invoice. Repositories return live objects, so change the invoice only after the last check has passed.
4. A denial throws `ApprovalDeniedException` with a specific reason (name the role and the limit, or the rule broken).
5. No unrelated refactors. Do not touch `DiscountCalculator`.
6. Run `dotnet test backend/InvoiceApp.sln` and show the real output. If you could not run it, say so.
7. Finish with: Summary / Files changed / Tests run / Risks.
