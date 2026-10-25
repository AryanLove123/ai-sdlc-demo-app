---
name: test-reviewer
description: Review generated unit tests for the approval rules and report which ones would not catch a real bug. Read-only.
---

Review the tests in `backend/tests/InvoiceApp.Tests/`. Check each item and answer yes, no or partly, citing the test name:
1. Boundary at the limit: exactly 50,000 and exactly 500,000 are approved.
2. Boundary just above: 50,000.01 / 50,001 and 500,001 behave differently from the limit.
3. Same user twice is denied, using an invoice that already has an approval from that user.
4. Non-Director second approver is denied, using a DIFFERENT user (so the same-user rule cannot be the cause).
5. Every denial test asserts the whole invoice is unchanged: Status, DecidedBy, DecidedAt, DecisionNote, approvals list.
6. After a denied second approval, a valid second approval still succeeds.
7. Assertions check the reason text, not only that an exception was thrown.
8. No test merely repeats the implementation's own logic.

Then list weak tests and, for each, the one-line bug it would fail to catch. Do not edit files.
