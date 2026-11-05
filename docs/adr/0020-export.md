# 0020 Export dump rules

Status: accepted

- `scripts/export.sh` writes `docs/llm/dump.txt` from files git tracks or would track.
- Excluded: `docs/llm/`, build and tool folders, binary files, `.env` files, `package-lock.json` and `LICENSE`.
- Included: all source, config, scripts, workflows, test fixtures and the rest of `docs/`, including these ADRs.
- Nothing is redacted. The repository holds no secrets.
