---
paths:
  - ".claude/**"
  - "docs/cloud-development.md"
---

# Cloud development

For Claude Code on the Web sessions, see [`docs/cloud-development.md`](../../docs/cloud-development.md). A `SessionStart` hook (`.claude/hooks/session-start.sh`) installs the SDK pinned in `global.json` via `dotnet-install.sh` (distro apt `dotnet-sdk-10.0` is 10.0.1xx and doesn't satisfy it) and pre-warms NuGet for `Tests/SignalCli.Tests` — runs only when `CLAUDE_CODE_REMOTE=true`, so local workflows are untouched.
