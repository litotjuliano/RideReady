# SDD ledger — plan: docs/superpowers/plans/2026-09-01-ride-booking-phase1-implementation.md
Task 1: complete (commits 2fdb767..2fdb767, review clean)
Task 2: complete (commits c07c790..c07c790, review clean)
Task 3: fix round 1/5 (2 addressed, 0 open; commits f0a453c..3205a4f)
Task 3: complete (commits f0a453c..3205a4f, 1 fix round)
Task 3: fix round 2/5 (10 addressed, 0 open; commits 3205a4f..f3708bb)
Task 3: complete (commits f0a453c..f3708bb, 2 fix rounds)

## PHASE 1 COMPLETE
Foundation tasks 1-3 complete, all tests passing, all code review findings addressed.
Branch ready for Tasks 4-11 planning.

## PHASE 1 CONTINUED — Tasks 4-11 (branch: phase1-tasks-4-11)
Task 4: complete (commit dd9463b, spec compliant, code review clean, 0 fix rounds)
Task 5: fix round 1/1 (API key log leak fixed + verified, malformed-response guard added; commits 686cb1d..85171f7)
Task 5: complete (commits 686cb1d..85171f7, 1 fix round)
Task 6: fix round 1/1 (reassignment DbUpdateException bug fixed at root cause, error handling + tests added; commits 880de96..35f79b8; fix subagent hit a rate limit mid-work, orchestrator completed it directly)
Task 6: complete (commits 880de96..35f79b8, 1 fix round)
Task 7: fix round 1/1 (missing error handling + terminal-booking gap fixed, tests added; commits 3447947..1207864)
Task 7: complete (commits 3447947..1207864, 1 fix round)
Task 8: fix round 1/1 (unguarded notification-status save fixed, 5 missing tests added; commits 37f3a3e..0494b1f)
Task 8: complete (commits 37f3a3e..0494b1f, 1 fix round)
Task 9: fix round 1/1 (Calendar-channel dead-letter fix + per-item exception isolation added; commits 632c1f9..91cdd20)
Task 9: complete (commits 632c1f9..91cdd20, 1 fix round)
Task 10: fix round 1/1 (Docker build context bug, permissions, CI gate fixed; commits 257186e..e36e096)
Task 10: complete (commits 257186e..e36e096, 1 fix round)
Task 11: fix round 1/2 (.env gitignore, curl in runtime image, rollback tag guidance fixed; commits de950bb..cc18e63)
Task 11: fix round 2/2 (rollback guidance corrected to point at GitHub Releases/git tags; commits cc18e63..4a162ef)
Task 11: complete (commits de950bb..4a162ef, 2 fix rounds)

## PHASE 1 (TASKS 4-11) COMPLETE
All 8 tasks (4-11) implemented, spec-reviewed, code-reviewed, and fix rounds resolved.

Final holistic cross-branch review (commit f6cb8ad): found 2 Critical state-machine bugs
visible only when Task 6 (driver assignment) and Task 7 (driver trip lifecycle) are traced
together — mid-trip driver reassignment could corrupt an in-progress trip's status/assignment,
and the admin status dropdown could jump a booking to "Driver_Assigned" without ever creating
a DriverAssignment row. Both fixed and independently verified (commit dfe5fa4, incl. mutation
testing to confirm the new guards are load-bearing). 3 Important findings (admin auth using
plaintext compare instead of the PasswordHasher pattern; no TLS in the Task 11 deploy pipeline;
deploy.yml not checking new_release_published) surfaced to the user as follow-up recommendations
rather than fixed now — they involve infra/architecture tradeoffs beyond a quick correctness patch.

Branch phase1-tasks-4-11 ready for merge decision. Final test count: 67/67 passing.

## LIVE TESTING (commit 3c71ddc)
User asked to actually run the app before deciding merge/PR/discard. Stood up the real stack
via Docker (run.bat: builds the image, starts Docker Desktop if needed, docker-compose up,
waits for /health). This is the first time in the project's history any environment had a
reachable database.

Found and fixed a Critical, previously-undetectable bug: two migrations from before Task 4
(AddUniqueConstraints, AddLuggageFeeToProductSetting) had no .Designer.cs, so EF Core silently
skipped them on every MigrateAsync() call, in every environment, always. First real booking
submission failed with "column p.LuggageFeePerExtra does not exist". Also found the 3 unique
constraints those migrations were meant to add were never in the C# model either. Fixed by
adding them properly via Fluent API and squashing all migrations into one fresh,
tool-verified InitialCreate (safe — no environment has ever had these migrations applied to
real data).

Re-verified end-to-end against the corrected schema: customer booking (blocked gracefully on
missing pricing seed data and placeholder Google Maps key — expected, not bugs), admin
login/dashboard, driver creation/login, and the full assign -> accept -> pick up -> complete
lifecycle. Both of this branch's earlier critical state-machine fixes (mid-trip reassignment
block, direct Driver_Assigned jump block) were exercised live via crafted requests and held
up correctly, including a full audit-trail check in BookingStatusHistory.

Final test count unchanged: 67/67 passing. Branch genuinely ready for a merge decision now,
verified by both automated tests and live use, not just automated tests.
