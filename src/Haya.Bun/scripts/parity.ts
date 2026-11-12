// Parity check: runs the .NET tool (Haya.Tool) on examples/dotnet and the Bun CLI on examples/typescript/example (and the
// custom attribute examples next to them),
// then diffs every output. The source path and the language the tools stamp are the only substitutions.
// Each tool runs from its own example directory, because ComponentSource is relative to the current directory.
//
//   bun run parity            # needs the .NET SDK (the baseline) and `bun install` in examples/typescript/example and examples/typescript/custom-annotations
import { mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const repo = resolve(import.meta.dir, "../../..");
const dotnetCwd = join(repo, "examples/dotnet/Example");
const tsCwd = join(repo, "examples/typescript/example");
const hayaTs = join(repo, "src/Haya.Bun/haya.ts");
const dotnetSln = join(repo, "examples/dotnet/Example.sln");
const tool = join(repo, "src/Haya.Tool");
const out = mkdtempSync(join(tmpdir(), "haya-parity-"));

// Each case: output file name and the CLI arguments (without the input path, which goes last for Argu).
const cases: { file: string; args: string[] }[] = [
  { file: "describe.json", args: ["describe", "-o", "describe.json"] },
  { file: "describe.yaml", args: ["describe", "-f", "yaml", "-o", "describe.yaml"] },
  { file: "crc.md", args: ["crc", "-o", "crc.md"] },
  { file: "crc-c.md", args: ["crc", "-c", "-o", "crc-c.md"] },
  { file: "crc-cc.md", args: ["crc", "-cc", "-o", "crc-cc.md"] },
  { file: "diagram.mermaid", args: ["diagram", "-o", "diagram.mermaid"] },
  { file: "diagram-l2.mermaid", args: ["diagram", "--detail", "apps", "-o", "diagram-l2.mermaid"] },
  { file: "diagram-l3.mermaid", args: ["diagram", "--detail", "components", "-o", "diagram-l3.mermaid"] },
  { file: "diagram.html", args: ["diagram", "-f", "html", "-o", "diagram.html"] },
  { file: "diagram-components.html", args: ["diagram", "-f", "html", "--detail", "components", "-o", "diagram-components.html"] },
  { file: "describe-members.json", args: ["describe", "--detail", "members", "-o", "describe-members.json"] },
  { file: "describe-members.yaml", args: ["describe", "-f", "yaml", "--detail", "members", "-o", "describe-members.yaml"] },
  { file: "diagram-members.html", args: ["diagram", "-f", "html", "--detail", "members", "-o", "diagram-members.html"] },
  { file: "diagram-members.mermaid", args: ["diagram", "--detail", "members", "-o", "diagram-members.mermaid"] },
  { file: "backstage.yaml", args: ["backstage", "-o", "backstage.yaml"] },
  { file: "backstage.json", args: ["backstage", "-f", "json", "-o", "backstage.json"] },
  { file: "datadog.yaml", args: ["datadog", "-o", "datadog.yaml"] },
  { file: "datadog.json", args: ["datadog", "-f", "json", "-o", "datadog.json"] },
];

// Never read the developer's own global config: both tools look here instead (and find nothing unless a case writes it).
process.env.HAYA_CONFIG_DIR = join(out, "global-config");

const run = (cmd: string[], cwd: string, env: Record<string, string> = {}) => {
  const p = Bun.spawnSync(cmd, { cwd, stdout: "pipe", stderr: "pipe", env: { ...process.env, ...env } });
  if (p.exitCode !== 0) {
    console.error(`FAILED: ${cmd.join(" ")}\n${p.stdout.toString()}${p.stderr.toString()}`);
    process.exit(1);
  }
};

console.log("Building Haya.Tool (.NET baseline)...");
run(["dotnet", "build", tool, "--nologo", "-v", "q"], repo);

// Several describe files at once (same files fed to both tools): merge, cross-links and legacy (0.0) input.
const fixtures = [join(repo, "src/Haya.Bun/scripts/fixtures/ordering.json"), join(repo, "src/Haya.Bun/scripts/fixtures/billing.yaml")];
const multiCases: { file: string; args: string[] }[] = [
  { file: "multi-describe.json", args: ["describe", "-o", "multi-describe.json"] },
  { file: "multi-describe-members.json", args: ["describe", "--detail", "members", "-o", "multi-describe-members.json"] },
  { file: "multi-crc.md", args: ["crc", "-c", "-o", "multi-crc.md"] },
  { file: "multi-crc-cc.md", args: ["crc", "-cc", "-o", "multi-crc-cc.md"] },
  { file: "multi-diagram.mermaid", args: ["diagram", "-o", "multi-diagram.mermaid"] },
  { file: "multi-diagram-l2.mermaid", args: ["diagram", "--detail", "apps", "-o", "multi-diagram-l2.mermaid"] },
  { file: "multi-diagram-l3.mermaid", args: ["diagram", "--detail", "components", "-o", "multi-diagram-l3.mermaid"] },
  { file: "multi-diagram.html", args: ["diagram", "-f", "html", "-o", "multi-diagram.html"] },
  { file: "multi-diagram-components.html", args: ["diagram", "-f", "html", "--detail", "components", "-o", "multi-diagram-components.html"] },
  { file: "multi-backstage.yaml", args: ["backstage", "-o", "multi-backstage.yaml"] },
  { file: "multi-datadog.yaml", args: ["datadog", "-o", "multi-datadog.yaml"] },
];

// A bigger set: one describe file per system, JSON and YAML mixed (examples/org-explorer).
const orgDir = join(repo, "examples/org-explorer/describe");
const orgFiles = readdirSync(orgDir).sort().map((f) => join(orgDir, f));
const orgCases: { file: string; args: string[] }[] = [
  { file: "org-describe.json", args: ["describe", "-o", "org-describe.json"] },
  { file: "org-crc-cc.md", args: ["crc", "-cc", "-o", "org-crc-cc.md"] },
  { file: "org-diagram.mermaid", args: ["diagram", "-o", "org-diagram.mermaid"] },
  { file: "org-diagram-l2.mermaid", args: ["diagram", "--detail", "apps", "-o", "org-diagram-l2.mermaid"] },
  { file: "org-diagram-l3.mermaid", args: ["diagram", "--detail", "components", "-o", "org-diagram-l3.mermaid"] },
  { file: "org-diagram.html", args: ["diagram", "-f", "html", "-o", "org-diagram.html"] },
  { file: "org-diagram-components.html", args: ["diagram", "-f", "html", "--detail", "components", "-o", "org-diagram-components.html"] },
  { file: "org-describe-members.yaml", args: ["describe", "-f", "yaml", "--detail", "members", "-o", "org-describe-members.yaml"] },
  { file: "org-diagram-members.html", args: ["diagram", "-f", "html", "--detail", "members", "-o", "org-diagram-members.html"] },
  { file: "org-backstage.yaml", args: ["backstage", "-o", "org-backstage.yaml"] },
  { file: "org-datadog.yaml", args: ["datadog", "-o", "org-datadog.yaml"] },
];

let failed = 0;
const total = cases.length * 2 + multiCases.length + orgCases.length;
// (the configuration cases below are counted as they run)
const check = (file: string, net: string, actual: string) => {
  const expected = readFileSync(net, "utf8");
  if (expected === actual) {
    console.log(`PASS  ${file}`);
  } else {
    failed++;
    console.log(`FAIL  ${file}`);
    const mapped = join(out, "bun-mapped-" + file);
    Bun.write(mapped, actual);
    const d = Bun.spawnSync(["diff", "-u", net, mapped], { stdout: "pipe" });
    console.log(d.stdout.toString());
  }
};
// The same describe files fed to both tools, so nothing is mapped.
const compareFiles = (list: { file: string; args: string[] }[], files: string[]) => {
  for (const { file, args } of list) {
    const net = join(out, "net-" + file);
    const bun = join(out, "bun-" + file);
    const withOut = (a: string[], o: string) => a.map((x) => (x === file ? o : x));
    run(["dotnet", "run", "--no-build", "--project", tool, "--", ...withOut(args, net), ...files], dotnetCwd);
    run(["bun", "run", hayaTs, ...withOut(args, bun), ...files], tsCwd);
    check(file, net, readFileSync(bun, "utf8"));
  }
};
compareFiles(multiCases, fixtures);
compareFiles(orgCases, orgFiles);
// The two tools name different languages by design (the .NET tool stamps `dotnet`, the Bun tool `javascript`), so the
// language of the Bun output is mapped to the .NET one before comparing.
// Datadog calls them `dotnet` and `js` in `spec.languages`.
const sameLanguage = (text: string) =>
  text
    .replaceAll('"language": "javascript"', '"language": "dotnet"')
    .replaceAll("language: javascript", "language: dotnet")
    .replace(/("languages": \[\s*)"js"/g, '$1"dotnet"')
    .replace(/(languages:\n\s+- )js\n/g, "$1dotnet\n")
    // a class's technology comes from its file extension in the component diagram: .ts against .cs
    .replace(/^(\s*Component\([^,]+, "[^"]*", )"TypeScript"/gm, '$1"C#"');
// `--detail members` (ADR017): each compiler prints types in its own short form and each language has its own naming
// convention, so these known differences are mapped from the TypeScript side to the C# one. Only in member and
// parameter `type` values, and in the first letter of a member's name (not a parameter's or class's):
//   Promise<void> -> Task, Promise<T> -> Task<T>, number -> int, boolean -> bool; pay -> Pay
// `<` is written as \u003c in the html page's script-safe JSON.
const csharpType = (t: string) => {
  const escaped = t.includes("\\u003c");
  const mapped = t
    .replaceAll("\\u003c", "<")
    .replace(/Promise<void>/g, "Task")
    .replace(/\bPromise\b/g, "Task")
    .replace(/\bnumber\b/g, "int")
    .replace(/\bboolean\b/g, "bool");
  return escaped ? mapped.replaceAll("<", "\\u003c") : mapped;
};
const sameMembers = (text: string) =>
  text
    // json, compact or indented
    .replace(/("type":\s*")([^"]*)(")/g, (_, a, t, b) => a + csharpType(t) + b)
    .replace(/("kind":\s*"(?:Method|Property|Field|Event)",\s*"name":\s*")(\w)/g, (_, a, c) => a + c.toUpperCase())
    // yaml
    .replace(/^(\s*type: )(.*)$/gm, (_, a, t) => a + csharpType(t))
    .replace(/(kind: (?:Method|Property|Field|Event)\s*\n\s*name: )(\w)/g, (_, a, c) => a + c.toUpperCase());
// The same comparison on a pair of example projects. `tsPath` -> `netPath` and the language are the only substitutions.
const compareExamples = (prefix: string, dotnetDir: string, sln: string, tsDir: string, tsPath: string, netPath: string) => {
  for (const { file, args } of cases) {
    const label = prefix + file;
    const net = join(out, "net-" + label);
    const bun = join(out, "bun-" + label);
    const withOut = (a: string[], o: string) => a.map((x) => (x === file ? o : x));
    run(["dotnet", "run", "--no-build", "--project", tool, "--", ...withOut(args, net), sln], dotnetDir);
    run(["bun", "run", hayaTs, ...withOut(args, bun), "."], tsDir);
    check(label, net, sameMembers(sameLanguage(readFileSync(bun, "utf8").replaceAll(tsPath, netPath))));
  }
};
compareExamples("", dotnetCwd, dotnetSln, tsCwd, "src/ecomm.ts", "HayaEcomm.cs");
// Custom attributes / annotations with defaults (examples/dotnet-custom-attributes, examples/typescript/custom-annotations)
compareExamples(
  "custom-",
  join(repo, "examples/dotnet-custom-attributes"),
  join(repo, "examples/dotnet-custom-attributes/CustomAttributes.sln"),
  join(repo, "examples/typescript/custom-annotations"),
  "src/checkout.ts",
  "Checkout/Checkout.cs",
);

// Configuration (ADR020): a local .haya/config.json, a global one and an environment variable, no flags. Both tools run in
// their own directory with the same files and must write the same files with the same content.
const configJson = {
  output: { path: "generated", formats: ["yaml"] },
  crc: { diagram: "apps" },
  diagram: { format: "html", detail: "components" },
  backstage: { format: "json" },
  viewer: { detail: "apps", complexity: { systems: [2, 3, 4, 5], links: [1, 2, 3, 4] } },
};
const configCommands = [["crc"], ["describe"], ["diagram"], ["backstage"], ["datadog"]];
const configRuns: Record<string, string>[] = [{}, { HAYA_VIEWER__DETAIL: "components", HAYA_DIAGRAM__FORMAT: "mermaid" }];
const configResult = (cwd: string, env: Record<string, string>) => {
  mkdirSync(join(cwd, ".haya"), { recursive: true });
  writeFileSync(join(cwd, ".haya/config.json"), JSON.stringify(configJson));
  mkdirSync(process.env.HAYA_CONFIG_DIR!, { recursive: true });
  writeFileSync(join(process.env.HAYA_CONFIG_DIR!, "config.json"), JSON.stringify({ output: { path: "overridden" }, backstage: { output: "generated/catalog.json" } }));
  for (const c of configCommands) {
    run(cwd === join(out, "config-net") ? ["dotnet", "run", "--no-build", "--project", tool, "--", ...c, ...orgFiles] : ["bun", "run", hayaTs, ...c, ...orgFiles], cwd, env);
  }
  const dir = join(cwd, "generated");
  return Object.fromEntries(readdirSync(dir).sort().map((f) => [f, readFileSync(join(dir, f), "utf8")]));
};
let configTotal = 0;
configRuns.forEach((env, i) => {
  const net = join(out, `config-net`), bun = join(out, `config-bun`);
  for (const d of [net, bun]) { rmSync(d, { recursive: true, force: true }); mkdirSync(d, { recursive: true }); }
  const a = configResult(net, env), b = configResult(bun, env);
  const names = [...new Set([...Object.keys(a), ...Object.keys(b)])].sort();
  for (const n of names) {
    configTotal++;
    const label = `config-${i}-${n}`;
    if (a[n] !== undefined && a[n] === b[n]) console.log(`PASS  ${label}`);
    else { failed++; console.log(`FAIL  ${label} (${a[n] === undefined ? "missing from .NET" : b[n] === undefined ? "missing from Bun" : "content differs"})`); }
  }
});
rmSync(out, { recursive: true, force: true });
if (failed > 0) {
  console.error(`${failed} of ${total + configTotal} outputs differ`);
  process.exit(1);
}
console.log(`All ${total + configTotal} outputs identical`);
