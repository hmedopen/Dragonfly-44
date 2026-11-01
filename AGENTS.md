# Dragonfly 44 - Mandatory Development Documentation & Activity Logs

This rule applies to all coding agents, subagents, and sessions operating in this repository.

## Mandatory Development Documentation & Activity Logs

Every plan, implementation task, modification, and completed task must be documented.

### Storage Location

Save all records in:

`C:\Users\HMED OPEN\Documents\Dragonfly 44\logs 44`

Create the directory automatically if it does not exist.

### Required Records

1. **Plans**
   Whenever you create or revise an implementation plan, save it as a Markdown (`.md`) file before or during implementation.
   Include:
   - Plan title and date.
   - Objective.
   - Proposed implementation steps.
   - Technical decisions and reasoning.
   - Dependencies and potential risks.
   - Expected completion criteria.

2. **Task Completion Reports**
   Whenever you finish a task, create a Markdown (`.md`) completion report.
   Include:
   - Task description.
   - Files created, modified, or deleted.
   - Implementation details.
   - Technical decisions.
   - Tests performed and their actual results.
   - Known issues or unfinished work.
   - Any deviations from the original plan.

3. **Change Records**
   Whenever you modify existing functionality, architecture, schemas, or project configuration, document the changes.
   Include:
   - What changed.
   - Why it changed.
   - Previous behavior versus new behavior.
   - Potential impact on other components.

4. **Technical Walkthroughs**
   For significant implementations, document how the implementation works, including important classes, services, dependencies, data flow, and relevant architectural decisions.

### Documentation Rules

- Use descriptive filenames with dates and task identifiers (e.g. `YYYY-MM-DD_type_identifier.md`).
- Use Markdown for readability.
- Never overwrite previous records. Create new records or explicitly version existing plans.
- Be accurate. Never claim tests passed unless they were actually executed.
- If a task is incomplete, document its current state and remaining work.
- Keep records chronological and sufficiently detailed for another developer or AI agent to understand the project's history.
- Documentation must reflect the actual implementation, not merely the intended implementation.

### Mandatory Final Step

**Before considering any task complete, save its required documentation records to the specified directory.**

At the end of every task, explicitly report:
1. Which documentation files were created.
2. Their full paths.
3. Whether all required records were successfully saved.

If the directory cannot be accessed, report the failure clearly. Never silently skip documentation.
