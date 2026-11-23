# CLAUDE.md

## Decision records

Architecture decision records live under `docs/adr/`, one file per decision, named `NNNN-kebab-slug.md`.
`scripts/check-adr.sh` enforces the file names, unique prefixes and the `reaches:` front matter in the `build` job.

When implementing an issue, add a file under `docs/adr/` only when the issue carries a `## Decision record` section; commit it with the code, changing it only where the implementation forced a change, and say so in the PR description. Put every other decision's reasoning in a code comment, the PR, or the commit message.

### The ADR gate

An ADR qualifies only when **both** hold:

1. **The decision reaches beyond one class or feature slice.** It shapes one of:
   - the system's structure
   - a contract between modules
   - a contract with an external system
   - a non-functional property (security, performance, availability, data retention)
   - a dependency choice
   - an invariant that a change elsewhere could break without knowing why
2. **Reversing it needs meaningful rework.**

Front matter records which one it reaches, and holds only this key:

```yaml
---
reaches: structure | module-contract | external-contract | non-functional | dependency | hidden-invariant
---
```

Exactly one value. `status:`, `supersedes:` and `superseded-by:` are no longer used.
The allowed values are also the `ALLOWED_REACHES` constant in `scripts/check-adr.sh`; change both together.

These never get an ADR. Each one's reasoning goes elsewhere instead:

| Decision | Where its reasoning goes |
|---|---|
| A choice inside one class, endpoint, DTO or UI widget | A short why-comment at that code, only when the why is not obvious from the code |
| A migration step, or "X stays until Y" | The issue or PR |
| Applying an existing ADR or convention | A citation of that ADR or convention |
| The fix itself | The commit message. A lasting rule a fix establishes is judged by the gate. |
| Design not built yet | An issue |

One ADR records one decision.

**Amend in place.** A changed decision rewrites Context or Decision to state current truth and adds one line to a trailing `## Revisions` list:

```markdown
## Revisions
- 2026-11-20 — <what changed> (<ADR NNNN or #issue>)
```

A wholly replaced ADR is deleted, and every citation of it is re-pointed to the successor. ADR numbers are never reused or renumbered, and gaps stay.
