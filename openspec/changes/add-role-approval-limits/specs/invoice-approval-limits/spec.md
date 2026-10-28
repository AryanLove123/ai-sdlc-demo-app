# Spec Delta

## Purpose

Defines who may approve an invoice and when: limits by role and amount, a two-approver rule above 500,000, and the guarantee that a denied attempt changes nothing.

## ADDED Requirements

### Requirement: Manager and Director limits are enforced by role and amount
The system SHALL allow a Manager to approve an invoice up to and including 50,000 and a Director up to and including 500,000. The legacy `Approver` role SHALL be treated as `Manager`. Any other role, including `Admin`, SHALL be denied. A denial reason SHALL name the role and, where relevant, its limit.

#### Scenario: Manager approves exactly 50,000
- **WHEN** a Manager approves a pending invoice of 50,000 that has no approvals
- **THEN** the invoice status becomes Approved
- **AND** DecidedBy is the Manager's username and DecidedAt is set
- **AND** one approval record exists with the Manager's username and role

#### Scenario: Manager is denied at 50,000.01
- **WHEN** a Manager approves a pending invoice of 50,000.01 that has no approvals
- **THEN** the approval is denied with a reason that names the Manager role and the 50,000 limit
- **AND** the invoice is unchanged

#### Scenario: Manager is denied at 50,001
- **WHEN** a Manager approves a pending invoice of 50,001 that has no approvals
- **THEN** the approval is denied with a reason that names the Manager role and the 50,000 limit
- **AND** the invoice is unchanged

#### Scenario: Director approves 50,001
- **WHEN** a Director approves a pending invoice of 50,001 that has no approvals
- **THEN** the invoice status becomes Approved with one approval record

#### Scenario: Director approves exactly 500,000
- **WHEN** a Director approves a pending invoice of 500,000 that has no approvals
- **THEN** the invoice status becomes Approved with one approval record

#### Scenario: Manager is denied at 500,000
- **WHEN** a Manager approves a pending invoice of 500,000 that has no approvals
- **THEN** the approval is denied with a reason that names the Manager role and the 50,000 limit
- **AND** the invoice is unchanged

#### Scenario: Legacy Approver is treated as Manager
- **WHEN** a user with the role Approver approves a pending invoice of 50,000
- **THEN** the invoice status becomes Approved
- **WHEN** the same role approves a pending invoice of 50,001
- **THEN** the approval is denied and that invoice is unchanged

#### Scenario: A role that may not approve is denied
- **WHEN** a user whose role is Admin, or any role other than Manager, Approver or Director, approves a pending invoice of any amount
- **THEN** the approval is denied with a reason stating that the role may not approve invoices
- **AND** the invoice is unchanged

### Requirement: Invoices above 500,000 need two distinct approvers
The system SHALL require two distinct approvers for an invoice above 500,000. The first approver SHALL be a Manager or a Director. The second approver SHALL be a Director. The invoice SHALL stay PendingApproval after the first approval and SHALL become Approved only when the second approval is recorded. Usernames SHALL be compared case-insensitively. When an attempt breaks both the same-user rule and the Director rule, the same-user reason SHALL be returned.

#### Scenario: First approval by a Director keeps the invoice pending
- **WHEN** Director A approves a pending invoice of 500,001 that has no approvals
- **THEN** the invoice status stays PendingApproval
- **AND** exactly one approval record exists for Director A
- **AND** DecidedBy and DecidedAt are not set

#### Scenario: First approval by a Manager keeps the invoice pending
- **WHEN** Manager A approves a pending invoice of 500,001 that has no approvals
- **THEN** the invoice status stays PendingApproval
- **AND** exactly one approval record exists for Manager A
- **AND** DecidedBy and DecidedAt are not set

#### Scenario: A different Director completes the approval
- **WHEN** Director B approves a pending invoice of 500,001 that already has one approval from Director A
- **THEN** the invoice status becomes Approved
- **AND** two approval records exist, for Director A and Director B
- **AND** DecidedBy is Director B and DecidedAt is set

#### Scenario: The same user cannot approve twice
- **WHEN** Director A approves a pending invoice of 500,001 that already has one approval from Director A
- **THEN** the approval is denied with a reason stating that the user has already approved this invoice
- **AND** the invoice is unchanged

#### Scenario: Usernames are compared case-insensitively
- **WHEN** a Director with the username director.a approves a pending invoice of 500,001 that already has one approval from Director.A
- **THEN** the approval is denied as a duplicate user
- **AND** the invoice is unchanged

#### Scenario: A second approver who is a Manager is denied
- **WHEN** Manager B, a different user from the first approver, approves a pending invoice of 500,001 that already has one approval from Director A
- **THEN** the approval is denied with a reason stating that the second approver must be a Director
- **AND** the invoice is unchanged

#### Scenario: The first approver, a Manager, tries again
- **WHEN** Manager A approves a pending invoice of 500,001 that already has one approval from Manager A
- **THEN** the approval is denied with the same-user reason, not the Director reason
- **AND** the invoice is unchanged

#### Scenario: A denied second approval leaves the invoice able to complete
- **WHEN** a denied second approval is followed by an approval from a different Director
- **THEN** the second attempt succeeds and the invoice becomes Approved with two approval records

### Requirement: A denied approval changes nothing and returns a clear reason
The system SHALL leave Status, DecidedBy, DecidedAt, DecisionNote and the approval history exactly as they were after any denied approval. The API SHALL answer a denial with HTTP 403 and a JSON body containing an `error` message, and the web UI SHALL show that message.

#### Scenario: Denial leaves every field untouched
- **WHEN** any approval is denied
- **THEN** Status, DecidedBy, DecidedAt, DecisionNote and the approval list equal their values before the attempt

#### Scenario: The API reports the reason
- **WHEN** a Manager calls the approve endpoint for a pending invoice of 75,000
- **THEN** the response is 403 with a JSON `error` message naming the Manager limit
- **AND** the invoice is still PendingApproval

### Requirement: Existing lifecycle behaviour is preserved
The system SHALL keep the existing behaviour for invoices that are not PendingApproval (409), unknown invoices (404), and rejection. The not-pending check SHALL run before the role and limit checks. Limits SHALL apply to approving only.

#### Scenario: A non-pending invoice keeps the 409 behaviour
- **WHEN** any user approves an invoice whose status is not PendingApproval
- **THEN** the existing InvalidOperationException (HTTP 409) is returned
- **AND** the invoice and its approval history are unchanged

#### Scenario: Reject is not limited by amount
- **WHEN** a Manager rejects a pending invoice of 75,000 with a reason
- **THEN** the invoice is Rejected exactly as it is today
