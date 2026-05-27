# Multi-Tenancy Migrations

**Spec ID:** 0029
**Created:** 2026-05-27
**Branch:** `multi-tenancy`
**Linked Issue:** [#4144](https://github.com/BrighterCommand/Brighter/issues/4144) — Box provisioning: support per-tenant isolation of `__BrighterMigrationHistory`

## Summary

Let operators isolate the box-provisioning migration-history table (`__BrighterMigrationHistory`) to a tenant's configured `SchemaName` instead of the backend default schema (`dbo` / `public` / connection `DATABASE()`), for per-schema-per-tenant deployments. Default behaviour stays backward compatible; SQLite and Spanner are out of scope (no schema concept). Builds on the F1a/F1b/F1c fresh-install schema-routing fixes from PR #4039 (reviewer item F2-5).

## Status

- [x] Requirements (`requirements.md`) — approved 2026-05-27 (PASS after 4 adversarial review rounds)
- [x] Design (ADR 0060) — Accepted & approved 2026-05-27 (PASS after 2 adversarial review rounds; `docs/adr/0060-multi-tenancy-migration-history-scope.md`)
- [ ] Tasks (`tasks.md`) — to be drafted post-ADR-approval
- [ ] Implementation — TDD per task; STOP for approval after each `/test-first` test before the GREEN
- [ ] Review
