# Security Reviews

Threat model snapshots, penetration-test reports, audit findings. The goal is traceable,
dated evidence — not generic security advice.

## File naming

`YYYY-MM-DD-<scope-slug>.md` — e.g. `2026-05-10-contacts-gdpr-erasure.md`.

## Minimum sections

- **Context** — scope, threat model in play (STRIDE or similar).
- **Findings** — severity (Critical / High / Medium / Low / Info), affected asset,
  CWE/CVE where applicable.
- **Mitigations** — landed fixes, compensating controls, accepted risk.
- **Follow-up** — tasks opened, ADRs proposed.

## Related

- [Reviews root](../README.md)
- `docs/standards/` — will include `security-review.md` after Agent B's standards split
- Skill: `.claude/skills/perform-security-review/`
