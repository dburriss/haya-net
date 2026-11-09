import { defineCollaborator, defineMeta } from "@haya/annotations";

// Organisation-wide annotations: each wraps a Haya annotation and bakes in options. Options written where the
// annotation is used override these defaults.

export const acmeMeta = defineMeta({ owner: "Acme Team", system: "Acme", technology: ".NET 10 / ASP.NET Core" });

export const CallsPayments = defineCollaborator({ appName: "Payments", system: "Acme", protocol: "gRPC", technology: ".NET 10" });
