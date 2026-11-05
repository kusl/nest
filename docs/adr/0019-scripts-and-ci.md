# 0019 Scripts and CI

Status: accepted

- All logic is in `scripts/`. Each script uses `set -euo pipefail`, can be rerun, and cleans its own output in `artifacts/`.
- `run-all.sh` runs build, unit, JavaScript, end-to-end and export, with `--skip-e2e`, `--skip-js` and `--skip-export`. Exit codes: 0 success, 2 usage, 127 missing tool, otherwise the failing step's code.
- Podman runs with `--security-opt label=disable`; the repository is mounted read-only and copied inside the container; NuGet packages are cached in a named volume.
- `ci.yml` runs `run-all.sh` on every push. `velixir.yml` deploys on every push to `main`.
