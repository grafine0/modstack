---
trigger: always_on
---

# Agent Session Journal and GitHub Backup

## Purpose

Maintain a persistent summary of important agent work for this project.

The summary must be stored in the Git repository so future agent sessions can recover important context.

## Journal Location

Use:

`.agents/sessions/`

Create the directory if it does not exist.

Use one Markdown file per significant development session:

`.agents/sessions/YYYY-MM-DD.md`

If multiple significant sessions occur on the same day:

`.agents/sessions/YYYY-MM-DD-01.md`

## When to Update the Journal

Update the journal when:

* A significant feature is implemented.
* A significant bug is investigated or fixed.
* An architectural decision is made.
* A significant refactoring is completed.
* Dependencies or build configuration are changed.
* An implementation plan is approved.
* A significant task is completed.
* Important technical discoveries are made.
* A substantial development session is ending.

Do not create entries for trivial questions or minor changes.

## Journal Content

Summarize the important information from the agent session.

Include:

### Objective

What the user wanted to accomplish.

### Investigation

Important findings from inspecting the project.

### Decisions

Important technical or architectural decisions and the reasoning behind them.

### Changes

Important files and components modified.

### Problems and Solutions

Important errors, bugs, and how they were resolved.

### Testing

Build, test, lint, and verification results.

### Remaining Work

Incomplete work or known issues.

### Next Steps

What should be done next.

### Important Context

Information that a future agent needs to continue the project correctly.

Do NOT copy the complete conversation.

Create a useful technical summary instead.

## Sensitive Information

NEVER save:

* Passwords
* API keys
* Access tokens
* Private keys
* Secrets
* `.env` contents
* Authentication credentials
* Sensitive personal information

## Git Workflow

Do not commit or push after every simple change.

- Group minor, related changes into meaningful commits.
- Commit and push after completing and verifying an important feature, bug fix, refactoring, or other significant task.
- If only minor changes have accumulated, commit and push them at the end of a substantial work session when at least 24 hours have passed since the last push.
- Before committing, run `git status` and review the changes with `git diff`.
- Include relevant journal updates in the same commit.
- Never create empty commits or push when there are no new commits.

Check the elapsed time during active work; this rule does not require background monitoring.

## Workflow

Read Previous Journal → Investigate → Plan → Implement → Test → Update Journal when significant → Commit and Push when the conditions above are met.

Use `.agents/sessions/` consistently when reading and writing journal entries.

## Recovering Previous Context

Before starting a significant task:

1. Check whether `.agent/sessions/` exists.
2. Read the most recent relevant journal entries.
3. Use them to recover previous decisions, completed work, known problems, and pending tasks.
4. Do not repeat work that has already been completed.

## Important Rule

The journal is a persistent technical memory for the project.

Keep it concise, accurate, and useful to another developer or AI agent continuing the project later.

Do not modify application code solely to support this journal.

## Workflow

For significant development work:

**Read Previous Journal → Investigate → Plan → Get Approval → Implement → Test → Update Journal → Git Commit → Git Push**