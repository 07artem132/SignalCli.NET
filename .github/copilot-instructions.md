# Copilot / AI agent instructions

The canonical guidance for AI coding agents in this repository lives in
[`CLAUDE.md`](../CLAUDE.md) at the repo root.

Please read it before generating or editing code. It covers:

- project overview, build & test commands;
- architecture and the patterns in use;
- C# conventions (match the existing code; comments/logs are in Ukrainian);
- **critical non-regression rules** (no PII above `Trace`, `ArgumentList` for process
  args, attachment filename sanitization, composite event dispatch, System.Text.Json
  with source generation, SHA-256 verification + **ASCII-only, no BOM** in the
  download scripts so they parse under Windows PowerShell 5.1);
- the OpenSpec planning workflow under `openspec/`.
