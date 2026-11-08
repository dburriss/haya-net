// Static analyser for TypeScript codebases: the counterpart of `Haya.Roslyn` (see ADR005).
//
// Reads `@Responsibility`, `@Collaborator` and `meta()` from the source with the TypeScript compiler API.
// User code is never executed. Annotations are resolved by symbol (following import aliases) to a declaration
// inside the `@haya/annotations` package, never by text matching.
import ts from "typescript";
import * as fs from "node:fs";
import * as path from "node:path";

/** Name of the package that declares the annotations (`src/Haya.TypeScript`). */
export const ANNOTATIONS_PACKAGE = "@haya/annotations";

export type Direction = "Upstream" | "Downstream";
export type Relationship = "Owned" | "Shared" | "Internal" | "External" | "InternalUser" | "ExternalUser";

const DIRECTIONS: readonly Direction[] = ["Upstream", "Downstream"];
const RELATIONSHIPS: readonly Relationship[] = ["Owned", "Shared", "Internal", "External", "InternalUser", "ExternalUser"];

export interface ResponsibilityInfo {
  kind: "responsibility";
  description: string;
  componentName: string;
  componentSource: string;
}

export interface CollaboratorInfo {
  kind: "collaborator";
  direction: Direction;
  tech: string;
  protocol: string;
  dataDescription: string;
  description: string;
  appName: string;
  system: string;
  relationship: Relationship;
  componentName: string;
  componentSource: string;
  repository: string;
  owner: string;
}

export interface MetaInfo {
  kind: "meta";
  appName: string;
  description: string;
  system: string;
  repository: string;
  owner: string;
}

export type DescriptorInfo = ResponsibilityInfo | CollaboratorInfo | MetaInfo;

export interface Problem {
  /** Path relative to the current directory, or undefined when not tied to a source file. */
  file?: string;
  line?: number;
  column?: number;
  message: string;
}

export const formatProblem = (p: Problem): string =>
  p.file === undefined ? p.message : `${p.file}:${p.line ?? 1}:${p.column ?? 1} - ${p.message}`;

/** Thrown when the input cannot be analysed. Carries every problem found, each with file and line where known. */
export class AnalysisError extends Error {
  constructor(readonly problems: Problem[]) {
    super(problems.map(formatProblem).join("\n"));
    this.name = "AnalysisError";
  }
}

type OptionSpec = Record<string, { type: "string" | "Direction" | "Relationship" }>;

const RESPONSIBILITY_OPTIONS: OptionSpec = { description: { type: "string" } };
const COLLABORATOR_OPTIONS: OptionSpec = {
  direction: { type: "Direction" },
  technology: { type: "string" },
  protocol: { type: "string" },
  dataDescription: { type: "string" },
  description: { type: "string" },
  appName: { type: "string" },
  system: { type: "string" },
  relationship: { type: "Relationship" },
  repository: { type: "string" },
  owner: { type: "string" },
};
const META_OPTIONS: OptionSpec = {
  appName: { type: "string" },
  owner: { type: "string" },
  description: { type: "string" },
  system: { type: "string" },
  repository: { type: "string" },
};

type AnnotationName = "Responsibility" | "Collaborator" | "meta";
const SPECS: Record<AnnotationName, OptionSpec> = {
  Responsibility: RESPONSIBILITY_OPTIONS,
  Collaborator: COLLABORATOR_OPTIONS,
  meta: META_OPTIONS,
};

/** Make a path relative to `cwd` like .NET's `Uri.MakeRelativeUri` (forward slashes, URL-escaped). */
export const makeRelativePath = (cwd: string, target: string): string => {
  const rel = path.relative(cwd, target).split(path.sep).join("/");
  return encodeURI(rel).replace(/#/g, "%23").replace(/\?/g, "%3F");
};

/** Resolve the tsconfig.json for `inputPath`, which is either a tsconfig file or a directory containing one. */
export const findConfig = (inputPath: string): string => {
  if (!fs.existsSync(inputPath)) throw new AnalysisError([{ message: `Not found: ${inputPath}` }]);
  if (fs.statSync(inputPath).isDirectory()) {
    const candidate = path.join(inputPath, "tsconfig.json");
    if (!fs.existsSync(candidate)) throw new AnalysisError([{ message: `No tsconfig.json in ${inputPath}` }]);
    return candidate;
  }
  return inputPath;
};

const packageNameCache = new Map<string, string | undefined>();

/** The name of the nearest package.json at or above `dir`. */
const packageNameOf = (dir: string): string | undefined => {
  if (packageNameCache.has(dir)) return packageNameCache.get(dir);
  let name: string | undefined;
  const candidate = path.join(dir, "package.json");
  if (fs.existsSync(candidate)) {
    try {
      name = JSON.parse(fs.readFileSync(candidate, "utf8")).name;
    } catch {
      name = undefined;
    }
  }
  if (name === undefined && path.dirname(dir) !== dir) name = packageNameOf(path.dirname(dir));
  packageNameCache.set(dir, name);
  return name;
};

const isHayaDeclaration = (decl: ts.Declaration): boolean =>
  packageNameOf(path.dirname(decl.getSourceFile().fileName)) === ANNOTATIONS_PACKAGE;

/** Analyse the TypeScript project at `inputPath` and return descriptors in the same order as `Haya.Roslyn`. */
export const analyse = (inputPath: string, cwd: string): DescriptorInfo[] => {
  const configPath = path.resolve(cwd, findConfig(path.resolve(cwd, inputPath)));
  const read = ts.readConfigFile(configPath, ts.sys.readFile);
  const configProblems = (diags: readonly ts.Diagnostic[]): Problem[] =>
    diags.map((d) => ({ file: path.basename(configPath), message: ts.flattenDiagnosticMessageText(d.messageText, "\n") }));
  if (read.error) throw new AnalysisError(configProblems([read.error]));
  const parsed = ts.parseJsonConfigFileContent(read.config, ts.sys, path.dirname(configPath), undefined, configPath);
  const fatal = parsed.errors.filter((d) => d.category === ts.DiagnosticCategory.Error && d.code !== 18003);
  if (fatal.length > 0) throw new AnalysisError(configProblems(fatal));

  const program = ts.createProgram({ rootNames: parsed.fileNames, options: parsed.options });
  const checker = program.getTypeChecker();
  const problems: Problem[] = [];

  const problemAt = (node: ts.Node, message: string): void => {
    const sf = node.getSourceFile();
    const { line, character } = sf.getLineAndCharacterOfPosition(node.getStart(sf));
    problems.push({ file: path.relative(cwd, sf.fileName).split(path.sep).join("/"), line: line + 1, column: character + 1, message });
  };

  /** The symbol an identifier refers to, following import aliases. */
  const resolveSymbol = (node: ts.Node): ts.Symbol | undefined => {
    let sym = checker.getSymbolAtLocation(node);
    if (sym && sym.flags & ts.SymbolFlags.Alias) sym = checker.getAliasedSymbol(sym);
    return sym;
  };

  /** Which Haya annotation a call's callee refers to, if any. */
  const annotationOf = (callee: ts.Expression): AnnotationName | undefined => {
    const nameNode = ts.isIdentifier(callee) ? callee : ts.isPropertyAccessExpression(callee) ? callee.name : undefined;
    if (!nameNode) return undefined;
    const sym = resolveSymbol(nameNode);
    const decls = sym?.declarations ?? [];
    if (!sym || !decls.some(isHayaDeclaration)) return undefined;
    return sym.name === "Responsibility" || sym.name === "Collaborator" || sym.name === "meta" ? sym.name : undefined;
  };

  const readEnum = (expr: ts.Expression, enumName: string, allowed: readonly string[], option: string): string | undefined => {
    const sym = ts.isPropertyAccessExpression(expr) ? resolveSymbol(expr.name) : undefined;
    const decl = sym?.valueDeclaration;
    if (
      sym && decl && ts.isEnumMember(decl) && isHayaDeclaration(decl) &&
      ts.isEnumDeclaration(decl.parent) && decl.parent.name.text === enumName && allowed.includes(sym.name)
    ) {
      return sym.name;
    }
    problemAt(expr, `'${option}' must be a ${enumName} member such as ${enumName}.${allowed[0]}; computed values are not supported`);
    return undefined;
  };

  const readString = (expr: ts.Expression, option: string): string | undefined => {
    if (ts.isStringLiteral(expr) || ts.isNoSubstitutionTemplateLiteral(expr)) return expr.text;
    problemAt(expr, `'${option}' must be a string literal or a template literal without substitutions; non-static values are not supported`);
    return undefined;
  };

  /** Read the options object of an annotation call statically. Unset options are absent from the result. */
  const readOptions = (call: ts.CallExpression, name: AnnotationName): Record<string, string> => {
    const spec = SPECS[name];
    const label = name === "meta" ? "meta()" : `@${name}`;
    const result: Record<string, string> = {};
    const arg = call.arguments[0];
    if (!arg) {
      problemAt(call, `${label} requires an options object`);
      return result;
    }
    if (!ts.isObjectLiteralExpression(arg)) {
      problemAt(arg, `${label} options must be an object literal; non-static values are not supported`);
      return result;
    }
    for (const prop of arg.properties) {
      if (ts.isSpreadAssignment(prop)) {
        problemAt(prop, `${label} options cannot use spread; non-static values are not supported`);
        continue;
      }
      if (!ts.isPropertyAssignment(prop)) {
        problemAt(prop, `${label} options must be written as 'name: value' with a static value`);
        continue;
      }
      const key = ts.isIdentifier(prop.name) || ts.isStringLiteral(prop.name) ? prop.name.text : undefined;
      if (key === undefined) {
        problemAt(prop.name, `${label} option names cannot be computed`);
        continue;
      }
      const option = spec[key];
      if (!option) {
        problemAt(prop.name, `Unknown option '${key}' for ${label} (expected: ${Object.keys(spec).join(", ")})`);
        continue;
      }
      const value =
        option.type === "string" ? readString(prop.initializer, key)
        : option.type === "Direction" ? readEnum(prop.initializer, "Direction", DIRECTIONS, key)
        : readEnum(prop.initializer, "Relationship", RELATIONSHIPS, key);
      if (value !== undefined) result[key] = value;
    }
    return result;
  };

  const responsibilities: ResponsibilityInfo[] = [];
  const collaborators: CollaboratorInfo[] = [];
  const metas: { node: ts.Node; info: MetaInfo }[] = [];

  const visit = (node: ts.Node, sf: ts.SourceFile): void => {
    if (ts.isClassDeclaration(node)) {
      const componentName = node.name?.text ?? "default";
      const componentSource = makeRelativePath(cwd, sf.fileName);
      for (const decorator of ts.getDecorators(node) ?? []) {
        const call = ts.isCallExpression(decorator.expression) ? decorator.expression : undefined;
        const name = annotationOf(call ? call.expression : decorator.expression);
        if (name === undefined) continue;
        if (name === "meta") {
          problemAt(decorator, "meta() is a function call, not a decorator");
          continue;
        }
        if (!call) {
          problemAt(decorator, `@${name} must be called with an options object`);
          continue;
        }
        const o = readOptions(call, name);
        if (name === "Responsibility") {
          responsibilities.push({ kind: "responsibility", description: o.description ?? "", componentName, componentSource });
        } else {
          collaborators.push({
            kind: "collaborator",
            direction: (o.direction as Direction | undefined) ?? "Downstream",
            tech: o.technology ?? "",
            protocol: o.protocol ?? "",
            dataDescription: o.dataDescription ?? "",
            description: o.description ?? "",
            appName: o.appName ?? "",
            system: o.system ?? "",
            relationship: (o.relationship as Relationship | undefined) ?? "Internal",
            componentName,
            componentSource,
            repository: o.repository ?? "",
            owner: o.owner ?? "",
          });
        }
      }
    } else if (ts.isCallExpression(node) && annotationOf(node.expression) === "meta") {
      if (!(ts.isExpressionStatement(node.parent) && ts.isSourceFile(node.parent.parent))) {
        problemAt(node, "meta() must be a top-level statement");
      } else {
        const o = readOptions(node, "meta");
        metas.push({
          node,
          info: {
            kind: "meta",
            appName: o.appName ?? "",
            description: o.description ?? "",
            system: o.system ?? "",
            repository: o.repository ?? "",
            owner: o.owner ?? "",
          },
        });
      }
    }
    ts.forEachChild(node, (child) => visit(child, sf));
  };

  const sources = program
    .getSourceFiles()
    .filter(
      (sf) =>
        !sf.isDeclarationFile &&
        !program.isSourceFileFromExternalLibrary(sf) &&
        !program.isSourceFileDefaultLibrary(sf) &&
        !sf.fileName.split("/").includes("node_modules"),
    )
    .sort((a, b) => (a.fileName < b.fileName ? -1 : a.fileName > b.fileName ? 1 : 0));
  for (const sf of sources) visit(sf, sf);

  if (metas.length > 1) {
    for (const m of metas.slice(1)) problemAt(m.node, "meta() can only be called once per project");
  }
  if (problems.length > 0) throw new AnalysisError(problems);

  return [...responsibilities, ...collaborators, ...metas.map((m) => m.info)];
};
