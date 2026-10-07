[🇧🇷 Português](performance.md)

# Performance baseline

The repository uses k6 to detect performance regressions automatically without making every pull request slower.

## Strategy

The `.github/workflows/performance.yml` workflow runs:

- weekly;
- on demand through `workflow_dispatch`.

It starts the same secure platform profile used elsewhere, including Vault, dynamic PostgreSQL credentials, RabbitMQ and Keycloak.

The benchmark calls Orders directly at `http://localhost:8081`. This is intentional: the baseline measures the Orders service plus persistence/outbox path without mixing the result with YARP Gateway rate limiting.

## Scenario

`tests/performance/orders-baseline.js`:

1. gradually ramps to 5 virtual users;
2. creates authenticated orders;
3. holds a small reproducible load;
4. ramps down.

## Thresholds

The test fails when:

- `http_req_failed >= 1%`;
- `p95(http_req_duration) >= 1000 ms`;
- fewer than 99% of checks confirm HTTP 201.

These thresholds are a portfolio baseline rather than a final commercial SLO. A production system should define thresholds from real traffic, capacity and business objectives.

## Why it does not run on every PR

Performance tests are more sensitive to shared-runner variation and materially increase feedback time.

The primary pipeline remains fast and deterministic. The weekly workflow detects trends/regressions, while performance-sensitive changes can run the baseline manually before merge.

## Future evolution

A natural next step is storing historical p50/p95/p99 series and adding separate read, write, messaging and saturation scenarios.
