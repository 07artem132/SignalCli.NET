# Tasks — json-hardening-source-gen-attribute

## 0. Setup

- [ ] 0.1 On branch `fix/post-audit-remediation` (already created for the
        previous 3-commit batch); this OpenSpec change adds one more commit on
        top. Alternatively branch off this branch if review feedback wants the
        json-hardening fix isolated — but the simpler path is one more commit.
- [ ] 0.2 Run `npx -y @fission-ai/openspec@latest validate json-hardening-source-gen-attribute --strict`
        and confirm green before any source edits.

## 1. Pre-flight grep (capability `json-hardening-source-gen`)

- [ ] 1.1 `grep -rn '"id":"[^"]*","id"' Tests/SignalCli.Tests/` to find any
        test that already feeds duplicate `id` keys. Expectation: zero hits.
- [ ] 1.2 Manual review: scan `Tests/SignalCli.Tests/JsonRpcErrorTests.cs`,
        `JsonRpcClientTests.cs`, `JsonSerializationTests.cs` for any literal
        JSON string with a repeated key in one object. Expectation: only the
        two RG05 facts (intentional).

## 2. Production source edit (capability `json-hardening-source-gen`)

- [ ] 2.1 Add `AllowDuplicateProperties = false` to the existing
        `[JsonSourceGenerationOptions(...)]` attribute on `SignalJsonContext`
        (`src/SignalCli/Serialization/SignalJsonContext.cs:23`):

        ```csharp
        [JsonSourceGenerationOptions(
            GenerationMode = JsonSourceGenerationMode.Default,
            AllowDuplicateProperties = false)]
        ```

- [ ] 2.2 Run `dotnet build src/SignalCli/SignalCli.csproj` and verify the
        generated source under `obj/Debug/net10.0/generated/System.Text.Json.SourceGeneration/`
        emits the duplicate-check (manually inspect one of the
        `JsonRpcResponse.g.cs` files for a `BitArray`/`Span<int>`-style guard
        in the deserialize-loop).

## 3. Test pinning (capability `json-hardening-source-gen`)

- [ ] 3.1 In `Tests/SignalCli.Tests/JsonSerializationTests.cs` (RG05 block),
        REMOVE the inline "КАВЕAT: source-gen Default fast-path…" block — it
        stops being true after §2.1.
- [ ] 3.2 Add a third fact next to the two existing RG05 facts:

        ```csharp
        /// <summary>
        /// RG05 (частина 3) — пінує що source-gen контекст ТЕЖ throw'ить на
        /// duplicate-key (а не лише runtime-flag і JsonDocument-level API).
        /// Після json-hardening-source-gen-attribute §2.1 ця гарантія діє на
        /// production-шляху через SignalJsonContext.Default.JsonRpcResponse,
        /// що його реально використовує JsonRpcClient.ProcessMessageAsync.
        /// </summary>
        [Fact]
        public void SignalJsonContext_AllowDuplicateProperties_ThrowsOnDuplicateKey()
        {
            const string duplicateKey = """{"id":"1","id":"2","jsonrpc":"2.0"}""";
            Assert.Throws<JsonException>(() =>
                JsonSerializer.Deserialize(duplicateKey, SignalJsonContext.Default.JsonRpcResponse));
        }
        ```

- [ ] 3.3 `dotnet test Tests/SignalCli.Tests/SignalCli.Tests.csproj` — all
        287+ tests green (was 286 + 1 new). Pre-§2.1 baseline of this exact
        test was RED (per audit v2.1 implementation experience); now GREEN
        proves the attribute change took effect.

## 4. Doc tightening

- [ ] 4.1 In `CLAUDE.md` rule #18, replace:

        > `SignalJson.Options` SHALL set `AllowDuplicateProperties = false`

        with:

        > Both production JSON layers SHALL reject duplicate keys: (a)
        > `SignalJson.Options.AllowDuplicateProperties = false` (runtime flag,
        > covers any reflection-based call-site like `OptionsForTests`);
        > (b) `[JsonSourceGenerationOptions(AllowDuplicateProperties = false)]`
        > on `SignalJsonContext` (source-gen attribute — covers every
        > `SignalJsonContext.Default.X` call-site which is what production
        > actually uses). Both layers are required because they cover
        > orthogonal code paths; removing either silently weakens the
        > defense-in-depth.

- [ ] 4.2 `CLAUDE.md` "Audit baseline — Regression guards" table row for RG05
        gains "+source-gen path" to its description column.

## 5. Verify + commit

- [ ] 5.1 `dotnet build SignalCli.sln` → 0 warnings, 0 errors.
- [ ] 5.2 `dotnet test` → 287 passed (286 baseline + 1 new RG05 fact).
- [ ] 5.3 `git commit` with message:

        ```
        fix: source-gen JSON hardening — close AllowDuplicateProperties bypass

        json-hardening-source-gen-attribute: SignalJsonContext gains
        AllowDuplicateProperties=false on its [JsonSourceGenerationOptions]
        attribute. CLAUDE.md rule #18 was set on the runtime SignalJson.Options
        flag, but the only production call-sites use SignalJsonContext.Default.X
        (source-gen GenerationMode=Default fast-path) which bypasses the flag.
        Fix shipped after audit v2.1 RG05 surfaced the bypass during T03 work.
        ```

## 6. Post-merge archive

- [ ] 6.1 After PR merges, run from the archive workflow in CLAUDE.md:
        `npx -y @fission-ai/openspec@latest archive json-hardening-source-gen-attribute --yes --skip-specs`
- [ ] 6.2 Update CLAUDE.md "Implemented, merged, archived" list to include the
        new archive entry.
