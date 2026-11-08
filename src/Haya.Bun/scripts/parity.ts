// Parity check: runs the .NET tool (Haya.Tool) on examples/dotnet and the Bun CLI on examples/typescript/example,
// then diffs every output. The source path is the only substitution (`HayaEcomm.cs` -> `src/ecomm.ts`).
// Each tool runs from its own example directory, because ComponentSource is relative to the current directory.
//
//   bun run parity            # needs the .NET SDK (the baseline) and `bun install` in examples/typescript/example
import { mkdtempSync, readFileSync, rmSync } from "node:fs";
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
  { file: "backstage.yaml", args: ["backstage", "-o", "backstage.yaml"] },
  { file: "backstage.json", args: ["backstage", "-f", "json", "-o", "backstage.json"] },
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

let failed = 0;
for (const { file, args } of cases) {
  const net = join(out, "net-" + file);
  const bun = join(out, "bun-" + file);
  const withOut = (a: string[], o: string) => a.map((x) => (x === file ? o : x));
  run(["dotnet", "run", "--no-build", "--project", tool, "--", ...withOut(args, net), dotnetSln], dotnetCwd);
  run(["bun", "run", hayaTs, ...withOut(args, bun), "."], tsCwd);
  const expected = readFileSync(net, "utf8");
  const actual = readFileSync(bun, "utf8").replaceAll("src/ecomm.ts", "HayaEcomm.cs");
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
}
rmSync(out, { recursive: true, force: true });
if (failed > 0) {
  console.error(`${failed} of ${cases.length} outputs differ`);
  process.exit(1);
}
console.log(`All ${cases.length} outputs identical`);
