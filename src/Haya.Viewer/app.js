import cytoscape from "cytoscape";
import fcose from "cytoscape-fcose";

cytoscape.use(fcose);

// The graph is embedded as JSON by the generator. Everything derived from it is written with textContent or text
// nodes (see `h`) or escaped for SVG, never as html.
const data = JSON.parse(document.getElementById("haya-data").textContent);
document.getElementById("title").textContent = document.title;

// ---------------------------------------------------------------- model
const nodes = new Map(data.nodes.map((n) => [n.id, n]));
const name = (id) => { const n = nodes.get(id); return n ? n.name || n.id : id; };
const appsOf = new Map(); // system id -> app ids
for (const n of data.nodes) if (n.kind === "app" && n.parent) {
  if (!appsOf.has(n.parent)) appsOf.set(n.parent, []);
  appsOf.get(n.parent).push(n.id);
}
const touching = new Map(); // node id -> model edges
for (const e of data.edges) for (const id of [e.from, e.to]) {
  if (!touching.has(id)) touching.set(id, []);
  touching.get(id).push(e);
}
// The classes of an app (components), drawn inside it when it is opened. They are nodes of the model too, so names, the panel
// and selection work the same; `kind` is "component" and `parent` the app.
const classesOf = new Map(); // app id -> class ids
for (const n of data.nodes) for (const c of n.components || []) {
  nodes.set(c.id, { ...c, kind: "component", parent: n.id });
  if (!classesOf.has(n.id)) classesOf.set(n.id, []);
  classesOf.get(n.id).push(c.id);
}
const classTouching = new Map(); // class id -> model edges whose end is that class
for (const e of data.edges) for (const id of [...(e.fromComponents || []), ...(e.toComponents || [])]) {
  if (!classTouching.has(id)) classTouching.set(id, []);
  if (!classTouching.get(id).includes(e)) classTouching.get(id).push(e);
}
/** True when `ancestor` is the parent of `id`, or its parent's parent. */
const within = (id, ancestor) => {
  for (let p = nodes.get(id).parent; p; p = nodes.get(p).parent) if (p === ancestor) return true;
  return false;
};

// Heat: a count is coloured by a ramp with four stops. Below the first stop it is plain; the first stop is where yellow starts,
// the second is full yellow (fading to orange from there), the third is orange (fading to red from there) and the fourth is the
// deepest red, kept for the most complex. If the data goes past the last stop, the red end stretches to the largest value, so the most
// complex system or pair always has the deepest red. Change the numbers here.
const HEAT_SYSTEMS = [7, 9, 13, 20]; // apps + relationships of a system: the border of an own system
const HEAT_LINKS = [3, 4, 6, 9];    // relationships between two systems: the line between them
const HEAT_COLOURS = [[58, 0.42, 0.6], [48, 0.5, 0.52], [28, 0.5, 0.5], [4, 0.5, 0.44]]; // hue, saturation, lightness: muted
const hsl = (h, s, l) => {
  const k = (n) => (n + h / 30) % 12, a = s * Math.min(l, 1 - l);
  const f = (n) => Math.round(255 * (l - a * Math.max(-1, Math.min(k(n) - 3, 9 - k(n), 1))));
  return "#" + [f(0), f(8), f(4)].map((x) => x.toString(16).padStart(2, "0")).join("");
};
const stretch = (stops, largest) => [stops[0], stops[1], stops[2], Math.max(stops[3], largest)];
/** The colour of `value` on the ramp, or undefined while it is below the first stop. */
const heat = (stops, value) => {
  if (value < stops[0]) return undefined;
  if (value >= stops[3]) return hsl(...HEAT_COLOURS[3]);
  let i = 0;
  while (value >= stops[i + 1]) i++;
  const t = (value - stops[i]) / (stops[i + 1] - stops[i]), [a, b] = [HEAT_COLOURS[i], HEAT_COLOURS[i + 1]];
  return hsl(a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t);
};
const heatBar = () => h("span", { class: "heat-bar", style: "background:linear-gradient(90deg," + HEAT_COLOURS.map((c) => hsl(...c)).join(",") + ")" });

// An own system's border, by its complexity: the apps it holds plus the relationships that touch them (inside the system or out; one
// between two of its own apps counts once). The panel shows the sum.
const systemOf = (id) => { const n = nodes.get(id); return n.kind === "system" ? id : n.parent || id; };
const complexity = new Map(); // own system id -> { apps, relationships, total }
for (const n of data.nodes) if (n.kind === "system" && n.ownSystem) {
  const apps = (appsOf.get(n.id) || []).length;
  const relationships = data.edges.filter((e) => systemOf(e.from) === n.id || systemOf(e.to) === n.id).length;
  complexity.set(n.id, { apps, relationships, total: apps + relationships });
}
const heatOf = new Map(); // own system id -> border colour
const appStops = stretch(HEAT_SYSTEMS, Math.max(0, ...[...complexity.values()].map((c) => c.total)));
for (const [id, c] of complexity) { const colour = heat(appStops, c.total); if (colour) heatOf.set(id, colour); }

// A line between two systems, by the total number of relationships between them in the data (not what is drawn right now, so it
// does not shift as things are opened). Lines to or from an app stay plain.
const pairKey = (a, b) => (a < b ? a + "|" + b : b + "|" + a);
const lineHeat = new Map(); // pairKey of two systems -> colour
const pairTotal = new Map();
for (const e of data.edges) {
  const a = systemOf(e.from), b = systemOf(e.to);
  if (a !== b) pairTotal.set(pairKey(a, b), (pairTotal.get(pairKey(a, b)) || 0) + 1);
}
const linkStops = stretch(HEAT_LINKS, Math.max(0, ...pairTotal.values()));
for (const [k, c] of pairTotal) { const colour = heat(linkStops, c); if (colour) lineHeat.set(k, colour); }

// ---------------------------------------------------------------- state
// What is visible follows from `shown` (the apps drawn): a system with shown apps is a boundary around them, one
// without is a single box. Systems are always drawn; edges between hidden apps roll up to their systems.
let shown = new Set();
let opened = new Set(); // apps drawn with their classes inside (only while the app is shown)
let selected = null; // { kind: "node" | "edge", id }
// every filter value starts enabled; switching one off fades the apps that have it
const valuesOf = (key, of) => [...new Set(data.nodes.filter((n) => n.kind === "app").map(of))].sort();
const FILTERS = [
  { key: "appKind", title: "Kind", of: (n) => n.appKind || "Unspecified" },
  { key: "tier", title: "Tier", of: (n) => n.tier || "none" },
  { key: "lifecycle", title: "Lifecycle", of: (n) => n.lifecycle || "none" },
];
const enabled = {};
const enableAll = () => { for (const f of FILTERS) enabled[f.key] = new Set(valuesOf(f.key, f.of)); };
enableAll();
const resetShown = () => { shown = new Set(data.nodes.filter((n) => n.kind === "app" && !n.parent).map((n) => n.id)); };
const isOpen = (systemId) => (appsOf.get(systemId) || []).some((a) => shown.has(a));
const isOpenApp = (id) => opened.has(id) && shown.has(id);
const rep = (id) => {
  const n = nodes.get(id);
  return n.kind === "system" || shown.has(id) ? id : n.parent || id;
};

// ---------------------------------------------------------------- dom helpers
const h = (tag, attrs, ...kids) => {
  const e = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs || {})) {
    if (v == null || v === false) continue;
    if (k === "class") e.className = v;
    else if (k.startsWith("on")) e.addEventListener(k.slice(2), v);
    else e.setAttribute(k, v === true ? "" : v);
  }
  for (const k of kids.flat()) if (k != null && k !== false) e.append(k);
  return e;
};
const css = (n) => getComputedStyle(document.documentElement).getPropertyValue(n).trim();

// ---------------------------------------------------------------- glyphs
// A node is a label with small glyphs (kind, tier, lifecycle) instead of text badges, so the boxes stay small.
// The filter chips and the legend show the same glyphs next to their names.
const KIND_FILL = { Api: "#2f6fdb", Service: "#6f42c1", WebApp: "#0a8f6a", Worker: "#b7791f", Database: "#c05621", Queue: "#b83280", Person: "#1f7a8c" };
const LIFE_FILL = { production: "#2f855a", experimental: "#b7791f", deprecated: "#c53030" };
const GREY = "#8b949e", AMBER = "#d69e2e";
const xml = (s) => String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&apos;" })[c]);
const tierText = (t) => (/^t/i.test(t) ? t : "T" + t).slice(0, 4);
const kindColor = (k) => KIND_FILL[k] || GREY;
const lifeColor = (l) => LIFE_FILL[(l || "").toLowerCase()] || GREY;

// 12x12 drawings, in one colour
const GLYPH = {
  Api: (c) => `<text x="6" y="9.6" font-size="10" font-weight="bold" font-family="monospace" text-anchor="middle" fill="${c}">{}</text>`,
  Service: (c) => `<path d="M6 .5 11.5 6 6 11.5.5 6z" fill="${c}"/>`,
  WebApp: (c) => `<rect x=".7" y="1.5" width="10.6" height="9" rx="1.5" fill="none" stroke="${c}" stroke-width="1.3"/><rect x=".7" y="1.5" width="10.6" height="3" fill="${c}"/>`,
  Worker: (c) => `<circle cx="6" cy="6" r="3" fill="none" stroke="${c}" stroke-width="1.8"/><circle cx="6" cy="6" r="5.4" fill="none" stroke="${c}" stroke-width="1.2" stroke-dasharray="2 1.4"/>`,
  Database: (c) => `<ellipse cx="6" cy="2.6" rx="5" ry="2" fill="${c}"/><path d="M1 2.6V9.4A5 2 0 0 0 11 9.4V2.6" fill="none" stroke="${c}" stroke-width="1.3"/>`,
  Queue: (c) => [1, 4.8, 8.6].map((y) => `<rect x=".5" y="${y}" width="11" height="2.4" rx="1" fill="${c}"/>`).join(""),
  // a head and shoulders
  Person: (c) => `<circle cx="6" cy="3.5" r="2.5" fill="${c}"/><path d="M1.2 11.3C1.2 8.2 3.3 6.9 6 6.9s4.8 1.3 4.8 4.4z" fill="${c}"/>`,
  Unspecified: (c) => `<rect x="1.5" y="1.5" width="9" height="9" rx="2" fill="none" stroke="${c}" stroke-width="1.3"/>`,
  System: (c) => `<rect x=".7" y=".7" width="10.6" height="10.6" rx="2" fill="none" stroke="${c}" stroke-width="1.2" stroke-dasharray="2.4 1.4"/><rect x="3.5" y="3.5" width="5" height="5" rx="1" fill="${c}"/>`,
  External: (c) => `<path d="M5 2H1.5V10.5H10V7M6 6 11 1M7.5 1H11V4.5" fill="none" stroke="${c}" stroke-width="1.3"/>`,
  Undocumented: (c) => `<text x="6" y="10" font-size="12" font-weight="bold" font-family="sans-serif" text-anchor="middle" fill="${c}">?</text>`,
  Lifecycle: (c) => `<circle cx="6" cy="6" r="4" fill="${c}"/>`,
  // a closed eye: the lid and three lashes
  EyeClosed: (c) => `<path d="M1 4.5C3 8 9 8 11 4.5M2.6 7 1.6 9M6 8v2.3M9.4 7l1 2" fill="none" stroke="${c}" stroke-width="1.2" stroke-linecap="round"/>`,
};
const glyphG = (kind, color, x, y) => `<g transform="translate(${x} ${y})">${GLYPH[kind](color)}</g>`;
const dataUri = (svg) => "data:image/svg+xml;utf8," + encodeURIComponent(svg);
const glyphImg = (kind, color, size = 14) => h("img", { src: dataUri(`<svg xmlns="http://www.w3.org/2000/svg" width="12" height="12" viewBox="0 0 12 12">${GLYPH[kind](color)}</svg>`), width: size, height: size, alt: "" });

const W = 184, HEIGHT = 30;
const imageCache = new Map();
const nodeImage = (n) => {
  const parts = [];
  if (n.kind === "system") {
    const count = (appsOf.get(n.id) || []).length;
    parts.push(glyphG(n.ownSystem ? "System" : "External", GREY, 9, (HEIGHT - 12) / 2));
    if (n.ownSystem && count) {
      const t = String(count), w = Math.max(18, t.length * 7 + 10);
      parts.push(`<rect x="${W - w - 7}" y="${(HEIGHT - 16) / 2}" width="${w}" height="16" rx="8" fill="${GREY}"/><text x="${W - 7 - w / 2}" y="${HEIGHT / 2 + 3.7}" font-size="10.5" font-family="system-ui,sans-serif" font-weight="600" fill="#fff" text-anchor="middle">${t}</text>`);
    }
  } else if (n.kind === "component") {
    // a class: its name is the label, nothing else to show
  } else if (n.kind === "person") {
    // a person is known only from collaborators, so there is no meta to be missing: no "undocumented" mark
    parts.push(glyphG("Person", kindColor("Person"), 9, (HEIGHT - 12) / 2));
  } else {
    parts.push(glyphG(n.appKind || "Unspecified", kindColor(n.appKind), 9, (HEIGHT - 12) / 2));
    if (!n.hasMeta) parts.push(glyphG("Undocumented", AMBER, W - 19, (HEIGHT - 12) / 2));
    else {
      if (n.lifecycle) parts.push(glyphG("Lifecycle", lifeColor(n.lifecycle), W - 19, (HEIGHT - 12) / 2));
      if (n.tier) parts.push(`<text x="${W - (n.lifecycle ? 25 : 9)}" y="${HEIGHT / 2 + 3.7}" font-size="10" font-family="system-ui,sans-serif" font-weight="600" fill="${GREY}" text-anchor="end">${xml(tierText(n.tier))}</text>`);
    }
  }
  const key = parts.join("");
  if (!imageCache.has(key)) imageCache.set(key, dataUri(`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${HEIGHT}">${key}</svg>`));
  return imageCache.get(key);
};
// the same facts as words, for the panel
const factPills = (n) => {
  const out = [];
  const pill = (text, color, glyph) => h("span", { class: "pill" }, glyph, text);
  if (n.kind === "component") return out; // a class says what it is in its panel
  if (n.kind === "system") {
    out.push(pill(n.ownSystem ? `${(appsOf.get(n.id) || []).length} apps` : n.relationship === "External" ? "external" : "other system", GREY, glyphImg(n.ownSystem ? "System" : "External", GREY, 12)));
  } else if (n.kind === "person") {
    out.push(pill(n.relationship === "External" ? "external person" : "person", kindColor("Person"), glyphImg("Person", kindColor("Person"), 12)));
  } else {
    out.push(pill(n.appKind || "Unspecified", kindColor(n.appKind), glyphImg(n.appKind || "Unspecified", kindColor(n.appKind), 12)));
    if (n.tier) out.push(pill(tierText(n.tier), GREY, null));
    if (n.lifecycle) out.push(pill(n.lifecycle, lifeColor(n.lifecycle), glyphImg("Lifecycle", lifeColor(n.lifecycle), 12)));
    if (!n.hasMeta) out.push(pill("undocumented", AMBER, glyphImg("Undocumented", AMBER, 12)));
  }
  return out;
};

// ---------------------------------------------------------------- theme
// auto follows the system; light and dark override it. The choice is kept in localStorage when the browser allows it.
const THEMES = ["auto", "light", "dark"];
let theme = "auto";
try { const saved = localStorage.getItem("haya-theme"); if (THEMES.includes(saved)) theme = saved; } catch { /* storage can be blocked, e.g. in a private window */ }
const themeButton = document.getElementById("theme");
const applyTheme = () => {
  if (theme === "auto") document.documentElement.removeAttribute("data-theme");
  else document.documentElement.setAttribute("data-theme", theme);
  const label = "Theme: " + theme[0].toUpperCase() + theme.slice(1);
  themeButton.textContent = label;
  themeButton.setAttribute("aria-label", label + ". Switch between automatic, light and dark");
};
applyTheme();

// ---------------------------------------------------------------- graph
const styleFor = () => {
  const fg = css("--fg"), muted = css("--muted"), line = css("--line"), accent = css("--accent"), card = css("--card"), bg = css("--bg");
  return [
    { selector: "node", style: {
      label: "data(label)", color: fg, "font-size": 12, "text-valign": "center", "text-halign": "center", "text-wrap": "ellipsis",
      "text-max-width": 112, "text-margin-x": -9, shape: "round-rectangle", width: W, height: HEIGHT, "background-color": card,
      "border-width": 1, "border-color": line, "background-image": "data(img)", "background-fit": "contain", "background-clip": "node" } },
    { selector: "node.system", style: { "border-width": 2, "border-color": muted } },
    { selector: "node.component", style: { "text-margin-x": 0, "text-max-width": W - 16, "border-style": "solid", "background-image": "none" } },
    { selector: "node.person", style: { shape: "round-rectangle", "corner-radius": 15 } },
    { selector: "node.external", style: { "border-style": "dashed" } },
    { selector: "node.undoc", style: { "border-style": "dashed", "border-color": "#d69e2e" } },
    { selector: ":parent", style: {
      shape: "rectangle", "text-valign": "top", "text-margin-x": 0, "text-margin-y": -4, "text-max-width": 600, "font-size": 13, "font-weight": "bold", "background-image": "none",
      "background-color": muted, "background-opacity": 0.08, "border-style": "dashed", "border-width": 1.5, "border-color": muted, padding: 24 } },
    { selector: "node[heat]", style: { "border-color": "data(heat)", "border-width": 3 } },
    { selector: ":parent[heat]", style: { "border-width": 2.5 } },
    { selector: "edge", style: {
      "curve-style": "bezier", width: "mapData(n, 1, 12, 1.5, 5)", "line-color": muted, "target-arrow-color": muted, "target-arrow-shape": "triangle",
      "source-arrow-color": muted, label: "data(label)", "font-size": 10, "font-weight": "bold", color: fg, "text-background-color": bg,
      "text-background-opacity": 1, "text-background-padding": 2, "text-background-shape": "roundrectangle", "overlay-padding": 6 } },
    { selector: "edge[heat]", style: { "line-color": "data(heat)", "target-arrow-color": "data(heat)", "source-arrow-color": "data(heat)" } },
    { selector: "edge[?both]", style: { "source-arrow-shape": "triangle" } },
    { selector: ".sel", style: { "border-color": accent, "border-width": 3, "line-color": accent, "target-arrow-color": accent, "source-arrow-color": accent } },
    { selector: ".muted", style: { opacity: 0.22 } },
    { selector: ".dim", style: { opacity: 0.18, events: "no" } },
  ];
};

const cy = cytoscape({ container: document.getElementById("graph"), elements: [], style: styleFor(), wheelSensitivity: 0.3, autounselectify: true, boxSelectionEnabled: false });
window.__haya = cy; // test hook
const edgeItems = new Map(); // drawn edge id -> { source, target, fwd: model edges that way, back: the other way }
// node images load asynchronously and the canvas can be sized before the window settles, so redraw and re-measure
let redrawTimer;
const redrawSoon = () => { clearTimeout(redrawTimer); redrawTimer = setTimeout(() => { cy.resize(); cy.forceRender(); }, 150); };
new ResizeObserver(redrawSoon).observe(document.getElementById("graph"));

const desired = () => {
  const want = { nodes: new Map(), edges: new Map() };
  for (const n of data.nodes) if (n.kind === "system" || n.kind === "person" || shown.has(n.id)) want.nodes.set(n.id, n); // people are always drawn
  for (const a of opened) if (shown.has(a)) for (const c of classesOf.get(a) || []) want.nodes.set(c, nodes.get(c));
  // one line per pair of drawn nodes, whichever way the relationships go; an opened app's end is its classes, when it has any there
  const ends = (id, classes) => (isOpenApp(id) && classes && classes.length ? classes : [rep(id)]);
  for (const e of data.edges) for (const f of ends(e.from, e.fromComponents)) for (const t of ends(e.to, e.toComponents)) {
    // inside one box, or between a boundary and something in it: nothing to draw
    if (f === t || within(f, t) || within(t, f)) continue;
    const key = f < t ? f + "|" + t : t + "|" + f;
    if (!want.edges.has(key)) want.edges.set(key, { source: f, target: t, fwd: [], back: [] });
    const g = want.edges.get(key), list = f === g.source ? g.fwd : g.back;
    if (!list.includes(e)) list.push(e);
  }
  return want;
};

const nodeData = (n, includeParent) => {
  const open = n.kind === "system" && isOpen(n.id);
  let label = name(n.id);
  if (open) {
    const total = appsOf.get(n.id).length, now = appsOf.get(n.id).filter((a) => shown.has(a)).length;
    if (now < total) label += ` (${now} of ${total} apps)`;
  }
  const d = { id: n.id, label, img: (n.kind === "system" && open) || isOpenApp(n.id) ? "none" : nodeImage(n) };
  if (includeParent && (n.kind === "app" || n.kind === "component") && n.parent) d.parent = n.parent; // a parent key on an existing node would move it
  if (heatOf.has(n.id)) d.heat = heatOf.get(n.id);
  return d;
};
const classOf = (n) =>
  n.kind === "system" ? "system" + (n.ownSystem ? "" : " external")
  : n.kind === "component" ? "component"
  : n.kind === "person" ? "person" + (n.relationship === "External" ? " external" : "")
  : "app" + (n.hasMeta ? "" : " undoc");

/** Bring the drawn graph in line with the state. `seeds` maps a node id to where to start it (else beside its system). */
const sync = (seeds = new Map()) => {
  const want = desired();
  const gone = cy.elements().filter((el) => !(el.isNode() ? want.nodes.has(el.id()) : want.edges.has(el.id())));
  cy.remove(gone);
  const fresh = [];
  cy.batch(() => {
    for (const kind of ["system", "person", "app", "component"]) for (const n of want.nodes.values()) {
      if (n.kind !== kind) continue;
      const cur = cy.getElementById(n.id);
      if (cur.nonempty()) { cur.data(nodeData(n, false)); continue; }
      const parent = n.parent && cy.getElementById(n.parent);
      const at = seeds.get(n.id) || (parent && parent.nonempty() ? parent.position() : undefined);
      const el = cy.add({ group: "nodes", data: nodeData(n, true), classes: classOf(n), position: at ? { x: at.x + (Math.random() - 0.5) * 30, y: at.y + (Math.random() - 0.5) * 30 } : undefined });
      fresh.push(el);
    }
    edgeItems.clear();
    for (const [key, g] of want.edges) {
      edgeItems.set(key, g);
      const n = g.fwd.length + g.back.length, both = g.fwd.length > 0 && g.back.length > 0;
      const d = { label: String(n), n, both };
      if (nodes.get(g.source).kind === "system" && nodes.get(g.target).kind === "system" && lineHeat.has(key)) d.heat = lineHeat.get(key);
      const cur = cy.getElementById(key);
      if (cur.nonempty()) cur.data(d);
      else cy.add({ group: "edges", data: { id: key, source: g.source, target: g.target, ...d } });
    }
  });
  applyFilters();
  applySelection();
  redrawSoon();
  return fresh;
};

const layout = (first, fresh = []) => {
  const run = cy.layout({
    name: "fcose", animate: !first, animationDuration: 300, randomize: first, fit: first, padding: 40, nodeDimensionsIncludeLabels: true,
    idealEdgeLength: 90, nodeRepulsion: 4500, gravity: 0.7, gravityRange: 2.5, packComponents: true,
  });
  run.one("layoutstop", () => { if (!first) ensureVisible(); redrawSoon(); });
  run.run();
};
// after growing the graph, pan/zoom only when something ended up outside the view
const ensureVisible = () => {
  const ext = cy.extent(), bb = cy.elements().boundingBox();
  if (bb.x1 < ext.x1 || bb.y1 < ext.y1 || bb.x2 > ext.x2 || bb.y2 > ext.y2) cy.animate({ fit: { eles: cy.elements(), padding: 40 }, duration: 250 });
};
const update = (seeds) => { const fresh = sync(seeds); layout(false, fresh); renderPanel(); };

// ---------------------------------------------------------------- actions
const posOf = (id) => { const el = cy.getElementById(id); return el.nonempty() ? el.position() : undefined; };
const openSystem = (id) => {
  const at = posOf(id), seeds = new Map();
  for (const a of appsOf.get(id) || []) { shown.add(a); if (at) seeds.set(a, at); }
  update(seeds);
};
const collapseSystem = (id) => {
  for (const a of appsOf.get(id) || []) { shown.delete(a); opened.delete(a); }
  if (selected && selected.id !== id && within(selected.id, id)) selected = { kind: "node", id };
  update();
};
const hideApp = (id) => { shown.delete(id); opened.delete(id); if (selected && selected.id === id) selected = { kind: "node", id: nodes.get(id).parent }; update(); };
const neighboursOf = (id) => {
  const out = new Set();
  for (const e of touching.get(id) || []) { const o = e.from === id ? e.to : e.from; if (nodes.get(o).kind === "app" && !shown.has(o)) out.add(o); }
  return [...out];
};
const expandNeighbours = (id) => {
  if (nodes.get(id).kind === "system") return openSystem(id);
  const at = posOf(id), seeds = new Map();
  for (const o of neighboursOf(id)) { shown.add(o); if (at) seeds.set(o, at); }
  update(seeds);
};
/** Open an app to show its classes inside it, and show the apps it connects to so its lines have somewhere to go. */
const openApp = (id) => {
  const at = posOf(id), seeds = new Map();
  shown.add(id);
  opened.add(id);
  for (const c of classesOf.get(id) || []) if (at) seeds.set(c, at);
  for (const o of neighboursOf(id)) { shown.add(o); if (at) seeds.set(o, at); }
  update(seeds);
};
const closeApp = (id) => {
  opened.delete(id);
  if (selected && selected.id !== id && within(selected.id, id)) selected = { kind: "node", id };
  update();
};
/** Make a node visible (showing just that app inside its boundary, or opening the app of a class), select it and bring it into view. */
const reveal = (id) => {
  const n = nodes.get(id);
  const app = n.kind === "component" ? n.parent : n.kind === "app" ? id : null;
  const hidden = app && (!shown.has(app) || (n.kind === "component" && !opened.has(app)));
  selected = { kind: "node", id };
  if (hidden) {
    shown.add(app);
    if (n.kind === "component") opened.add(app);
    const at = posOf(app) || posOf(nodes.get(app).parent), seeds = new Map();
    if (at) { seeds.set(app, at); for (const c of classesOf.get(app) || []) seeds.set(c, at); }
    const fresh = sync(seeds);
    layout(false, fresh);
  }
  applySelection();
  renderPanel();
  setTimeout(() => { const el = cy.getElementById(id); if (el.nonempty()) cy.animate({ center: { eles: el }, duration: 250 }); }, 350);
};
const select = (sel) => { selected = sel; applySelection(); renderPanel(); };
const applySelection = () => {
  cy.elements().removeClass("sel");
  if (selected) cy.getElementById(selected.id).addClass("sel");
  applyFocus();
};
const reset = () => {
  resetShown(); opened = new Set(); selected = null; enableAll();
  document.getElementById("search").value = ""; closeResults(); renderFilters();
  cy.elements().remove(); sync(); layout(true); renderPanel();
};

cy.on("tap", "node", (e) => select({ kind: "node", id: e.target.id() }));
cy.on("tap", "edge", (e) => select({ kind: "edge", id: e.target.id() }));
cy.on("tap", (e) => { if (e.target === cy) select(null); });
// what double-click and Enter/Space do: open or collapse a system, open or close an app with classes, or show the apps any
// other app (or a person) connects to. A class has nothing to open.
const toggleExpand = (id) => {
  const n = nodes.get(id);
  if (n.kind === "system") isOpen(id) ? collapseSystem(id) : openSystem(id);
  else if (n.kind === "app" && classesOf.has(id)) isOpenApp(id) ? closeApp(id) : openApp(id);
  else if (n.kind !== "component") expandNeighbours(id);
};
cy.on("dbltap", "node", (e) => toggleExpand(e.target.id()));

// ---------------------------------------------------------------- filters
const matches = (n) => FILTERS.every((f) => enabled[f.key].has(f.of(n)));
function applyFilters() {
  cy.elements().removeClass("dim");
  cy.nodes().forEach((el) => { const n = nodes.get(el.id()); if (n.kind === "app" && !matches(n)) el.addClass("dim"); });
  cy.nodes(".component").forEach((el) => { if (el.parent().hasClass("dim")) el.addClass("dim"); });
  cy.edges().forEach((el) => { if (el.source().hasClass("dim") || el.target().hasClass("dim")) el.addClass("dim"); });
}
// Mute whatever has no relationship with the focus: the lines that do not touch it and the systems or apps only those lines
// reach. The focus is the selected node (a system with its drawn apps, or an app with its boundary), otherwise the open
// systems. Muted things stay clickable, unlike filtered-out ones.
function applyFocus() {
  cy.elements().removeClass("muted");
  const focus = new Set();
  const addApp = (id) => { focus.add(id); if (isOpenApp(id)) for (const c of classesOf.get(id) || []) focus.add(c); };
  const addSystem = (id) => { focus.add(id); for (const a of appsOf.get(id) || []) if (shown.has(a)) addApp(a); };
  const sel = selected && selected.kind === "node" ? nodes.get(selected.id) : null;
  if (sel) {
    if (sel.kind === "system") addSystem(sel.id);
    else if (sel.kind === "app") { addApp(sel.id); if (sel.parent) focus.add(sel.parent); }
    else for (let id = sel.id; id; id = nodes.get(id).parent) focus.add(id); // a person, or a class with its app and system
  } else for (const n of data.nodes) if (n.kind === "system" && isOpen(n.id)) addSystem(n.id);
  if (!focus.size) return;
  cy.edges().forEach((e) => { if (!focus.has(e.source().id()) && !focus.has(e.target().id())) e.addClass("muted"); });
  cy.nodes().forEach((el) => { if (!focus.has(el.id()) && !el.connectedEdges().some((e) => !e.hasClass("muted"))) el.addClass("muted"); });
  // an app that an unmuted line reaches keeps its system boundary lit too (a compound is not touched by lines drawn to its apps)
  cy.nodes().forEach((el) => { if (!el.hasClass("muted")) el.ancestors().removeClass("muted"); });
}
// the chips are the legend for kind, tier and lifecycle: glyph and name, pressed = shown
function renderFilters() {
  const box = document.getElementById("filters");
  box.replaceChildren();
  for (const f of FILTERS) {
    const values = valuesOf(f.key, f.of);
    if (values.length < 2) continue;
    const glyph = (v) =>
      f.key === "appKind" ? glyphImg(v, kindColor(v === "Unspecified" ? "" : v), 13) :
      f.key === "lifecycle" ? glyphImg("Lifecycle", v === "none" ? GREY : lifeColor(v), 13) : null;
    box.append(h("span", { class: "fgroup", role: "group", "aria-label": f.title }, h("span", {}, f.title),
      values.map((v) => h("button", { type: "button", class: "chip", title: (enabled[f.key].has(v) ? "Hide " : "Show ") + v, "aria-pressed": String(enabled[f.key].has(v)), onclick: (e) => {
        enabled[f.key].has(v) ? enabled[f.key].delete(v) : enabled[f.key].add(v);
        e.currentTarget.setAttribute("aria-pressed", String(enabled[f.key].has(v)));
        e.currentTarget.title = (enabled[f.key].has(v) ? "Hide " : "Show ") + v;
        applyFilters();
      } }, glyph(v), v))));
  }
}

// ---------------------------------------------------------------- search
const searchBox = document.getElementById("search"), resultsEl = document.getElementById("results");
function closeResults() { resultsEl.hidden = true; resultsEl.replaceChildren(); }
const searchFor = (q) => {
  q = q.trim().toLowerCase();
  if (!q) return [];
  const score = (n) => {
    const nm = (n.name || n.id).toLowerCase();
    if (nm.startsWith(q)) return 0;
    if (nm.includes(q)) return 1;
    return [n.description, n.tech, n.owner, n.repository].some((t) => t && t.toLowerCase().includes(q)) ? 2 : 9;
  };
  return data.nodes.map((n) => [score(n), n]).filter(([s]) => s < 9).sort((a, b) => a[0] - b[0] || name(a[1].id).localeCompare(name(b[1].id))).slice(0, 12).map(([, n]) => n);
};
const pick = (id) => { closeResults(); searchBox.value = ""; searchBox.blur(); reveal(id); };
searchBox.addEventListener("input", () => {
  const found = searchFor(searchBox.value);
  resultsEl.replaceChildren(...found.map((n) => h("li", {}, h("button", { type: "button", onclick: () => pick(n.id), onkeydown: (e) => {
    if (e.key === "ArrowDown") { e.preventDefault(); e.currentTarget.parentElement.nextElementSibling?.firstChild.focus(); }
    if (e.key === "ArrowUp") { e.preventDefault(); (e.currentTarget.parentElement.previousElementSibling?.firstChild || searchBox).focus(); }
    if (e.key === "Escape") { e.stopPropagation(); closeResults(); searchBox.focus(); } // not also a deselect
  } }, name(n.id) + (n.kind === "app" && n.parent ? " · " + name(n.parent) : n.kind === "system" ? " · system" : n.kind === "person" ? " · person" : "")))));
  resultsEl.hidden = found.length === 0;
});
searchBox.addEventListener("keydown", (e) => {
  if (e.key === "ArrowDown") { e.preventDefault(); resultsEl.querySelector("button")?.focus(); }
  if (e.key === "Enter") { const f = searchFor(searchBox.value)[0]; if (f) pick(f.id); }
  if (e.key === "Escape") { closeResults(); searchBox.value = ""; searchBox.blur(); }
});
// Esc lets go of the selection. Unlike the keys below it works with a button or link focused (Esc does nothing there), but not in
// a text field or the search results, where Esc already means "close this".
document.addEventListener("keydown", (e) => {
  if (e.key !== "Escape" || e.ctrlKey || e.metaKey || e.altKey || !selected) return;
  const t = e.target;
  if (t instanceof HTMLElement && (t.isContentEditable || ["INPUT", "TEXTAREA", "SELECT"].includes(t.tagName) || t.closest("#results"))) return;
  e.preventDefault();
  select(null);
});
// Page-level keys. They leave alone anything that already handles keys itself: fields, buttons and links, and any modifier.
const handlesKeys = (t) => t instanceof HTMLElement && (t.isContentEditable || ["INPUT", "TEXTAREA", "SELECT", "BUTTON", "A", "SUMMARY"].includes(t.tagName));
document.addEventListener("keydown", (e) => {
  if (e.ctrlKey || e.metaKey || e.altKey || handlesKeys(e.target)) return;
  if (e.key === "/") {
    // jump to the search box, as in vim and most web apps
    e.preventDefault();
    searchBox.focus();
    searchBox.select();
  } else if (e.key === "r") {
    // start over, like the Reset button
    e.preventDefault();
    reset();
  } else if ((e.key === "Enter" || e.key === " ") && selected && selected.kind === "node") {
    // expand the selected node, like a double-click
    e.preventDefault(); // Space would scroll the page
    toggleExpand(selected.id);
  }
});
document.getElementById("fit").addEventListener("click", () => cy.animate({ fit: { eles: cy.elements(), padding: 40 }, duration: 250 }));
document.getElementById("reset").addEventListener("click", reset);

// ---------------------------------------------------------------- side panel
const panel = document.getElementById("panel");
const act = (text, fn) => h("button", { type: "button", onclick: fn }, text);
const goto = (id) => h("button", { type: "button", class: "link", onclick: () => reveal(id) }, name(id));
// The glyph before an app in a system's list: its kind once it is drawn, a closed eye while it is not. Screen readers get words.
const appMark = (id) => {
  const n = nodes.get(id), drawn = shown.has(id);
  const [glyph, color, word, tip] = !drawn ? ["EyeClosed", GREY, "Hidden:", "Not shown on the graph. Click its name to add it."]
    : !n.hasMeta ? ["Undocumented", AMBER, "Undocumented:", "Undocumented"]
    : [n.appKind || "Unspecified", kindColor(n.appKind), (n.appKind || "App") + ":", n.appKind || "App"];
  return h("span", { class: "mark", title: tip }, h("span", { "aria-hidden": "true" }, glyphImg(glyph, color, 14)), h("span", { class: "sr-only" }, word));
};
const field = (term, value) => (value ? [h("dt", {}, term), h("dd", {}, value)] : []);
const note = (e) => [e.label || "(no description)", e.protocol && ` [${e.protocol}]`].filter(Boolean).join("");
/** A relationship seen from `id`: who is at the other end, then what it carries. */
const relRow = (e, id) => h("li", {}, goto(e.from === id || inside(id, e.from) ? e.to : e.from), " · " + note(e));
const inside = (id, other) => nodes.get(id).kind === "system" && (appsOf.get(id) || []).includes(other);
const section = (title, items) => (items.length ? [h("h3", {}, title), h("ul", {}, items)] : []);

function nodePanel(n) {
  const kids = [h("h2", {}, name(n.id)), h("div", {}, factPills(n))];
  if (n.kind === "app") {
    if (!n.hasMeta) kids.push(h("p", { class: "note" }, "Known only from a collaborator declaration. The app has no [Meta] of its own, so these details come from the app that uses it."));
    if (n.description) kids.push(h("p", {}, n.description));
    kids.push(h("dl", {}, field("System", n.parent && name(n.parent)), field("Tech", n.tech), field("Owner", n.owner), field("Repository", n.repository), field("Tier", n.tier), field("Lifecycle", n.lifecycle)));
    const hidden = neighboursOf(n.id).length;
    const classes = (classesOf.get(n.id) || []).length;
    kids.push(h("div", { class: "actions" },
      classes > 0 && (isOpenApp(n.id) ? act("Hide classes", () => closeApp(n.id)) : act(`Show classes (${classes})`, () => openApp(n.id))),
      h("button", { type: "button", disabled: !hidden, onclick: () => expandNeighbours(n.id) }, hidden ? `Show connected apps (${hidden})` : "All connected apps shown"),
      n.parent && act("Hide", () => hideApp(n.id)),
      act("Centre", () => cy.animate({ center: { eles: cy.getElementById(n.id) }, duration: 250 }))));
    kids.push(section("Responsibilities", (n.responsibilities || []).map((r) => h("li", {}, r))));
    // the classes that carry the app's attributes, with the file (or referenced assembly) they are in
    kids.push(section("Components", (n.components || []).map((c) => h("li", { class: "component" }, goto(c.id), c.source && h("div", { class: "muted source" }, c.source)))));
    const mine = touching.get(n.id) || [];
    kids.push(section("Inbound", mine.filter((e) => e.to === n.id).map((e) => relRow(e, n.id))));
    kids.push(section("Outbound", mine.filter((e) => e.from === n.id).map((e) => relRow(e, n.id))));
  } else if (n.kind === "component") {
    // a class of an app: where it is, what it does, and the relationships written on it (or that name it from another app)
    kids.push(h("p", {}, h("span", { class: "pill" }, "class"), " in ", goto(n.parent)));
    if (n.source) kids.push(h("p", { class: "muted source" }, n.source));
    kids.push(h("div", { class: "actions" }, act("Centre", () => cy.animate({ center: { eles: cy.getElementById(n.id) }, duration: 250 }))));
    kids.push(section("Responsibilities", (n.responsibilities || []).map((r) => h("li", {}, r))));
    const mine = classTouching.get(n.id) || [];
    const other = (e) => ((e.fromComponents || []).includes(n.id) ? e.to : e.from);
    const row = (e) => h("li", {}, goto(other(e)), " · " + note(e));
    kids.push(section("Inbound", mine.filter((e) => (e.toComponents || []).includes(n.id)).map(row)));
    kids.push(section("Outbound", mine.filter((e) => (e.fromComponents || []).includes(n.id)).map(row)));
  } else if (n.kind === "person") {
    if (n.description) kids.push(h("p", {}, n.description));
    kids.push(h("dl", {}, field("Owner", n.owner)));
    const hidden = neighboursOf(n.id).length;
    kids.push(h("div", { class: "actions" },
      h("button", { type: "button", disabled: !hidden, onclick: () => expandNeighbours(n.id) }, hidden ? `Show connected apps (${hidden})` : "All connected apps shown"),
      act("Centre", () => cy.animate({ center: { eles: cy.getElementById(n.id) }, duration: 250 }))));
    const mine = touching.get(n.id) || [];
    kids.push(section("Inbound", mine.filter((e) => e.to === n.id).map((e) => relRow(e, n.id))));
    kids.push(section("Outbound", mine.filter((e) => e.from === n.id).map((e) => relRow(e, n.id))));
  } else {
    // by kind, then name, both alphabetical; an app with no kind sorts as "Unspecified"
    const kindOf = (id) => nodes.get(id).appKind || "Unspecified";
    if (n.ownSystem && complexity.has(n.id)) {
      const c = complexity.get(n.id);
      kids.push(h("dl", {}, field("Complexity", `${c.apps} apps + ${c.relationships} relationships = ${c.total}`)));
    }
    const members = [...(appsOf.get(n.id) || [])].sort((x, y) => kindOf(x).localeCompare(kindOf(y)) || name(x).localeCompare(name(y)));
    if (n.description) kids.push(h("p", {}, n.description));
    if (!n.ownSystem) kids.push(h("p", { class: "muted" }, "A system outside the apps documented here, known from collaborators."));
    if (members.length) {
      const open = isOpen(n.id), all = members.every((a) => shown.has(a));
      kids.push(h("div", { class: "actions" }, !all && act(open ? "Show all apps" : "Open", () => openSystem(n.id)), open && act("Collapse", () => collapseSystem(n.id))));
      kids.push(section("Apps", members.map((a) => h("li", { class: "app-row" }, appMark(a), goto(a)))));
      const inside = new Set(members);
      const links = new Map();
      for (const a of members) for (const e of touching.get(a) || []) {
        if (inside.has(e.from) && inside.has(e.to)) continue;
        links.set(`${e.from}|${e.to}|${e.label}`, e);
      }
      kids.push(section("Inbound", [...links.values()].filter((e) => inside.has(e.to)).map((e) => h("li", {}, goto(e.from), " → ", goto(e.to), " · " + note(e)))));
      kids.push(section("Outbound", [...links.values()].filter((e) => inside.has(e.from)).map((e) => h("li", {}, goto(e.from), " → ", goto(e.to), " · " + note(e)))));
    } else {
      const mine = touching.get(n.id) || [];
      kids.push(section("Inbound", mine.filter((e) => e.to === n.id).map((e) => relRow(e, n.id))));
      kids.push(section("Outbound", mine.filter((e) => e.from === n.id).map((e) => relRow(e, n.id))));
    }
  }
  return kids;
}

function overviewPanel() {
  const apps = data.nodes.filter((n) => n.kind === "app"), systems = data.nodes.filter((n) => n.kind === "system");
  const people = data.nodes.filter((n) => n.kind === "person");
  const undocumented = apps.filter((n) => !n.hasMeta);
  return [
    h("h2", {}, "Overview"),
    h("p", {}, `${systems.length} systems, ${apps.length} apps, ${people.length ? people.length + " people, " : ""}${data.edges.length} relationships.`),
    h("p", { class: "muted" }, "Select a node or line for details. Selecting a node mutes what has no relationship with it. Double-click a system to open it, or an app to open it into its classes (with the apps it connects to) or, for an app without classes, to show the apps it connects to. Press Enter or Space to do the same to the selected node, Esc to deselect, r to start over. Search (press / to jump to it) finds anything, hidden or not. Switch a kind, tier or lifecycle off to fade those apps."),
    section("Systems", systems.map((n) => h("li", {}, goto(n.id), " ", factPills(n)))),
    section("People", people.map((n) => h("li", {}, goto(n.id), " ", factPills(n)))),
    section("Undocumented apps", undocumented.map((n) => h("li", {}, goto(n.id), n.parent ? " · " + name(n.parent) : ""))),
  ];
}

function renderPanel() {
  let kids;
  if (selected && selected.kind === "node" && nodes.has(selected.id)) kids = nodePanel(nodes.get(selected.id));
  else if (selected && selected.kind === "edge" && edgeItems.has(selected.id)) {
    const g = edgeItems.get(selected.id), a = name(g.source), b = name(g.target);
    // between boxes that stand for several apps, say which apps; between two apps the header says it
    const wide = nodes.get(g.source).kind === "system" || nodes.get(g.target).kind === "system";
    const bullet = (e) => h("li", {}, wide ? [goto(e.from), " → ", goto(e.to), ": "] : "", note(e));
    const both = g.fwd.length > 0 && g.back.length > 0;
    kids = [h("h2", {}, both ? `${a} ↔ ${b}` : g.fwd.length ? `${a} → ${b}` : `${b} → ${a}`)];
    if (both) kids.push(h("h3", {}, `${a} → ${b}`), h("ul", {}, g.fwd.map(bullet)), h("h3", {}, `${b} → ${a}`), h("ul", {}, g.back.map(bullet)));
    else kids.push(h("ul", {}, (g.fwd.length ? g.fwd : g.back).map(bullet)));
  } else kids = overviewPanel();
  panel.replaceChildren(...kids.flat(Infinity).filter(Boolean));
}

// ---------------------------------------------------------------- legend
// what the chips do not explain: the box types and the lines
(() => {
  const items = [
    [glyphImg("System", GREY), "System, with its number of apps"],
    [glyphImg("External", GREY), "Outside the documented systems"],
    [glyphImg("Undocumented", AMBER), "Undocumented: no [Meta] of its own"],
    ...(data.nodes.some((n) => n.kind === "person") ? [[glyphImg("Person", kindColor("Person")), "Person: a user, dashed when outside the organisation"]] : []),
    [heatBar(), `System border, by apps + relationships: plain under ${appStops[0]}, yellow from ${appStops[0]}, orange by ${appStops[2]}, deep red at ${appStops[3]}`],
    [heatBar(), `Line between systems, by relationships: plain under ${linkStops[0]}, yellow from ${linkStops[0]}, orange by ${linkStops[2]}, deep red at ${linkStops[3]}`],
    [h("span", { class: "arrows" }, "→ ↔"), "One line per pair, with the number of relationships"],
  ];
  document.getElementById("legend-list").append(...items.map(([g, t]) => h("li", {}, g, h("span", {}, t))));
})();

// ---------------------------------------------------------------- start
const restyle = () => { cy.style(styleFor()); redrawSoon(); };
window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", restyle);
themeButton.addEventListener("click", () => {
  theme = THEMES[(THEMES.indexOf(theme) + 1) % THEMES.length];
  try { localStorage.setItem("haya-theme", theme); } catch { /* not remembered */ }
  applyTheme();
  restyle();
});
resetShown();
renderFilters();
sync();
layout(true);
renderPanel();
// deep links: #open=system-id,other&select=node-id
const params = new URLSearchParams(location.hash.slice(1));
for (const id of (params.get("open") || "").split(",")) if (nodes.has(id) && appsOf.has(id)) for (const a of appsOf.get(id)) shown.add(a);
if (params.get("open")) { sync(); layout(true); }
if (params.get("select") && nodes.has(params.get("select"))) reveal(params.get("select"));
