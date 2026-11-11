// Bridge from the analyser's plain objects to the Fable-compiled `Haya.Core` model.
// Enums are ints in Fable output (see Haya.Core/Model.fs) and lists are fable-library lists.
import {
  AppModule_ofDescriptors,
  AppModule_withComponents,
  AppModule_withLanguage,
  ComponentDetail,
  Member,
  Parameter,
  type App,
  CollaboratorDescriptor,
  Descriptor_Collaborator,
  Descriptor_Meta,
  Descriptor_Responsibility,
  MetaDescriptor,
  ResponsibilityDescriptor,
  type Descriptor_$union,
} from "../fable_output/Haya.Core/Model.ts";
import { ofArray, type FSharpList } from "../fable_output/fable_modules/fable-library-ts.5.27.0/List.ts";
import type { AppKind, ComponentInfo, Direction, DescriptorInfo, MemberKind, ParameterInfo, Relationship } from "./analyser.ts";

const DIRECTION: Record<Direction, number> = { Upstream: 1, Downstream: 2 };
const RELATIONSHIP: Record<Relationship, number> = {
  Owned: 1,
  Shared: 2,
  Internal: 3,
  External: 4,
};

/** `Language.JavaScript` of `Haya.Core.Analysis` (an F# enum, so a number under Fable). */
const LANGUAGE_JAVASCRIPT = 2;

const APP_KIND: Record<AppKind | "Unspecified", number> = {
  Unspecified: 0,
  Service: 1,
  WebApp: 2,
  Api: 3,
  Worker: 4,
  Database: 5,
  Queue: 6,
  Person: 7,
};

export const toFableDescriptor = (d: DescriptorInfo): Descriptor_$union => {
  switch (d.kind) {
    case "responsibility":
      return Descriptor_Responsibility(new ResponsibilityDescriptor(d.description, d.componentName, d.componentSource));
    case "collaborator":
      return Descriptor_Collaborator(
        new CollaboratorDescriptor(
          DIRECTION[d.direction],
          d.tech,
          d.protocol,
          d.dataDescription,
          d.description,
          d.appName,
          d.system,
          RELATIONSHIP[d.relationship],
          d.componentName,
          d.componentSource,
          d.repository,
          d.owner,
          APP_KIND[d.appKind],
        ),
      );
    case "meta":
      return Descriptor_Meta(new MetaDescriptor(d.appName, d.description, d.system, d.repository, d.owner, d.tech, APP_KIND[d.appKind], d.lifecycle, d.tier));
  }
};

/** `MemberKind` of `Haya.Core.Analysis` (an F# enum, so a number under Fable). */
const MEMBER_KIND: Record<MemberKind, number> = { Method: 1, Property: 2, Field: 3, Event: 4 };

const toFableParameters = (ps: ParameterInfo[]) => ofArray(ps.map((p) => new Parameter(p.name, p.type)));

export const toFableComponent = (c: ComponentInfo): ComponentDetail =>
  new ComponentDetail(
    c.name,
    c.source,
    ofArray(c.constructors.map(toFableParameters)),
    ofArray(c.members.map((m) => new Member(m.name, MEMBER_KIND[m.kind], m.type, toFableParameters(m.parameters)))),
  );

export const toFableDescriptors = (ds: DescriptorInfo[]): FSharpList<Descriptor_$union> => ofArray(ds.map(toFableDescriptor));

/**
 * One analysed project becomes one app (the counterpart of `App.ofDescriptors` on the .NET side). `components` holds
 * the classes' constructors and members, only at `--detail members`; Haya.Core puts them in order.
 */
export const toFableApp = (ds: DescriptorInfo[], components: ComponentInfo[] = []): App =>
  AppModule_withComponents(
    ofArray(components.map(toFableComponent)),
    AppModule_withLanguage(LANGUAGE_JAVASCRIPT, AppModule_ofDescriptors(toFableDescriptors(ds))),
  );
