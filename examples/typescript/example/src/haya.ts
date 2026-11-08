// Haya annotations for TypeScript.
//
// Placeholder that mirrors the C# `Haya` attributes (src/Haya/Library.fs). The functions do nothing at runtime:
// Haya reads the annotations statically from the source. This is expected to become an npm package.

/** The direction of the data flow or dependency. */
export enum Direction {
  Upstream = "Upstream",
  Downstream = "Downstream",
}

/** The type of relationship between the team and the application or system. */
export enum Relationship {
  Owned = "Owned",
  Shared = "Shared",
  Internal = "Internal",
  External = "External",
  InternalUser = "InternalUser",
  ExternalUser = "ExternalUser",
}

/** The responsibility, usecase, or feature of the component. Repeatable. */
export interface ResponsibilityOptions {
  /** A description of the responsibility, usecase, or feature. */
  description: string;
}

/** The collaborator, system, or application that the component interacts with. Repeatable. */
export interface CollaboratorOptions {
  /** The direction of the data flow or dependency (default: Downstream). */
  direction?: Direction;
  /** The technology used by the collaborator. */
  technology?: string;
  /** The technology or protocol used to communicate with the collaborator (default: HTTPS). */
  protocol?: string;
  /** A description of the data or dependency flowing between the component and the collaborator. */
  dataDescription?: string;
  /** A description of the collaborator, system, or application. */
  description?: string;
  /** The name of the application being interacted with. */
  appName: string;
  /** The name of the system being interacted with. */
  system?: string;
  /** The type of relationship between the team and the application or system (default: Internal). */
  relationship?: Relationship;
  /** The slug of the VCS repository for the collaborator. */
  repository?: string;
  /** The owner of the system or application. */
  owner?: string;
}

/** Information about this application or system. Call once per project. */
export interface MetaOptions {
  /** The name of this application. */
  appName: string;
  /** The team responsible for this application. */
  owner?: string;
  /** A description of this application. */
  description?: string;
  /** The system this application belongs to. */
  system?: string;
  /** The slug of the VCS repository for this application. */
  repository?: string;
}

type ClassDecorator = (target: abstract new (...args: any[]) => unknown, context: ClassDecoratorContext) => void;

/** Class decorator: marks the class with a responsibility. */
export const Responsibility = (_options: ResponsibilityOptions): ClassDecorator => () => {};

/** Class decorator: marks the class as interacting with a collaborator. */
export const Collaborator = (_options: CollaboratorOptions): ClassDecorator => () => {};

/** Top level call: describes this application. The TypeScript equivalent of the C# assembly-level `Meta`. */
export const meta = (_options: MetaOptions): void => {};
