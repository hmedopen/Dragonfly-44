---
name: development-documentation
description: >-
  Mandatory development documentation and activity logging for every single task in Dragonfly 44.
  Use to create and maintain plans, task completion reports, change records, and technical walkthroughs
  saved in C:\Users\HMED OPEN\Documents\Dragonfly 44\logs 44.
---

# Mandatory Development Documentation & Activity Logs

Every plan, implementation task, modification, and completed task in this project must be documented.

## Storage Location

Save all records in:

`C:\Users\HMED OPEN\Documents\Dragonfly 44\logs 44`

Create the directory automatically if it does not exist.

## Required Records

### 1. Plans
Whenever you create or revise an implementation plan, save it as a Markdown (`.md`) file before or during implementation.

**Must include:**
- Plan title and date.
- Objective.
- Proposed implementation steps.
- Technical decisions and reasoning.
- Dependencies and potential risks.
- Expected completion criteria.

### 2. Task Completion Reports
Whenever you finish a task, create a Markdown (`.md`) completion report.

**Must include:**
- Task description.
- Files created, modified, or deleted.
- Implementation details.
- Technical decisions.
- Tests performed and their actual results.
- Known issues or unfinished work.
- Any deviations from the original plan.

### 3. Change Records
Whenever you modify existing functionality, architecture, schemas, or project configuration, document the changes.

**Must include:**
- What changed.
- Why it changed.
- Previous behavior versus new behavior.
- Potential impact on other components.

### 4. Technical Walkthroughs
For significant implementations, document how the implementation works, including important classes, services, dependencies, data flow, and relevant architectural decisions.

## Documentation Rules

- **Filenames**: Use descriptive filenames with ISO dates (YYYY-MM-DD) and task identifiers (e.g., `2026-11-01_plan_feature-name.md`, `2026-11-01_task-completion_feature-name.md`).
- **Format**: Use clean Markdown formatting for readability.
- **No Overwriting**: Never overwrite previous records. Create new records or explicitly version existing plans (e.g., `_v2.md`).
- **Accuracy**: Be accurate. Never claim tests passed unless they were actually executed and verified.
- **Incomplete Work**: If a task is incomplete, document its current state, blocking issues, and remaining work.
- **Chronological & Detailed**: Keep records chronological and sufficiently detailed for another developer or AI agent to understand the project's history without prior context.
- **Actual vs Intended**: Documentation must reflect the actual implementation, not merely the intended implementation.

## Mandatory Final Step

**Before considering any task complete, save its required documentation records to the specified directory.**

At the end of every task, explicitly report to the user:
1. Which documentation files were created.
2. Their full paths.
3. Whether all required records were successfully saved.

If the directory cannot be accessed, report the failure clearly. Never silently skip documentation.
