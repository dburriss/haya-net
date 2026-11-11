// Parity check: runs the .NET tool (Haya.Tool) on examples/dotnet and the Bun CLI on examples/typescript/example (and the
// custom attribute examples next to them),
// then diffs every output. The source path and the language the tools stamp are the only substitutions.
// Each tool runs from its own example directory, because ComponentSource is relative to the current directory.
//
//   bun run parity            # needs the .NET SDK (the baseline) and `bun install` in examples/typescript/example and examples/typescript/custom-annotations
import { mkdtempSync, readdirSync, readFileSync, rmSync } from "node:fs";
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
  { file: "diagram-l2.mermaid", args: ["diagram", "-t", "c4container", "-o", "diagram-l2.mermaid"] },
  { file: "diagram-l3.mermaid", args: ["diagram", "-t", "c4component", "-o", "diagram-l3.mermaid"] },
  { file: "diagram.html", args: ["diagram", "-f", "html", "-o", "diagram.html"] },
  { file: "backstage.yaml", args: ["backstage", "-o", "backstage.yaml"] },
  { file: "backstage.json", args: ["backstage", "-f", "json", "-o", "backstage.json"] },
  { file: "datadog.yaml", args: ["datadog", "-o", "datadog.yaml"] },
  { file: "datadog.json", args: ["datadog", "-f", "json", "-o", "datadog.json"] },
];

const run = (cmd: string[], cwd: string) => {
  const p = Bun.spawnSync(cmd, { cwd, stdout: "pipe", stderr: "pipe" });
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
  { file: "multi-crc.md", args: ["crc", "-c", "-o", "multi-crc.md"] },
  { file: "multi-crc-cc.md", args: ["crc", "-cc", "-o", "multi-crc-cc.md"] },
  { file: "multi-diagram.mermaid", args: ["diagram", "-o", "multi-diagram.mermaid"] },
  { file: "multi-diagram-l2.mermaid", args: ["diagram", "-t", "c4container", "-o", "multi-diagram-l2.mermaid"] },
  { file: "multi-diagram-l3.mermaid", args: ["diagram", "-t", "c4component", "-o", "multi-diagram-l3.mermaid"] },
  { file: "multi-diagram.html", args: ["diagram", "-f", "html", "-o", "multi-diagram.html"] },
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
  { file: "org-diagram-l2.mermaid", args: ["diagram", "-t", "c4container", "-o", "org-diagram-l2.mermaid"] },
  { file: "org-diagram-l3.mermaid", args: ["diagram", "-t", "c4component", "-o", "org-diagram-l3.mermaid"] },
  { file: "org-diagram.html", args: ["diagram", "-f", "html", "-o", "org-diagram.html"] },
  { file: "org-backstage.yaml", args: ["backstage", "-o", "org-backstage.yaml"] },
  { file: "org-datadog.yaml", args: ["datadog", "-o", "org-datadog.yaml"] },
];

let failed = 0;
const total = cases.length * 2 + multiCases.length + orgCases.length;
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
// The same comparison on a pair of example projects. `tsPath` -> `netPath` and the language are the only substitutions.
const compareExamples = (prefix: string, dotnetDir: string, sln: string, tsDir: string, tsPath: string, netPath: string) => {
  for (const { file, args } of cases) {
    const label = prefix + file;
    const net = join(out, "net-" + label);
    const bun = join(out, "bun-" + label);
    const withOut = (a: string[], o: string) => a.map((x) => (x === file ? o : x));
    run(["dotnet", "run", "--no-build", "--project", tool, "--", ...withOut(args, net), sln], dotnetDir);
    run(["bun", "run", hayaTs, ...withOut(args, bun), "."], tsDir);
    check(label, net, sameLanguage(readFileSync(bun, "utf8").replaceAll(tsPath, netPath)));
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
rmSync(out, { recursive: true, force: true });
if (failed > 0) {
  console.error(`${failed} of ${total} outputs differ`);
  process.exit(1);
}
console.log(`All ${total} outputs identical`);
