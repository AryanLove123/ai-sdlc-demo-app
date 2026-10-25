---
name: repo-explorer
description: Map the files, classes and tests relevant to a request in this invoice app before any change is made. Read-only. Use before planning any change to approvals.
---

Explore the repository for the user's request. Do not edit anything. Report:
1. Files and classes involved, with paths (for example `backend/src/InvoiceApp.Domain/ApprovalService.cs`).
2. Existing behaviour that must be preserved, and the tests that protect it.
3. Where each new rule belongs. Business rules go in `InvoiceApp.Domain`, never in controllers or `Program.cs`.
4. Anything you could not find or are unsure about. Do not guess or invent files, methods or commands.

Keep the report under 40 lines.
