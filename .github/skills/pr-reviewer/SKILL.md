---
name: pr-reviewer
description: Run the workshop-provided pull request review prompt against the current branch diff and list every finding. Use when asked to review the pull request.
---

PLACEHOLDER: the workshop review prompt is not in this repository.
Paste it into `docs/review/workshop-review-prompt.md` exactly as given. Do not write a substitute.

When that file exists:
1. Read `docs/review/workshop-review-prompt.md`.
2. Get the diff with `git diff main...HEAD`.
3. Apply the prompt to that diff. Number every finding.
4. Do not fix anything and do not assign verdicts. The author decides Fixed, Dismissed or Deferred.
5. If the file is missing, stop and say so.
