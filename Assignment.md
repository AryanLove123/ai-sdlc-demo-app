# AI in SDLC: Role-based approval limits

**Author:** Lovekesh Kumar Arya

**Repo (my fork):** https://github.com/AryanLove123/ai-sdlc-demo-app.git

**Pull request:** https://github.com/AryanLove123/ai-sdlc-demo-app/pull/1

**Demo video link** https://nagarro-my.sharepoint.com/:v:/p/lovekesh_arya/IQAHqCnDhfyrToz5Ff4P3DthAbASWISN56rXSug0ZE7whTg?nav=eyJyZWZlcnJhbEluZm8iOnsicmVmZXJyYWxBcHAiOiJPbmVEcml2ZUZvckJ1c2luZXNzIiwicmVmZXJyYWxBcHBQbGF0Zm9ybSI6IldlYiIsInJlZmVycmFsTW9kZSI6InZpZXciLCJyZWZlcnJhbFZpZXciOiJNeUZpbGVzTGlua0NvcHkifX0&e=2a9lhZ


## 1. Summary

I extended the invoice-approval demo app (.NET, in-memory repository, small static web UI) with role-based approval limits:

| Rule | Behaviour |
| --- | --- |
| Manager | Can approve an invoice up to and including 50,000 |
| Director | Can approve an invoice up to and including 500,000 |
| Above 500,000 | Needs two distinct approvers. The first may be a Manager or a Director. The second must be a Director. |
| Same user | One user never counts twice on the same invoice (usernames compared trimmed and case-insensitively) |
| Denied attempt | Returns a clear reason (HTTP 403 with the reason) and changes nothing on the invoice |

The work was built with GitHub Copilot (Free plan) in VS Code, spec-first with OpenSpec, in two increments.

## 2. Design decisions and assumptions

The brief left some things open, so I settled them in the spec before writing code:

- **Roles are exactly `Manager` and `Director`, matched exactly.** Role validation is case-sensitive and restricted strictly to Manager or Director. The legacy Approver role is no longer supported, and Admin does not have approval rights. Any variations in capitalization or unauthorized roles will result in a denial.
- **First approval above 500,000** may come from a Manager or a Director, because the brief only restricts the second approver. The Manager limit does not apply to that first approval.
- **Status stays `PendingApproval` after the first approval above 500,000.** It becomes `Approved` only when the second approval lands. I did not add a new status value.
- **Check order:** find the invoice, check it is pending, check the approver name is not blank, check the role, then the amount rules. If a second approval breaks both the same-user rule and the Director rule, the same-user message wins.
- **A denied attempt changes nothing.** All checks run before any field is touched, because the repository returns live object references, so a change made before a failed check would leak into storage. Denials are not recorded on the invoice.
- **Limits apply to approving only.** Reject is unchanged.
- **Denials use a new `ApprovalDeniedException`**, mapped to HTTP 403 `{ "error": "<reason>" }` by `ApiControllerBase`. Existing errors (404 unknown invoice, 409 not pending, 400 blank reason) are unchanged. The web UI already shows `error` in the Approvals tab.
- **One static lock** covers the whole of `Approve`, from finding the invoice to saving it, so two concurrent requests cannot both pass the "pending" and "same user" checks.

## 3. Spec tool: OpenSpec

I chose OpenSpec because it creates Copilot prompt files and skills directly (`openspec init --tools github-copilot`, then `/opsx-propose`). The change is in `openspec/changes/add-role-approval-limits/` (proposal, design, tasks and `specs/invoice-approval-limits/spec.md`), plus `openspec/config.yaml` with project rules.

Copilot's first draft needed corrections before I accepted it:
- several scenarios were missing preconditions (for example, a second approval needs a first approval already seeded)
- the behaviour of an invoice above 500,000 during the first increment was not specified
- the Approver role alias, which I later removed after re-reading the brief

`openspec validate --strict` passes on the corrected files.


## 4. Agents, skills, hooks and AGENTS.md

- **`AGENTS.md`** now documents the approval rules, the assumptions, where code belongs, how a denial must behave, the tests expected, review expectations, the Stryker command and the demo accounts. 

   Mutation testing was performed using **Stryker.NET (v4.13.0)** targeting `ApprovalService.cs`.
   
   *Stryker Mutation Testing Results (`ApprovalService.cs`, v4.13.0):*

   - **Baseline Score (Before implementation):** **60.00%** (20 mutants tested | 10 killed, 8 survived, 2 timeout; saved at `docs/mutation/baseline.txt`).
   - **Final Score (After implementation & test hardening):** **100.00%** (57 mutants tested | 57 killed, 0 survived, 0 timeout; saved at `docs/mutation/super-final.txt`).
- **Agents** (`.github/agents/`): `orchestrator` (delegates only), `explorer`, `implementer`, `test-author`, `reviewer`.
- **Skills** (`.github/skills/`): `repo-explorer`, `implement-approval-rule`, `test-reviewer`, `pr-reviewer`.
- **Hook** (`.github/hooks/ai-log.json` and `log-event.mjs`): every event is written as one JSON line with the same six keys (`ts, session_id, event, agent_type, tool_name, summary`) to `docs/ai-log/ai-interactions.jsonl`, with secrets redacted. Known limit: `Stop` events do not include the assistant's reply, so the log shows that a turn ended and what was asked, not what the AI answered.

## 5. How the work was done

I worked in small steps and never asked for the whole feature at once: plan first, then one increment per prompt, and I read every diff before keeping it.

| Commit | What and why |
| --- | --- |
| chore(ai) | Added Copilot skills, specialized agents, and the automated hook-based interaction logger to track AI usage. |
| docs(spec) | Initialized OpenSpec files (proposal, scenarios, and task breakdowns) and updated the spec to explicitly retire the legacy Approver role alias. |
| feat(approval-limits) | Increment 1: Implemented core single-approver rules (Manager up to 50,000, Director up to 500,000), throwing proper 403 rejections on over-limit attempts while blocking amounts above 500,000 temporarily. |
| feat(approvals) | Increment 2: Added the two-approver workflow for invoices over 500,000, enforcing a second distinct Director approver and thread-safe static locking.|
| test(approvals) (or mutation run) | Wrote rigorous test suites verifying immutability on denial, correct error messages, non-pending invoice protection, and completed Stryker mutation test analysis.|
| test(e2e) | Implemented and executed the Playwright end-to-end test verifying that over-limit approval requests are correctly blocked and surfaced through the browser UI. |


## 6. Tests

40 unit tests in `ApprovalServiceTests` (xUnit, `FakeInvoiceRepository`), 1 Playwright test.

| Rule | Tests (examples) |
| --- | --- |
| Manager exactly 50,000 | `Approve_ManagerAt50000_IsApproved` |
| Just above the Manager limit | `Approve_ManagerAt50000Point01_IsDenied`, `Approve_ManagerAt50001_IsDenied` |
| Director above 50,000 | `Approve_DirectorAt50001_IsApproved` |
| Director exactly 500,000 | `Approve_DirectorAt500000_IsApproved` |
| Manager at 500,000 | `Approve_ManagerAt500000_IsDenied` |
| First approval above 500,000 | `Approve_DirectorFirstApprovalAbove500000_KeepsInvoicePending`, `Approve_ManagerFirstApprovalAbove500000_KeepsInvoicePending` |
| Valid two-approver combinations | `Approve_ManagerThenDifferentDirectorAbove500000_CompletesApproval`, `Approve_DirectorThenDifferentDirectorAbove500000_CompletesApproval` |
| Negative rule 1: same user twice | `Approve_SameDirectorSecondApprovalAbove500000_IsDenied`|
| Negative rule 2: second approver not a Director | `Approve_DifferentUserManagerSecondApprovalAbove500000_IsDenied` |
| Both broken at once | `Approve_SameManagerSecondApprovalAbove500000_UsesSameUserMessage` |
| Valid second approval after a denial | two `..._IsFollowedByValidDirectorApproval` tests |
| Finished invoice | `Approve_ThirdApprovalAfterApprovalIsAlreadyComplete_ThrowsAndLeavesInvoiceUnchanged` |
| Roles | Admin, `Approver`, lowercase `manager`, unknown role, Admin on a large invoice |
| Other | blank approver, already-approved invoice, Reject is not limited by amount |
| Saves | six tests check `Update` is called once per save and not on a denial |

Every denial test asserts that the whole invoice is unchanged (`Status`, `DecidedBy`, `DecidedAt`, `DecisionNote` and the approval list), not only the status.

**Playwright** (`e2e/tests/over-limit-approval.spec.ts`, page objects in `e2e/pages`, role and label locators only): a Manager creates and submits a 75,000 invoice, tries to approve it, sees "A Manager may approve invoices up to 50,000.", changed the first sign-in to `demo.director`. The approval then succeeded.

## 7. What the AI got wrong, and how I caught it

I did not trust any summary Copilot wrote. Things I found by reading diffs and running the tests:

- Its first production draft matched roles case-insensitively (a `manager` role would have been approved), duplicated checks, formatted amounts with the current culture, and kept `Approver` in the controller attribute after the plan said it was retired. I discarded it.
- Its second draft ignored my message wording and claimed fixes it had not made. I wrote the exact code and applied it by hand.
- Its first test draft deleted existing tests, wrote Increment 2 tests with message texts it had invented, and used an "unchanged" helper that did not compare the whole invoice. I restored the original file and made small, reviewed edits in batches.
- The orchestrator agent looped for minutes on a large prompt without changing any file. Splitting the work into small steps with the implementer and test-author worked.

## 8. AI interaction log

`docs/ai-log/ai-interactions.jsonl`, written by the hook, with one uniform schema for every event.