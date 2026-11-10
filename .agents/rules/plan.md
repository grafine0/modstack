---
trigger: always_on
---

# Implementation Planning Rule

## Significant Changes

Before implementing any significant change, you MUST:

1. Inspect the existing code and architecture relevant to the request.
2. Determine the files, components, dependencies, and systems affected.
3. Create a clear implementation plan before modifying any files.
4. Always respond in English, even when the user writes in Arabic or another language.
5. Present the plan in english to the user.

### Significant changes include:
- New features
- Multi-file changes
- Architecture changes
- Refactoring
- Database or data-model changes
- API changes
- Authentication/authorization changes
- Navigation changes
- State-management changes
- Theme/design-system changes
- Localization architecture changes
- Dependency changes
- Gradle/build configuration changes
- Firebase/backend changes
- Security changes
- Performance-related architectural changes
- Any change that can affect multiple parts of the application

## Approval Required

After presenting the implementation plan:

**STOP and WAIT for explicit user approval.**

Do NOT:
- Modify files
- Implement code
- Apply patches
- Run commands that modify the project
- Continue automatically

Wait for explicit approval such as:

- "Approved"
- "Proceed"
- "Implement it"
- "Go ahead"

If the user requests changes to the plan, update the plan and wait for approval again.

## After Approval

Once the user explicitly approves:

1. Implement only the approved plan.
2. Do not expand the scope without approval.
3. If a significant new requirement or architectural change is discovered, STOP and ask for approval again.
4. Test the implementation.
5. Report the files changed, tests performed, build/lint results, and any remaining issues.

## Small Changes

Do not require a formal plan for trivial changes such as:
- Typo fixes
- Formatting
- Simple one-line fixes
- Obvious localized bug fixes
- Minor documentation changes

When uncertain whether a change is significant, treat it as significant and request approval before implementation.

## Core Workflow

**Inspect → Plan → STOP → User Approval → Implement → Test → Report**