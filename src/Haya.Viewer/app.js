import cytoscape from "cytoscape";
import fcose from "cytoscape-fcose";
import expandCollapse from "cytoscape-expand-collapse";

cytoscape.use(fcose);
expandCollapse(cytoscape);

// Data is embedded as JSON by the generator. Only textContent is used for anything derived from it.
const data = JSON.parse(document.getElementById("haya-data").textContent);
document.getElementById("title").textContent = document.title;

const elements = [
  ...data.nodes.map((n) => ({ data: { id: n.id, label: n.name || n.id, parent: n.parent } })),
  ...data.edges.map((e, i) => ({ data: { id: "e" + i, source: e.from, target: e.to, label: e.label || "" } })),
];

const cs = getComputedStyle(document.documentElement);
const cy = cytoscape({
  container: document.getElementById("graph"),
  elements,
  layout: { name: "fcose", animate: false },
  style: [
    { selector: "node", style: { label: "data(label)", "text-valign": "center", color: cs.getPropertyValue("--fg") } },
    { selector: ":parent", style: { "text-valign": "top", "background-opacity": 0.1, "border-width": 1 } },
    { selector: "edge", style: { label: "data(label)", "curve-style": "bezier", "target-arrow-shape": "triangle", "font-size": 10 } },
  ],
});
window.__haya = cy;
