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

// ---------------------------------------------------------------- state
// What is visible follows from `shown` (the apps drawn): a system with shown apps is a boundary around them, one
// without is a single box. Systems are always drawn; edges between hidden apps roll up to their systems.
let shown = new Set();
let selected = null; // { kind: "node" | "edge", id }
const filters = { appKind: new Set(), tier: new Set(), lifecycle: new Set() };
const resetShown = () => { shown = new Set(data.nodes.filter((n) => n.kind === "app" && !n.parent).map((n) => n.id)); };
const isOpen = (systemId) => (appsOf.get(systemId) || []).some((a) => shown.has(a));
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

// ---------------------------------------------------------------- badges
const KIND_FILL = { Api: "#2f6fdb", Service: "#6f42c1", WebApp: "#0a8f6a", Worker: "#b7791f", Database: "#c05621", Queue: "#b83280" };
const LIFE_FILL = { production: "#2f855a", experimental: "#b7791f", deprecated: "#c53030" };
const GREY = "#667085", AMBER = "#b7791f";
const xml = (s) => String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&apos;" })[c]);
const tierText = (t) => (/^t/i.test(t) ? t : "T" + t);

const W = 176, HEIGHT = 60;
const imageCache = new Map();
const badgeImage = (pills) => {
  const key = JSON.stringify(pills);
  if (imageCache.has(key)) return imageCache.get(key);
  let x = 8;
  const parts = [];
  for (const [text, fill] of pills) {
    const w = Math.round(text.length * 6 + 12);
    if (x + w > W - 4) break;
    parts.push(`<rect x="${x}" y="${HEIGHT - 22}" rx="8" ry="8" width="${w}" height="16" fill="${fill}"/>` +
      `<text x="${x + w / 2}" y="${HEIGHT - 10.5}" font-size="10" font-family="system-ui,sans-serif" fill="#fff" text-anchor="middle">${xml(text)}</text>`);
    x += w + 4;
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${HEIGHT}">${parts.join("")}</svg>`;
  const uri = "data:image/svg+xml;utf8," + encodeURIComponent(svg);
  imageCache.set(key, uri);
  return uri;
};
const pillsOf = (n) => {
  if (n.kind === "system") {
    const count = (appsOf.get(n.id) || []).length;
    return n.ownSystem ? (count ? [[count + (count === 1 ? " app" : " apps"), GREY]] : []) : [[n.relationship === "External" ? "external" : "other system", GREY]];
  }
  const pills = [];
  if (n.appKind) pills.push([n.appKind, KIND_FILL[n.appKind] || GREY]);
  if (n.tier) pills.push([tierText(n.tier), GREY]);
  if (n.lifecycle) pills.push([n.lifecycle, LIFE_FILL[n.lifecycle.toLowerCase()] || GREY]);
  if (!n.hasMeta) pills.push(["undocumented", AMBER]);
  return pills;
};
const pillEls = (n) => pillsOf(n).map(([t, f]) => h("span", { class: "pill", style: "background:" + f }, t));

// ---------------------------------------------------------------- graph
const styleFor = () => {
  const fg = css("--fg"), muted = css("--muted"), line = css("--line"), accent = css("--accent"), card = css("--card"), bg = css("--bg");
  return [
    { selector: "node", style: {
      label: "data(label)", color: fg, "font-size": 12, "text-valign": "center", "text-halign": "center", "text-wrap": "ellipsis",
      "text-max-width": 150, "text-margin-y": -9, shape: "round-rectangle", width: W, height: HEIGHT, "background-color": card,
      "border-width": 1, "border-color": line, "background-image": "data(img)", "background-fit": "contain", "background-clip": "node" } },
    { selector: "node.system", style: { "border-width": 2, "border-color": muted } },
    { selector: "node.external", style: { "border-style": "dashed" } },
    { selector: ":parent", style: {
      shape: "rectangle", "text-valign": "top", "text-margin-y": -4, "font-size": 13, "font-weight": "bold", "background-image": "none",
      "background-color": muted, "background-opacity": 0.08, "border-style": "dashed", "border-width": 1.5, "border-color": muted, padding: 24 } },
    { selector: "edge", style: {
      "curve-style": "bezier", width: 1.5, "line-color": muted, "target-arrow-color": muted, "target-arrow-shape": "triangle", label: "data(label)",
      "font-size": 10, color: fg, "text-background-color": bg, "text-background-opacity": 1, "text-background-padding": 2,
      "text-rotation": "autorotate", "text-wrap": "ellipsis", "text-max-width": 130 } },
    { selector: "edge[n > 1]", style: { width: 3 } },
    { selector: ".sel", style: { "border-color": accent, "border-width": 3, "line-color": accent, "target-arrow-color": accent } },
    { selector: ".dim", style: { opacity: 0.2 } },
  ];
};

const cy = cytoscape({ container: document.getElementById("graph"), elements: [], style: styleFor(), wheelSensitivity: 0.3, autounselectify: true, boxSelectionEnabled: false });
window.__haya = cy; // test hook
const edgeItems = new Map(); // drawn edge id -> model edges it stands for

const desired = () => {
  const want = { nodes: new Map(), edges: new Map() };
  for (const n of data.nodes) if (n.kind === "system" || shown.has(n.id)) want.nodes.set(n.id, n);
  for (const e of data.edges) {
    const f = rep(e.from), t = rep(e.to);
    // inside one box, or between a boundary and an app in it: nothing to draw
    if (f === t || nodes.get(f).parent === t || nodes.get(t).parent === f) continue;
    const key = f + ">" + t;
    if (!want.edges.has(key)) want.edges.set(key, { source: f, target: t, items: [] });
    want.edges.get(key).items.push(e);
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
  const d = { id: n.id, label, img: n.kind === "system" && open ? "none" : badgeImage(pillsOf(n)) };
  if (includeParent && n.kind === "app" && n.parent) d.parent = n.parent; // a parent key on an existing node would move it
  return d;
};
const classOf = (n) => (n.kind === "system" ? "system" + (n.ownSystem ? "" : " external") : "app");

/** Bring the drawn graph in line with the state. `seeds` maps a node id to where to start it (else beside its system). */
const sync = (seeds = new Map()) => {
  const want = desired();
  const gone = cy.elements().filter((el) => !(el.isNode() ? want.nodes.has(el.id()) : want.edges.has(el.id())));
  cy.remove(gone);
  const fresh = [];
  cy.batch(() => {
    for (const kind of ["system", "app"]) for (const n of want.nodes.values()) {
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
      edgeItems.set(key, g.items);
      const label = g.items.length === 1 ? g.items[0].label || "" : g.items.length + " relationships";
      const cur = cy.getElementById(key);
      if (cur.nonempty()) cur.data({ label, n: g.items.length });
      else cy.add({ group: "edges", data: { id: key, source: g.source, target: g.target, label, n: g.items.length } });
    }
  });
  applyFilters();
  applySelection();
  return fresh;
};

const layout = (first, fresh = []) => {
  const run = cy.layout({
    name: "fcose", animate: !first, animationDuration: 300, randomize: first, fit: first, padding: 40, nodeDimensionsIncludeLabels: true,
    idealEdgeLength: 150, nodeRepulsion: 9000, packComponents: true,
  });
  run.one("layoutstop", () => { if (!first) ensureVisible(); });
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
  for (const a of appsOf.get(id) || []) { shown.delete(a); if (selected && selected.id === a) selected = { kind: "node", id }; }
  update();
};
const hideApp = (id) => { shown.delete(id); if (selected && selected.id === id) selected = { kind: "node", id: nodes.get(id).parent }; update(); };
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
/** Make a node visible (showing just that app inside its boundary), select it and bring it into view. */
const reveal = (id) => {
  const n = nodes.get(id);
  if (n.kind === "app" && !shown.has(id)) {
    shown.add(id);
    selected = { kind: "node", id };
    const at = posOf(n.parent), seeds = new Map();
    if (at) seeds.set(id, at);
    const fresh = sync(seeds);
    layout(false, fresh);
  } else selected = { kind: "node", id };
  applySelection();
  renderPanel();
  setTimeout(() => { const el = cy.getElementById(id); if (el.nonempty()) cy.animate({ center: { eles: el }, duration: 250 }); }, 350);
};
const select = (sel) => { selected = sel; applySelection(); renderPanel(); };
const applySelection = () => {
  cy.elements().removeClass("sel");
  if (selected) cy.getElementById(selected.id).addClass("sel");
};
const reset = () => {
  resetShown(); selected = null; for (const s of Object.values(filters)) s.clear();
  document.getElementById("search").value = ""; closeResults(); renderFilters();
  cy.elements().remove(); sync(); layout(true); renderPanel();
};

cy.on("tap", "node", (e) => select({ kind: "node", id: e.target.id() }));
cy.on("tap", "edge", (e) => select({ kind: "edge", id: e.target.id() }));
cy.on("tap", (e) => { if (e.target === cy) select(null); });
cy.on("dbltap", "node", (e) => {
  const id = e.target.id(), n = nodes.get(id);
  if (n.kind === "system") isOpen(id) ? collapseSystem(id) : openSystem(id);
  else expandNeighbours(id);
});

// ---------------------------------------------------------------- filters
const matches = (n) =>
  (!filters.appKind.size || filters.appKind.has(n.appKind || "Unspecified")) &&
  (!filters.tier.size || filters.tier.has(n.tier || "none")) &&
  (!filters.lifecycle.size || filters.lifecycle.has(n.lifecycle || "none"));
function applyFilters() {
  cy.elements().removeClass("dim");
  cy.nodes().forEach((el) => { const n = nodes.get(el.id()); if (n.kind === "app" && !matches(n)) el.addClass("dim"); });
  cy.edges().forEach((el) => { if (el.source().hasClass("dim") || el.target().hasClass("dim")) el.addClass("dim"); });
}
function renderFilters() {
  const box = document.getElementById("filters");
  box.replaceChildren();
  const apps = data.nodes.filter((n) => n.kind === "app");
  const groups = [
    ["Kind", "appKind", [...new Set(apps.map((n) => n.appKind || "Unspecified"))]],
    ["Tier", "tier", [...new Set(apps.map((n) => n.tier || "none"))]],
    ["Lifecycle", "lifecycle", [...new Set(apps.map((n) => n.lifecycle || "none"))]],
  ];
  for (const [title, key, values] of groups) {
    if (values.length < 2) continue;
    box.append(h("span", { class: "fgroup", role: "group", "aria-label": title }, h("span", {}, title),
      values.sort().map((v) => h("button", { type: "button", class: "chip", "aria-pressed": String(filters[key].has(v)), onclick: (e) => {
        filters[key].has(v) ? filters[key].delete(v) : filters[key].add(v);
        e.currentTarget.setAttribute("aria-pressed", String(filters[key].has(v)));
        applyFilters();
      } }, v))));
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
const pick = (id) => { closeResults(); searchBox.value = ""; reveal(id); };
searchBox.addEventListener("input", () => {
  const found = searchFor(searchBox.value);
  resultsEl.replaceChildren(...found.map((n) => h("li", {}, h("button", { type: "button", onclick: () => pick(n.id), onkeydown: (e) => {
    if (e.key === "ArrowDown") { e.preventDefault(); e.currentTarget.parentElement.nextElementSibling?.firstChild.focus(); }
    if (e.key === "ArrowUp") { e.preventDefault(); (e.currentTarget.parentElement.previousElementSibling?.firstChild || searchBox).focus(); }
    if (e.key === "Escape") { closeResults(); searchBox.focus(); }
  } }, name(n.id) + (n.kind === "app" && n.parent ? " · " + name(n.parent) : n.kind === "system" ? " · system" : "")))));
  resultsEl.hidden = found.length === 0;
});
searchBox.addEventListener("keydown", (e) => {
  if (e.key === "ArrowDown") { e.preventDefault(); resultsEl.querySelector("button")?.focus(); }
  if (e.key === "Enter") { const f = searchFor(searchBox.value)[0]; if (f) pick(f.id); }
  if (e.key === "Escape") { closeResults(); searchBox.value = ""; }
});
document.getElementById("fit").addEventListener("click", () => cy.animate({ fit: { eles: cy.elements(), padding: 40 }, duration: 250 }));
document.getElementById("reset").addEventListener("click", reset);

// ---------------------------------------------------------------- side panel
const panel = document.getElementById("panel");
const act = (text, fn) => h("button", { type: "button", onclick: fn }, text);
const goto = (id) => h("button", { type: "button", class: "link", onclick: () => reveal(id) }, name(id));
const field = (term, value) => (value ? [h("dt", {}, term), h("dd", {}, value)] : []);
const relRow = (e, id) => {
  const out = e.from === id, other = out ? e.to : e.from;
  return h("li", {}, out ? "→ " : "← ", goto(other), [e.label && " · " + e.label, e.protocol && " · " + e.protocol].filter(Boolean).join(""));
};
const section = (title, items) => (items.length ? [h("h3", {}, title), h("ul", {}, items)] : []);

function nodePanel(n) {
  const kids = [h("h2", {}, name(n.id)), h("div", {}, pillEls(n))];
  if (n.kind === "app") {
    if (!n.hasMeta) kids.push(h("p", { class: "note" }, "Known only from a collaborator declaration. The app has no [Meta] of its own, so these details come from the app that uses it."));
    if (n.description) kids.push(h("p", {}, n.description));
    kids.push(h("dl", {}, field("System", n.parent && name(n.parent)), field("Tech", n.tech), field("Owner", n.owner), field("Repository", n.repository), field("Tier", n.tier), field("Lifecycle", n.lifecycle)));
    const hidden = neighboursOf(n.id).length;
    kids.push(h("div", { class: "actions" },
      h("button", { type: "button", disabled: !hidden, onclick: () => expandNeighbours(n.id) }, hidden ? `Show connected apps (${hidden})` : "All connected apps shown"),
      n.parent && act("Hide", () => hideApp(n.id)),
      act("Centre", () => cy.animate({ center: { eles: cy.getElementById(n.id) }, duration: 250 }))));
    kids.push(section("Responsibilities", (n.responsibilities || []).map((r) => h("li", {}, r))));
    kids.push(section("Relationships", (touching.get(n.id) || []).map((e) => relRow(e, n.id))));
  } else {
    const members = appsOf.get(n.id) || [];
    if (n.description) kids.push(h("p", {}, n.description));
    if (!n.ownSystem) kids.push(h("p", { class: "muted" }, "A system outside the apps documented here, known from collaborators."));
    if (members.length) {
      const open = isOpen(n.id), all = members.every((a) => shown.has(a));
      kids.push(h("div", { class: "actions" }, !all && act(open ? "Show all apps" : "Open", () => openSystem(n.id)), open && act("Collapse", () => collapseSystem(n.id))));
      kids.push(section("Apps", members.map((a) => h("li", {}, goto(a), shown.has(a) ? "" : " (hidden)"))));
      const inside = new Set(members);
      const links = new Map();
      for (const a of members) for (const e of touching.get(a) || []) {
        if (inside.has(e.from) && inside.has(e.to)) continue;
        links.set(`${e.from}|${e.to}|${e.label}`, e);
      }
      kids.push(section("Connections", [...links.values()].map((e) => relRow(e, inside.has(e.from) ? e.from : e.to))));
    } else kids.push(section("Relationships", (touching.get(n.id) || []).map((e) => relRow(e, n.id))));
  }
  return kids;
}

function overviewPanel() {
  const apps = data.nodes.filter((n) => n.kind === "app"), systems = data.nodes.filter((n) => n.kind === "system");
  const undocumented = apps.filter((n) => !n.hasMeta);
  return [
    h("h2", {}, "Overview"),
    h("p", {}, `${systems.length} systems, ${apps.length} apps, ${data.edges.length} relationships.`),
    h("p", { class: "muted" }, "Select a node to see its details. Double-click a system to open it, or an app to show the apps it connects to. Search finds anything, hidden or not."),
    section("Systems", systems.map((n) => h("li", {}, goto(n.id), " ", pillEls(n)))),
    section("Undocumented apps", undocumented.map((n) => h("li", {}, goto(n.id), n.parent ? " · " + name(n.parent) : ""))),
  ];
}

function renderPanel() {
  let kids;
  if (selected && selected.kind === "node" && nodes.has(selected.id)) kids = nodePanel(nodes.get(selected.id));
  else if (selected && selected.kind === "edge" && edgeItems.has(selected.id)) {
    const items = edgeItems.get(selected.id), [f, t] = selected.id.split(">");
    kids = [h("h2", {}, `${name(f)} → ${name(t)}`), h("p", { class: "muted" }, items.length === 1 ? "1 relationship" : items.length + " relationships"),
      h("ul", {}, items.map((e) => h("li", {}, goto(e.from), " → ", goto(e.to), [e.label && " · " + e.label, e.protocol && " · " + e.protocol].filter(Boolean).join(""))))];
  } else kids = overviewPanel();
  panel.replaceChildren(...kids.flat(Infinity).filter(Boolean));
}

// ---------------------------------------------------------------- start
window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", () => cy.style(styleFor()));
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
