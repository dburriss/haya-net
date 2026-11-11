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
export type Relationship = "Owned" | "Shared" | "Internal" | "External";
export type AppKind = "Service" | "WebApp" | "Api" | "Worker" | "Database" | "Queue" | "Person";

const DIRECTIONS: readonly Direction[] = ["Upstream", "Downstream"];
const RELATIONSHIPS: readonly Relationship[] = ["Owned", "Shared", "Internal", "External"];
const APP_KINDS: readonly AppKind[] = ["Service", "WebApp", "Api", "Worker", "Database", "Queue", "Person"];

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
  /** What the collaborator is; "Person" for a user. Unset is reported as "Unspecified". (Not `kind`: the discriminator.) */
  appKind: AppKind | "Unspecified";
}

export interface MetaInfo {
  kind: "meta";
  appName: string;
  description: string;
  system: string;
  repository: string;
  owner: string;
  tech: string;
  /** Unset is reported as "Unspecified". (Not `kind`: that is the discriminator of the union.) */
  appKind: AppKind | "Unspecified";
  lifecycle: string;
  tier: string;
}

export type DescriptorInfo = ResponsibilityInfo | CollaboratorInfo | MetaInfo;

export interface ParameterInfo {
  name: string;
  type: string;
}

export type MemberKind = "Method" | "Property" | "Field" | "Event";

export interface MemberInfo {
  name: string;
  kind: MemberKind;
  /** The return/property type as the TypeScript checker prints it, e.g. `Promise<boolean>`. */
  type: string;
  /** Methods only: the parameters in order. */
  parameters: ParameterInfo[];
}

/** The public constructors and members of an annotated class (`--detail members`, ADR017). Order is set by Haya.Core. */
export interface ComponentInfo {
  name: string;
  source: string;
  constructors: ParameterInfo[][];
  members: MemberInfo[];
}

/** What a project analysed with members holds. */
export interface Analysis {
  descriptors: DescriptorInfo[];
  components: ComponentInfo[];
}

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

type OptionSpec = Record<string, { type: "string" | "Direction" | "Relationship" | "AppKind" }>;

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
  kind: { type: "AppKind" },
};
const META_OPTIONS: OptionSpec = {
  appName: { type: "string" },
  owner: { type: "string" },
  description: { type: "string" },
  system: { type: "string" },
  repository: { type: "string" },
  technology: { type: "string" },
  kind: { type: "AppKind" },
  lifecycle: { type: "string" },
  tier: { type: "string" },
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
export const analyse = (inputPath: string, cwd: string): DescriptorInfo[] => analyseProject(inputPath, cwd, false).descriptors;

/** Analyse the project and also collect each annotated class's public constructors and members (`--detail members`). */
export const analyseWithMembers = (inputPath: string, cwd: string): Analysis => analyseProject(inputPath, cwd, true);

/** Names the C# side leaves out as overrides of `System.Object`; their TypeScript counterparts. */
const OBJECT_MEMBERS = new Set(["toString", "valueOf"]);

const analyseProject = (inputPath: string, cwd: string, withMembers: boolean): Analysis => {
  const configPath = path.resolve(cwd, findConfig(path.resolve(cwd, inputPath)));
  const read = ts.readConfigFile(configPath, ts.sys.readFile);
  const configProblems = (diags: readonly ts.Diagnostic[]): Problem[] =>
    diags.map((d) => ({ file: path.basename(configPath), message: ts.flattenDiagnosticMessageText(d.messageText, "\n") }));
  if (read.error) throw new AnalysisError(configProblems([read.error]));
  const parsed = ts.parseJsonConfigFileContent(read.config, ts.sys, path.dirname(configPath), undefined, configPath);
  const fatal = parsed.errors.filter((d) => d.category === ts.DiagnosticCategory.Error && d.code !== 18003);
  if (fatal.length > 0) throw new AnalysisError(configProblems(fatal));

  // Project references normally resolve to the referenced project's .d.ts output, which hides its annotations.
  // Analyse the sources of every referenced project (transitively) as part of this program instead.
  const rootNames = new Set(parsed.fileNames);
  const seenConfigs = new Set([configPath]);
  const addReferences = (refs: readonly ts.ProjectReference[] | undefined): void => {
    for (const ref of refs ?? []) {
      const refConfig = ts.resolveProjectReferencePath(ref);
      if (seenConfigs.has(refConfig)) continue;
      seenConfigs.add(refConfig);
      const refRead = ts.readConfigFile(refConfig, ts.sys.readFile);
      if (refRead.error) throw new AnalysisError(configProblems([refRead.error]));
      const refParsed = ts.parseJsonConfigFileContent(refRead.config, ts.sys, path.dirname(refConfig), undefined, refConfig);
      refParsed.fileNames.forEach((f) => rootNames.add(f));
      addReferences(refParsed.projectReferences);
    }
  };
  addReferences(parsed.projectReferences);

  const program = ts.createProgram({ rootNames: [...rootNames], options: parsed.options });
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

  const calleeName = (callee: ts.Expression): ts.Identifier | undefined =>
    ts.isIdentifier(callee) ? callee : ts.isPropertyAccessExpression(callee) && ts.isIdentifier(callee.name) ? callee.name : undefined;

  const DEFINERS: Record<string, AnnotationName> = {
    defineResponsibility: "Responsibility",
    defineCollaborator: "Collaborator",
    defineMeta: "meta",
  };

  /** Defaults of a custom annotation, read once per `define*` call so a problem in them is reported once. */
  const defaultsCache = new Map<ts.CallExpression, Record<string, string>>();

  /**
   * Which Haya annotation a call's callee refers to, if any, with the defaults it carries. The callee is either an
   * annotation from the Haya package, or a custom annotation: a `const` initialised with `defineX({...})`.
   */
  const annotationOf = (callee: ts.Expression): { name: AnnotationName; defaults: Record<string, string>; custom: boolean } | undefined => {
    const nameNode = calleeName(callee);
    if (!nameNode) return undefined;
    const sym = resolveSymbol(nameNode);
    const decls = sym?.declarations ?? [];
    if (!sym) return undefined;
    if (decls.some(isHayaDeclaration)) {
      return sym.name === "Responsibility" || sym.name === "Collaborator" || sym.name === "meta"
        ? { name: sym.name, defaults: {}, custom: false }
        : undefined;
    }
    const decl = decls.find(ts.isVariableDeclaration);
    const init = decl?.initializer;
    if (!decl || !init || !ts.isCallExpression(init) || !(decl.parent.flags & ts.NodeFlags.Const)) return undefined;
    const definerName = calleeName(init.expression);
    const definer = definerName ? resolveSymbol(definerName) : undefined;
    const name = definer && (definer.declarations ?? []).some(isHayaDeclaration) ? DEFINERS[definer.name] : undefined;
    if (name === undefined) return undefined;
    let defaults = defaultsCache.get(init);
    if (!defaults) {
      defaults = readOptions(init, name, `${definer!.name}()`);
      defaultsCache.set(init, defaults);
    }
    return { name, defaults, custom: true };
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
  const readOptions = (call: ts.CallExpression, name: AnnotationName, label: string, allowMissing = false): Record<string, string> => {
    const spec = SPECS[name];
    const result: Record<string, string> = {};
    const arg = call.arguments[0];
    if (!arg) {
      if (!allowMissing) problemAt(call, `${label} requires an options object`);
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
        : option.type === "AppKind" ? readEnum(prop.initializer, "AppKind", APP_KINDS, key)
        : readEnum(prop.initializer, "Relationship", RELATIONSHIPS, key);
      if (value !== undefined) result[key] = value;
    }
    return result;
  };

  const typeText = (t: ts.Type): string => checker.typeToString(t, undefined, ts.TypeFormatFlags.NoTruncation);

  /** Hidden from other code: `private`, `protected` or an ECMAScript `#private` name. */
  const isHidden = (node: ts.Node): boolean => {
    const flags = ts.getCombinedModifierFlags(node as ts.Declaration);
    if (flags & (ts.ModifierFlags.Private | ts.ModifierFlags.Protected)) return true;
    const name = (node as ts.NamedDeclaration).name;
    return name !== undefined && ts.isPrivateIdentifier(name);
  };

  const parametersOf = (params: readonly ts.ParameterDeclaration[]): ParameterInfo[] =>
    params.map((p) => ({
      name: p.name.getText(),
      type: typeText(checker.getTypeAtLocation(p)),
    }));

  /** The declarations that make up the public signatures: the overloads, or the implementation when there are none. */
  const signatures = <T extends ts.FunctionLikeDeclaration>(decls: T[]): T[] => {
    const overloads = decls.filter((d) => d.body === undefined);
    return overloads.length > 0 ? overloads : decls;
  };

  /** The public constructors and the public members declared on the class (not inherited). */
  const componentOf = (node: ts.ClassDeclaration, name: string, source: string): ComponentInfo => {
    const ctors = signatures(node.members.filter(ts.isConstructorDeclaration)).filter((c) => !isHidden(c));
    const members: MemberInfo[] = [];
    const methods = new Map<string, ts.MethodDeclaration[]>();
    const accessors = new Set<string>();
    for (const m of node.members) {
      if (isHidden(m) || m.name === undefined || ts.isComputedPropertyName(m.name)) continue;
      const memberName = m.name.getText();
      if (OBJECT_MEMBERS.has(memberName)) continue;
      if (ts.isMethodDeclaration(m)) {
        methods.set(memberName, [...(methods.get(memberName) ?? []), m]);
      } else if (ts.isPropertyDeclaration(m)) {
        members.push({ name: memberName, kind: "Property", type: typeText(checker.getTypeAtLocation(m)), parameters: [] });
      } else if ((ts.isGetAccessorDeclaration(m) || ts.isSetAccessorDeclaration(m)) && !accessors.has(memberName)) {
        // one property per name, whether it has a getter, a setter or both
        accessors.add(memberName);
        const sym = checker.getSymbolAtLocation(m.name);
        const type = sym ? checker.getTypeOfSymbolAtLocation(sym, m) : checker.getTypeAtLocation(m);
        members.push({ name: memberName, kind: "Property", type: typeText(type), parameters: [] });
      }
    }
    for (const [memberName, decls] of methods) {
      for (const d of signatures(decls)) {
        const sig = checker.getSignatureFromDeclaration(d);
        const type = sig ? typeText(checker.getReturnTypeOfSignature(sig)) : "";
        members.push({ name: memberName, kind: "Method", type, parameters: parametersOf(d.parameters) });
      }
    }
    // public parameter properties (`constructor(public repo: Repo)`) are properties of the class
    for (const c of node.members.filter(ts.isConstructorDeclaration)) {
      for (const p of c.parameters) {
        if (ts.isParameterPropertyDeclaration(p, c) && !isHidden(p)) {
          members.push({ name: p.name.getText(), kind: "Property", type: typeText(checker.getTypeAtLocation(p)), parameters: [] });
        }
      }
    }
    return { name, source, constructors: ctors.map((c) => parametersOf(c.parameters)), members };
  };

  const components: ComponentInfo[] = [];
  const responsibilities: ResponsibilityInfo[] = [];
  const collaborators: CollaboratorInfo[] = [];
  const metas: { node: ts.Node; info: MetaInfo }[] = [];

  const visit = (node: ts.Node, sf: ts.SourceFile): void => {
    if (ts.isClassDeclaration(node)) {
      const componentName = node.name?.text ?? "default";
      const componentSource = makeRelativePath(cwd, sf.fileName);
      let annotated = false;
      for (const decorator of ts.getDecorators(node) ?? []) {
        const call = ts.isCallExpression(decorator.expression) ? decorator.expression : undefined;
        const annotation = annotationOf(call ? call.expression : decorator.expression);
        if (annotation === undefined) continue;
        const { name } = annotation;
        const written = call ? calleeName(call.expression)?.text ?? name : calleeName(decorator.expression)?.text ?? name;
        if (name === "meta") {
          problemAt(decorator, `${annotation.custom ? written : "meta"}() is a function call, not a decorator`);
          continue;
        }
        if (!call) {
          problemAt(decorator, `@${written} must be called with an options object`);
          continue;
        }
        const o = { ...annotation.defaults, ...readOptions(call, name, `@${written}`, annotation.custom) };
        annotated = true;
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
            appKind: (o.kind as AppKind | undefined) ?? "Unspecified",
          });
        }
      }
      if (withMembers && annotated) components.push(componentOf(node, componentName, componentSource));
    } else if (ts.isCallExpression(node) && annotationOf(node.expression)?.name === "meta") {
      const annotation = annotationOf(node.expression)!;
      const written = calleeName(node.expression)?.text ?? "meta";
      if (!(ts.isExpressionStatement(node.parent) && ts.isSourceFile(node.parent.parent))) {
        problemAt(node, `${written}() must be a top-level statement`);
      } else {
        const o = { ...annotation.defaults, ...readOptions(node, "meta", `${written}()`, annotation.custom) };
        metas.push({
          node,
          info: {
            kind: "meta",
            appName: o.appName ?? "",
            description: o.description ?? "",
            system: o.system ?? "",
            repository: o.repository ?? "",
            owner: o.owner ?? "",
            tech: o.technology ?? "",
            appKind: (o.kind as AppKind | undefined) ?? "Unspecified",
            lifecycle: o.lifecycle ?? "",
            tier: o.tier ?? "",
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
        // Skip installed packages by path, not by `isSourceFileFromExternalLibrary`: a workspace package linked into
        // node_modules resolves to its real path outside node_modules and must be analysed.
        !program.isSourceFileDefaultLibrary(sf) &&
        !sf.fileName.split("/").includes("node_modules"),
    )
    .sort((a, b) => (a.fileName < b.fileName ? -1 : a.fileName > b.fileName ? 1 : 0));
  // A file can be in the program twice, as a root and again through a symlinked package path: visit it once.
  const visited = new Set<string>();
  for (const sf of sources) {
    const real = fs.realpathSync(sf.fileName);
    if (visited.has(real)) continue;
    visited.add(real);
    visit(sf, sf);
  }

  if (metas.length > 1) {
    for (const m of metas.slice(1)) problemAt(m.node, "meta() can only be called once per project");
  }
  if (problems.length > 0) throw new AnalysisError(problems);

  return { descriptors: [...responsibilities, ...collaborators, ...metas.map((m) => m.info)], components };
};
