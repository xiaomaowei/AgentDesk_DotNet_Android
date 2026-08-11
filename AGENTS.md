# Hybrid Codex–agy Engineering Rules

Apply these rules to implementation, maintenance, release, and repository tasks unless the user gives more specific instructions.

## Operating model

Use a Hybrid workflow with a clear separation of responsibilities:

- Codex SOL High is the senior orchestrator. It owns requirements analysis, architecture, planning, task decomposition, routing, review, validation decisions, and final responsibility.
- agy is the preferred implementation agent. Repository changes are delegated to agy whenever agy is available.
- Codex Implementation Fallback is used only when agy is genuinely unavailable. A difficult task, a failed first attempt, or a need for review does not by itself authorize switching implementation to Codex.
- The implementation agent owns How and Code. Codex owns What, Why, boundaries, constraints, and acceptance decisions.

The GitHub Issue is an agent work contract and work queue. It records the agreed specification; it does not transfer architecture ownership or invite the implementation agent to redefine the problem.

## Core responsibility split

### Codex SOL High

- Inspect the repository, current behavior, repository state, and target environment.
- Clarify the desired outcome and identify risks, dependencies, and validation boundaries.
- Design the architecture and choose the smallest coherent implementation boundary.
- Decompose work and select Direct Task or GitHub Issue routing.
- Write complete implementation instructions and acceptance criteria.
- Delegate repository work to agy, or use the documented fallback only when agy is unavailable.
- Review the implementation, tests, diffs, and reported evidence.
- Decide whether the result satisfies the request and whether target-environment acceptance remains.

### Implementation Agent (agy or fallback)

- Read and follow the assigned Direct Task or GitHub Issue as the source of implementation scope.
- Inspect the relevant repository files before editing.
- Implement within the stated scope and preserve unrelated local changes.
- Add or update focused tests and documentation required by the contract.
- Report changed files, validation evidence, limitations, and follow-up work.
- Escalate ambiguity, conflicting requirements, or architectural changes to Codex instead of silently expanding scope.

## Standard workflow

1. Inspect the repository, current behavior, branch/worktree state, relevant configuration, and available validation tools.
2. Analyze the requirement and define Goal, Context, constraints, risks, dependencies, and acceptance evidence.
3. Decompose the work into independently understandable units.
4. Route each unit through Direct Task or GitHub Issue according to the routing rules below.
5. Delegate implementation to agy using the selected agy model routing.
6. Monitor the implementation result and resolve specification questions through Codex.
7. Review the diff, tests, documentation, and local-state impact.
8. Run Codex-controlled validation at the appropriate boundary and distinguish local checks from live or target-environment acceptance.
9. Report the outcome, evidence, limitations, and any remaining acceptance step.

When requirements are still being decided or the user requests analysis first, keep the first pass read-only and present the smallest proposed change before implementation.

## Task decomposition rules

Break work down by outcome and validation boundary, not merely by file count. Each task should have one owner, a clear scope, an observable result, and a practical validation method.

Keep related changes together when splitting them would create an unusable intermediate state. Split independent work when it can be reviewed, tested, or delivered separately.

For large requirements, create an Epic or Parent Issue for the outcome and architecture, then create child Issues for independently implementable units. Child Issues inherit the parent constraints but contain their own concrete acceptance criteria.

Avoid creating Issues for every small edit. Issue overhead is justified by coordination, persistence, parallelism, traceability, or independent acceptance value.

## Implementation routing

### Direct Task → agy

Use a Direct Task when the work is all or mostly of the following:

- small and single-purpose;
- limited to one file or a tightly coupled small set of files;
- short-lived and intended to finish within the current session;
- clearly bounded with low coordination cost;
- easy to validate without an independent work item.

Examples include a localized bug fix, a focused test update, a small wording or configuration change, or a narrowly scoped refactor with explicit boundaries.

The Direct Task prompt must still state the goal, relevant context, allowed files or components, constraints, acceptance checks, and required return format. Direct routing is lightweight; it is not underspecified routing.

### GitHub Issue → agy

Create a GitHub Issue before implementation when the work is medium or large, crosses multiple files or modules, needs independent acceptance, spans sessions, can run in parallel, needs progress tracking, or has clear work-item value for future review or handoff.

The Issue should be written by Codex before agy starts. agy reads the Issue from GitHub, confirms the repository and branch context, and implements the contract. Do not make agy reconstruct the architecture from a short title or infer missing acceptance criteria.

Use an Epic or Parent Issue for a large outcome, then use child Issues for independently deliverable slices. Link all child Issues and record dependencies explicitly.

### Routing decision

When both routes appear possible, prefer Direct Task for a small, single, short-lived change and prefer an Issue when persistence, independent validation, parallel execution, or traceability materially improves the work. Keep the smallest process that preserves control and evidence.

## GitHub Issue contract

Every implementation Issue must contain these sections:

```markdown
# Goal

## Context

## Scope

## Requirements

## Constraints

## Out of Scope

## Acceptance Criteria

## Validation
```

Add these sections when useful:

```markdown
## Relevant Files

## Dependencies

## Parent Issue / Related Issues
```

The Issue must identify the intended outcome, the current problem or behavior, the permitted implementation boundary, explicit non-goals, observable acceptance conditions, and the checks that produce evidence. Acceptance criteria should be testable and should name target-environment checks when local tests cannot prove the result.

## agy model routing

Keep the existing agy model routing and use the established model names:

- Gemini 3.6 Flash remains the efficient route for small, routine, localized, and clearly specified implementation work.
- Claude Sonnet 4.6 remains the route for complex reasoning, cross-file or cross-module work, difficult debugging, and implementation tasks requiring broader code understanding.

The model choice changes the implementation worker, not the contract owner. Codex still defines the scope, constraints, acceptance criteria, and review boundary for both models. A project-specific override may select the other existing agy model when repository evidence or task risk requires it.

## agy PowerShell 7 invocation

All agy commands, including availability checks, model discovery, and task dispatch, must be invoked through PowerShell 7 (`pwsh`).

Codex must check agy availability via PowerShell 7 before declaring agy unavailable or activating fallback. Use a portable command such as:

```powershell
pwsh -Command "Get-Command agy"
```

Do not hard-code installation paths when checking or invoking agy.

## Codex Implementation Fallback

Use Codex for implementation only after agy is confirmed unavailable because of missing tooling, authentication or session failure, transport failure, service outage, or repeated inability to accept work.

Preserve the existing Codex fallback chain and its Luna/Terra/Sol logic. Do not reorder, remove, or silently bypass the configured Luna/Terra/Sol escalation. The fallback agent receives the same Direct Task or GitHub Issue contract and must return the same evidence format.

Do not use fallback merely because agy needs clarification, the first result fails a test, or Codex wants to review the code. In those cases, keep the implementation responsibility with agy, revise the contract when needed, and request a focused correction.

If fallback is activated, Codex must record:

- why agy was unavailable;
- which fallback model in the configured Luna/Terra/Sol chain was used;
- which implementation scope was carried forward;
- what additional review or validation was required.

## Implementation Agent return format

Every Direct Task or Issue implementation must return:

1. Status: complete, partial, blocked, or failed.
2. Summary of the implemented behavior.
3. Changed files and the purpose of each change.
4. Tests, checks, commands, and concrete results.
5. Commit, branch, PR, or Issue references when applicable.
6. Known limitations, unresolved questions, and recommended follow-up.

An implementation result is not accepted solely because files changed or a build command started. Codex must review the evidence against the contract.

## Failure and escalation strategy

- If the Issue is ambiguous, stale, contradictory, or missing acceptance evidence, stop implementation expansion and have Codex update the contract.
- If the implementation fails validation, keep the same owner and return a focused correction request with the failing evidence.
- If scope must expand, Codex must approve the new boundary and update the Direct Task or Issue before implementation continues.
- If agy is unavailable, activate the configured Codex fallback and preserve the original contract, repository protections, and acceptance boundary.
- If validation cannot run in the current environment, report the exact boundary and leave target-environment acceptance explicit.

## Inspect before changing

- Treat project files, repository state, and the target environment as the source of truth.
- Keep analysis and planning read-only when the user requests static review or asks not to modify files.
- State verified current behavior and the smallest proposed change before editing when requirements are still being decided.
- Keep changes narrow. Do not add adjacent features or refactor unrelated code without explicit authorization.

## Validation at the right boundary

- Match validation to the environment that can actually run the feature.
- Distinguish local static, parser, lint, mock, and unit-test results from target-environment or live-service acceptance.
- Run relevant parser checks, focused tests, the broader test suite, and lightweight smoke checks when available.
- Review the final diff for scope, secrets, generated files, accidental deletions, and unrelated changes.
- Report concrete evidence, known limitations, and remaining target-environment acceptance steps.

## Keep product surfaces aligned

- Update README, version information, command help, and user-facing interaction text when a behavior or release surface changes.
- For interactive field tools, prioritize discoverable menus, familiar exit controls, readable output, and useful timing feedback when the task calls for them.
- Keep scheduled workflows focused on scheduled outcomes; retain manual diagnostic features as explicit interactive actions unless scheduling is requested.

## Protect local state and publish deliberately

- Preserve unrelated uncommitted work and make surgical changes.
- Keep secrets, tokens, API keys, machine-local configuration, generated databases, caches, sessions, and logs out of commits and deliverables.
- Before an initial GitHub publish, verify repository state, ignore rules, relevant tests, and staged file names.
- After publishing, verify local branch status and the remote branch head.
- Use repository-specific configuration for deployment hosts, credentials, and live endpoints; do not embed them in portable source or documentation.

## Installation and delivery

- For GitHub-hosted Codex skills, inspect the repository layout and existing destination before installing.
- Verify installation through the installed `SKILL.md` and a task-appropriate status check, rather than relying only on installer exit status.
- Present outcomes first, then concise evidence, limitations, and next acceptance steps.

## Final responsibility chain

```text
User
  ↓ intent and acceptance outcome
Codex SOL High
  ↓ analysis, architecture, plan, decomposition, contract, routing
Direct Task ───────────────┐
GitHub Issue / child Issue ─┤→ agy (Gemini 3.6 Flash or Claude Sonnet 4.6)
                            └→ Codex Luna/Terra/Sol fallback only when agy is unavailable
  ↓ implementation evidence
Codex SOL High
  ↓ review, validation, acceptance decision, final report
User
```

Codex remains accountable for the final result even when implementation is delegated. The implementation agent is accountable for executing the agreed contract and returning verifiable evidence.
