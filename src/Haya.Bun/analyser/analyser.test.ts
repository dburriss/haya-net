import { afterAll, describe, expect, test } from "bun:test";
import { mkdirSync, mkdtempSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { AnalysisError, analyse, type CollaboratorInfo, type DescriptorInfo } from "./analyser.ts";

const annotations = resolve(import.meta.dir, "../../Haya.TypeScript");
const roots: string[] = [];

/** Create a throwaway project with `@haya/annotations` installed and the given source files. */
const project = (files: Record<string, string>): string => {
  const root = mkdtempSync(join(tmpdir(), "haya-analyser-"));
  roots.push(root);
  mkdirSync(join(root, "node_modules/@haya"), { recursive: true });
  symlinkSync(annotations, join(root, "node_modules/@haya/annotations"));
  writeFileSync(join(root, "package.json"), '{"name":"fixture","type":"module"}');
  writeFileSync(
    join(root, "tsconfig.json"),
    JSON.stringify({ compilerOptions: { target: "ES2022", module: "ESNext", moduleResolution: "bundler", strict: true, noEmit: true }, include: ["src"] }),
  );
  for (const [name, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, name)), { recursive: true });
    writeFileSync(join(root, name), text);
  }
  return root;
};

afterAll(() => roots.forEach((r) => rmSync(r, { recursive: true, force: true })));

const imports = `import { Collaborator, Direction, meta, Relationship, Responsibility } from "@haya/annotations";\n`;
const problemsOf = (root: string): string[] => {
  try {
    analyse(root, root);
  } catch (e) {
    if (e instanceof AnalysisError) return e.problems.map((p) => `${p.file}:${p.line}:${p.column} ${p.message}`);
    throw e;
  }
  throw new Error("expected an AnalysisError");
};

describe("analyse", () => {
  test("reads responsibilities, collaborators and meta with defaults", () => {
    const root = project({
      "src/a.ts":
        imports +
        `meta({ appName: "App", system: "Sys" });
@Responsibility({ description: "Does things" })
@Collaborator({ appName: "Other", direction: Direction.Upstream, relationship: Relationship.External, technology: "Go", description: \`tpl\` })
export class Thing {}
@Collaborator({ appName: "Plain" })
export class Plain {}`,
    });
    const ds = analyse(root, root);
    expect(ds.map((d) => d.kind)).toEqual(["responsibility", "collaborator", "collaborator", "meta"]);
    expect(ds[0]).toEqual({ kind: "responsibility", description: "Does things", componentName: "Thing", componentSource: "src/a.ts" });
    const other = ds[1] as CollaboratorInfo;
    expect(other).toMatchObject({ appName: "Other", direction: "Upstream", relationship: "External", tech: "Go", description: "tpl", protocol: "", owner: "" });
    // unset properties default like Haya.Roslyn: "", Downstream, Internal
    expect(ds[2]).toMatchObject({ appName: "Plain", direction: "Downstream", relationship: "Internal", tech: "", protocol: "" });
    expect(ds[3]).toEqual({ kind: "meta", appName: "App", description: "", system: "Sys", repository: "", owner: "" });
  });

  test("orders responsibilities, then collaborators, then meta; files by path; repeated decorators in source order", () => {
    const root = project({
      "src/b.ts": imports + `@Collaborator({ appName: "B1" })\n@Collaborator({ appName: "B2" })\n@Responsibility({ description: "rb" })\nexport class B {}`,
      "src/a.ts": imports + `meta({ appName: "M" });\n@Responsibility({ description: "ra" })\nexport class A {}\n@Collaborator({ appName: "A1" })\nexport class A2 {}`,
    });
    const ds = analyse(root, root);
    const label = (d: DescriptorInfo) => (d.kind === "responsibility" ? `R:${d.description}` : d.kind === "collaborator" ? `C:${d.appName}` : `M:${d.appName}`);
    expect(ds.map(label)).toEqual(["R:ra", "R:rb", "C:A1", "C:B1", "C:B2", "M:M"]);
  });

  test("resolves by symbol: import aliases and namespace imports work, same-named functions elsewhere are ignored", () => {
    const root = project({
      "src/fake.ts": `export const Responsibility = (_o: { description: string }) => (_t: unknown, _c: unknown) => {};`,
      "src/a.ts": `import { Responsibility as R } from "@haya/annotations";\nimport * as haya from "@haya/annotations";
@R({ description: "aliased" })
export class A {}
@haya.Collaborator({ appName: "ns" })
export class B {}`,
      "src/b.ts": `import { Responsibility } from "./fake.ts";\n@Responsibility({ description: "not haya" })\nexport class C {}`,
    });
    const ds = analyse(root, root);
    expect(ds.map((d) => d.kind === "responsibility" ? d.description : d.kind === "collaborator" ? d.appName : "")).toEqual(["aliased", "ns"]);
  });

  test("ComponentSource is relative to the current directory with forward slashes", () => {
    const root = project({ "src/deep/x.ts": imports + `@Responsibility({ description: "r" })\nexport class X {}` });
    expect(analyse(root, root)[0]).toMatchObject({ componentSource: "src/deep/x.ts" });
    expect(analyse(root, join(root, "src"))[0]).toMatchObject({ componentSource: "deep/x.ts" });
  });

  test("accepts a tsconfig.json path as well as a directory", () => {
    const root = project({ "src/a.ts": imports + `@Responsibility({ description: "r" })\nexport class A {}` });
    expect(analyse(join(root, "tsconfig.json"), root)).toHaveLength(1);
  });

  test("returns nothing when there are no annotations", () => {
    const root = project({ "src/a.ts": `export class A {}` });
    expect(analyse(root, root)).toEqual([]);
  });
});

describe("non-static values", () => {
  test("identifier, template substitution and computed enum are reported with file and line", () => {
    const root = project({
      "src/a.ts":
        imports +
        `const text = "x";
@Responsibility({ description: text })
export class A {}
@Collaborator({ appName: \`a\${text}\` })
export class B {}
const dir = Direction.Upstream;
@Collaborator({ appName: "c", direction: dir })
export class C {}`,
    });
    const problems = problemsOf(root);
    expect(problems).toHaveLength(3);
    expect(problems[0]).toMatch(/^src\/a\.ts:3:32 'description' must be a string literal/);
    expect(problems[1]).toMatch(/^src\/a\.ts:5:26 'appName' must be a string literal/);
    expect(problems[2]).toMatch(/^src\/a\.ts:8:42 'direction' must be a Direction member/);
  });

  test("spread, shorthand, computed names, non-object arguments and unknown options are errors", () => {
    const root = project({
      "src/a.ts":
        imports +
        `const o = { description: "x" };
const description = "x";
@Responsibility({ ...o })
export class A {}
@Responsibility({ description })
export class B {}
@Responsibility(o)
export class C {}
@Responsibility({ descripton: "typo" })
export class D {}
@Responsibility
export class E {}`,
    });
    const problems = problemsOf(root);
    expect(problems.map((p) => p.replace(/^src\/a\.ts:/, ""))).toEqual([
      expect.stringMatching(/^4:\d+ @Responsibility options cannot use spread/),
      expect.stringMatching(/^6:\d+ @Responsibility options must be written as 'name: value'/),
      expect.stringMatching(/^8:\d+ @Responsibility options must be an object literal/),
      expect.stringMatching(/^10:\d+ Unknown option 'descripton' for @Responsibility/),
      expect.stringMatching(/^12:\d+ @Responsibility must be called with an options object/),
    ]);
  });

  test("a string where an enum is expected is rejected", () => {
    const root = project({ "src/a.ts": imports + `// @ts-ignore\n@Collaborator({ appName: "a", relationship: "External" })\nexport class A {}` });
    expect(problemsOf(root)[0]).toMatch(/^src\/a\.ts:3:\d+ 'relationship' must be a Relationship member/);
  });
});

describe("meta", () => {
  test("must be top level and called once", () => {
    const root = project({
      "src/a.ts": imports + `meta({ appName: "one" });\nmeta({ appName: "two" });\nfunction f() { meta({ appName: "nested" }); }`,
    });
    const problems = problemsOf(root);
    expect(problems.some((p) => /^src\/a\.ts:4:\d+ meta\(\) must be a top-level statement/.test(p))).toBe(true);
    expect(problems.some((p) => /^src\/a\.ts:3:\d+ meta\(\) can only be called once per project/.test(p))).toBe(true);
  });
});

describe("input", () => {
  test("missing path and directory without tsconfig.json", () => {
    const root = project({});
    expect(() => analyse("nope", root)).toThrow(/Not found/);
    const empty = mkdtempSync(join(tmpdir(), "haya-empty-"));
    roots.push(empty);
    expect(() => analyse(empty, empty)).toThrow(/No tsconfig\.json/);
  });
});

// Annotations split over several packages: `api` holds meta() and a @Responsibility, `adapters` a @Collaborator.
describe("multiple packages", () => {
  const compilerOptions = { target: "ES2022", module: "ESNext", moduleResolution: "bundler", allowImportingTsExtensions: true, strict: true };
  const adapterSource =
    imports + `@Collaborator({ appName: "Visa", relationship: Relationship.External })\nexport class VisaAdapter {}\n`;
  const apiSource = (from: string) =>
    imports +
    `import { VisaAdapter } from "${from}";
meta({ appName: "PaymentsApi" });
@Responsibility({ description: "Orchestrates payments" })
export class PayUsecase { adapter = new VisaAdapter(); }\n`;

  /** A monorepo with `adapters` and `api` packages. `link` decides how `api` reaches `adapters`. */
  const monorepo = (link: "relative" | "workspace" | "references" | "root"): string => {
    const root = mkdtempSync(join(tmpdir(), "haya-monorepo-"));
    roots.push(root);
    mkdirSync(join(root, "node_modules/@haya"), { recursive: true });
    symlinkSync(annotations, join(root, "node_modules/@haya/annotations"));
    const write = (name: string, text: string) => {
      mkdirSync(dirname(join(root, name)), { recursive: true });
      writeFileSync(join(root, name), text);
    };
    write("adapters/package.json", '{"name":"@acme/adapters","type":"module","main":"./src/index.ts","types":"./src/index.ts"}');
    write("adapters/src/index.ts", adapterSource);
    write("api/package.json", '{"name":"@acme/api","type":"module"}');
    const from = link === "relative" ? "../../adapters/src/index.ts" : "@acme/adapters";
    write("api/src/main.ts", apiSource(from));
    if (link === "workspace" || link === "references") {
      mkdirSync(join(root, "node_modules/@acme"), { recursive: true });
      symlinkSync(join(root, "adapters"), join(root, "node_modules/@acme/adapters"));
    }
    if (link === "references") {
      write("adapters/tsconfig.json", JSON.stringify({ compilerOptions: { ...compilerOptions, composite: true, emitDeclarationOnly: true, declaration: true, outDir: "dist" }, include: ["src"] }));
      write("api/tsconfig.json", JSON.stringify({ compilerOptions: { ...compilerOptions, noEmit: true }, include: ["src"], references: [{ path: "../adapters" }] }));
    } else {
      write("api/tsconfig.json", JSON.stringify({ compilerOptions: { ...compilerOptions, noEmit: true }, include: ["src"] }));
    }
    if (link === "root") {
      write("tsconfig.json", JSON.stringify({ compilerOptions: { ...compilerOptions, noEmit: true }, include: ["api/src", "adapters/src"] }));
    }
    return root;
  };
  const names = (d: DescriptorInfo[]) => d.flatMap((x) => (x.kind === "meta" ? [] : [x.componentName])).sort();

  test("collaborator in another package is found via a relative import", () => {
    const root = monorepo("relative");
    const found = analyse(join(root, "api"), root);
    expect(names(found)).toEqual(["PayUsecase", "VisaAdapter"]);
    expect(found.filter((x) => x.kind === "meta")).toHaveLength(1);
  });

  test("collaborator in another package is found when one tsconfig covers both", () => {
    const root = monorepo("root");
    expect(names(analyse(root, root))).toEqual(["PayUsecase", "VisaAdapter"]);
  });

  test("collaborator in a workspace package imported by name is found", () => {
    const root = monorepo("workspace");
    expect(names(analyse(join(root, "api"), root))).toEqual(["PayUsecase", "VisaAdapter"]);
  });

  test("collaborator in a project-referenced package is found", () => {
    const root = monorepo("references");
    expect(names(analyse(join(root, "api"), root))).toEqual(["PayUsecase", "VisaAdapter"]);
  });
});
