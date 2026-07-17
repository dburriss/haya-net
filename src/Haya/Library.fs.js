import { Attribute } from "../Haya.Core/fable_modules/fable-library-js.4.24.0/Types.js";
import { class_type } from "../Haya.Core/fable_modules/fable-library-js.4.24.0/Reflection.js";

export class ResponsibilityAttribute extends Attribute {
    constructor() {
        super();
        this["Description@"] = "";
    }
}

export function ResponsibilityAttribute_$reflection() {
    return class_type("Haya.ResponsibilityAttribute", undefined, ResponsibilityAttribute, class_type("System.Attribute"));
}

export function ResponsibilityAttribute_$ctor() {
    return new ResponsibilityAttribute();
}

/**
 * A description of the responsibility, usecase, or feature
 */
export function ResponsibilityAttribute__get_Description(__) {
    return __["Description@"];
}

/**
 * A description of the responsibility, usecase, or feature
 */
export function ResponsibilityAttribute__set_Description_Z721C83C5(__, v) {
    __["Description@"] = v;
}

export class CollaboratorAttribute extends Attribute {
    constructor() {
        super();
        this["Direction@"] = 2;
        this["Technology@"] = "";
        this["Protocol@"] = "HTTPS";
        this["DataDescription@"] = "";
        this["Description@"] = "";
        this["AppName@"] = "";
        this["System@"] = "";
        this["Relationship@"] = 3;
        this["Repository@"] = "";
        this["Owner@"] = "";
    }
}

export function CollaboratorAttribute_$reflection() {
    return class_type("Haya.CollaboratorAttribute", undefined, CollaboratorAttribute, class_type("System.Attribute"));
}

export function CollaboratorAttribute_$ctor() {
    return new CollaboratorAttribute();
}

/**
 * The direction of the data flow or dependency
 */
export function CollaboratorAttribute__get_Direction(__) {
    return __["Direction@"];
}

/**
 * The direction of the data flow or dependency
 */
export function CollaboratorAttribute__set_Direction_Z63A1D161(__, v) {
    __["Direction@"] = (v | 0);
}

/**
 * Then technology used by the collaborator
 */
export function CollaboratorAttribute__get_Technology(__) {
    return __["Technology@"];
}

/**
 * Then technology used by the collaborator
 */
export function CollaboratorAttribute__set_Technology_Z721C83C5(__, v) {
    __["Technology@"] = v;
}

/**
 * The technology or protocol used to communicate with the collaborator
 */
export function CollaboratorAttribute__get_Protocol(__) {
    return __["Protocol@"];
}

/**
 * The technology or protocol used to communicate with the collaborator
 */
export function CollaboratorAttribute__set_Protocol_Z721C83C5(__, v) {
    __["Protocol@"] = v;
}

/**
 * A description of the data or dependency flowing between the component and the collaborator
 */
export function CollaboratorAttribute__get_DataDescription(__) {
    return __["DataDescription@"];
}

/**
 * A description of the data or dependency flowing between the component and the collaborator
 */
export function CollaboratorAttribute__set_DataDescription_Z721C83C5(__, v) {
    __["DataDescription@"] = v;
}

/**
 * A description of the collaborator, system, or application
 */
export function CollaboratorAttribute__get_Description(__) {
    return __["Description@"];
}

/**
 * A description of the collaborator, system, or application
 */
export function CollaboratorAttribute__set_Description_Z721C83C5(__, v) {
    __["Description@"] = v;
}

/**
 * The name of the application being interacted with
 */
export function CollaboratorAttribute__get_AppName(__) {
    return __["AppName@"];
}

/**
 * The name of the application being interacted with
 */
export function CollaboratorAttribute__set_AppName_Z721C83C5(__, v) {
    __["AppName@"] = v;
}

/**
 * The name of the system being interacted with
 */
export function CollaboratorAttribute__get_System(__) {
    return __["System@"];
}

/**
 * The name of the system being interacted with
 */
export function CollaboratorAttribute__set_System_Z721C83C5(__, v) {
    __["System@"] = v;
}

/**
 * The type of relationship between the team and the application or system
 */
export function CollaboratorAttribute__get_Relationship(__) {
    return __["Relationship@"];
}

/**
 * The type of relationship between the team and the application or system
 */
export function CollaboratorAttribute__set_Relationship_7251B3BE(__, v) {
    __["Relationship@"] = (v | 0);
}

/**
 * The slug of the VCS repository for the collaborator
 */
export function CollaboratorAttribute__get_Repository(__) {
    return __["Repository@"];
}

/**
 * The slug of the VCS repository for the collaborator
 */
export function CollaboratorAttribute__set_Repository_Z721C83C5(__, v) {
    __["Repository@"] = v;
}

/**
 * The owner of the system or application
 */
export function CollaboratorAttribute__get_Owner(__) {
    return __["Owner@"];
}

/**
 * The owner of the system or application
 */
export function CollaboratorAttribute__set_Owner_Z721C83C5(__, v) {
    __["Owner@"] = v;
}

export class MetaAttribute extends Attribute {
    constructor() {
        super();
        this["AppName@"] = "";
        this["Owner@"] = "";
        this["Description@"] = "";
        this["System@"] = "";
        this["Repository@"] = "";
    }
}

export function MetaAttribute_$reflection() {
    return class_type("Haya.MetaAttribute", undefined, MetaAttribute, class_type("System.Attribute"));
}

export function MetaAttribute_$ctor() {
    return new MetaAttribute();
}

/**
 * The name of this application
 */
export function MetaAttribute__get_AppName(__) {
    return __["AppName@"];
}

/**
 * The name of this application
 */
export function MetaAttribute__set_AppName_Z721C83C5(__, v) {
    __["AppName@"] = v;
}

/**
 * The team responsible for this application
 */
export function MetaAttribute__get_Owner(__) {
    return __["Owner@"];
}

/**
 * The team responsible for this application
 */
export function MetaAttribute__set_Owner_Z721C83C5(__, v) {
    __["Owner@"] = v;
}

/**
 * A description of this application
 */
export function MetaAttribute__get_Description(__) {
    return __["Description@"];
}

/**
 * A description of this application
 */
export function MetaAttribute__set_Description_Z721C83C5(__, v) {
    __["Description@"] = v;
}

/**
 * The team responsible for this application
 */
export function MetaAttribute__get_System(__) {
    return __["System@"];
}

/**
 * The team responsible for this application
 */
export function MetaAttribute__set_System_Z721C83C5(__, v) {
    __["System@"] = v;
}

/**
 * The slug of the VCS repository for this application
 */
export function MetaAttribute__get_Repository(__) {
    return __["Repository@"];
}

/**
 * The slug of the VCS repository for this application
 */
export function MetaAttribute__set_Repository_Z721C83C5(__, v) {
    __["Repository@"] = v;
}

