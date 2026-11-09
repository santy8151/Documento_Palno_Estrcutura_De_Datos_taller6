"use strict";

const SVG_NS = "http://www.w3.org/2000/svg";
const VIEW = { width: 1000, height: 680, padding: 36 };
const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const $ = (id) => document.getElementById(id);

const state = {
  data: null,
  positions: null,
  current: 0,
  playing: false,
  frame: 0,
  layers: {},
  rider: null,
};

// ---------------------------------------------------------------- helpers

function svg(name, attributes, parent) {
  const node = document.createElementNS(SVG_NS, name);
  for (const [key, value] of Object.entries(attributes || {})) {
    node.setAttribute(key, value);
  }
  if (parent) {
    parent.appendChild(node);
  }
  return node;
}

function html(tag, className, text) {
  const node = document.createElement(tag);
  if (className) {
    node.className = className;
  }
  if (text !== undefined) {
    node.textContent = text;
  }
  return node;
}

function kilometers(meters) {
  return (meters / 1000).toFixed(2);
}

function setStatus(message, isError) {
  const status = $("status");
  status.textContent = message;
  status.classList.toggle("error", Boolean(isError));
}

function currentPlace() {
  const { steps, start } = state.data;
  return state.current === 0 ? start : steps[state.current - 1].to;
}

// ---------------------------------------------------------------- loading

async function loadRoute(start) {
  stopPlayback();
  setStatus("Calculando la ruta…");

  try {
    const response = await fetch("/api/route" + (start ? "?start=" + encodeURIComponent(start) : ""));
    const data = await response.json();

    if (!response.ok) {
      setStatus(data.error, true);
      if (state.data) {
        $("startSelect").value = state.data.start;
      }
      return;
    }

    state.data = data;
    if (!state.positions) {
      state.positions = computeLayout(data.locations, data.streets);
    }

    fillStartSelect();
    renderSummary();
    buildMap();
    buildSteps();
    $("slider").max = data.steps.length;
    setStatus("");
    goTo(0);
  } catch (error) {
    setStatus("No se pudo contactar el servidor: " + error.message, true);
  }
}

function fillStartSelect() {
  const select = $("startSelect");
  if (select.options.length === 0) {
    for (const location of state.data.locations) {
      select.add(new Option(location, location));
    }
  }
  select.value = state.data.start;
}

function renderSummary() {
  const data = state.data;
  $("stats").textContent =
    `${data.packageCount} paquetes · ${data.locationCount} ubicaciones · ` +
    `${data.streetCount} calles (${data.oneWayCount} de un solo sentido) · ` +
    `calculada en ${(data.elapsedMilliseconds / 1000).toFixed(1)} s`;
  $("totalKm").textContent = kilometers(data.totalMeters);

  const saved = (1 - data.totalMeters / data.greedyMeters) * 100;
  $("comparison").textContent =
    `Vecino más cercano: ${kilometers(data.greedyMeters)} km. Esta ruta recorre ${saved.toFixed(1)} % menos.`;
}

// ---------------------------------------------------------------- map layout

/** Force-directed layout (Fruchterman-Reingold) with a fixed seed, so the map looks the same every time. */
function computeLayout(nodes, streets) {
  let seed = 11;
  const random = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
  const position = {};
  nodes.forEach((node) => {
    position[node] = { x: random() * VIEW.width, y: random() * VIEW.height };
  });

  const ideal = Math.sqrt((VIEW.width * VIEW.height) / nodes.length) * 0.75;
  const iterations = 500;

  for (let iteration = 0; iteration < iterations; iteration++) {
    const temperature = (1 - iteration / iterations) * (VIEW.width / 8) + 0.5;
    const push = {};
    nodes.forEach((node) => (push[node] = { x: 0, y: 0 }));

    for (let i = 0; i < nodes.length; i++) {
      for (let j = i + 1; j < nodes.length; j++) {
        const a = nodes[i];
        const b = nodes[j];
        const dx = position[a].x - position[b].x;
        const dy = position[a].y - position[b].y;
        const distance = Math.max(0.01, Math.hypot(dx, dy));
        const force = (ideal * ideal) / distance;
        push[a].x += (dx / distance) * force;
        push[a].y += (dy / distance) * force;
        push[b].x -= (dx / distance) * force;
        push[b].y -= (dy / distance) * force;
      }
    }

    for (const street of streets) {
      const dx = position[street.from].x - position[street.to].x;
      const dy = position[street.from].y - position[street.to].y;
      const distance = Math.max(0.01, Math.hypot(dx, dy));
      const force = ((distance * distance) / ideal) * Math.sqrt(250 / street.meters);
      push[street.from].x -= (dx / distance) * force;
      push[street.from].y -= (dy / distance) * force;
      push[street.to].x += (dx / distance) * force;
      push[street.to].y += (dy / distance) * force;
    }

    for (const node of nodes) {
      const length = Math.max(0.01, Math.hypot(push[node].x, push[node].y));
      const move = Math.min(length, temperature);
      position[node].x += (push[node].x / length) * move;
      position[node].y += (push[node].y / length) * move;
    }
  }

  const xs = nodes.map((node) => position[node].x);
  const ys = nodes.map((node) => position[node].y);
  const minX = Math.min(...xs);
  const minY = Math.min(...ys);
  const spanX = Math.max(...xs) - minX || 1;
  const spanY = Math.max(...ys) - minY || 1;

  nodes.forEach((node) => {
    position[node].x = VIEW.padding + ((position[node].x - minX) / spanX) * (VIEW.width - 2 * VIEW.padding);
    position[node].y = VIEW.padding + ((position[node].y - minY) / spanY) * (VIEW.height - 2 * VIEW.padding);
  });

  return position;
}

// ---------------------------------------------------------------- map drawing

function buildMap() {
  const { streets, packages, locations, start } = state.data;
  const position = state.positions;
  const root = $("map");
  root.replaceChildren();
  root.setAttribute("viewBox", `0 0 ${VIEW.width} ${VIEW.height}`);

  const defs = svg("defs", {}, root);
  const marker = svg("marker", {
    id: "arrow", viewBox: "0 0 10 10", refX: 9, refY: 5, markerWidth: 6, markerHeight: 6, orient: "auto",
  }, defs);
  svg("path", { d: "M0,0 L10,5 L0,10 z", class: "arrow-head" }, marker);

  for (const name of ["streets", "route", "nodes", "rider"]) {
    state.layers[name] = svg("g", {}, root);
  }

  for (const street of streets) {
    const a = position[street.from];
    const b = position[street.to];
    const attributes = { x1: a.x, y1: a.y, x2: b.x, y2: b.y, class: "street" };

    if (street.oneWay) {
      // Stop the line before the node circle so the arrow head stays visible.
      const length = Math.max(0.01, Math.hypot(b.x - a.x, b.y - a.y));
      attributes.x2 = b.x - ((b.x - a.x) / length) * 14;
      attributes.y2 = b.y - ((b.y - a.y) / length) * 14;
      attributes["marker-end"] = "url(#arrow)";
    }
    svg("line", attributes, state.layers.streets);
  }

  const tasks = {};
  const pickups = new Set();
  const deliveries = new Set();
  for (const item of packages) {
    pickups.add(item.pickup);
    deliveries.add(item.delivery);
    (tasks[item.pickup] = tasks[item.pickup] || []).push(`recoger ${item.id}`);
    (tasks[item.delivery] = tasks[item.delivery] || []).push(`entregar ${item.id}`);
  }

  for (const location of locations) {
    const { x, y } = position[location];
    const group = svg("g", { class: "node" }, state.layers.nodes);
    const isPickup = pickups.has(location);
    const isDelivery = deliveries.has(location);

    if (isPickup && isDelivery) {
      svg("circle", { cx: x, cy: y, r: 15, class: "halo" }, group);
    }

    let role = "plain";
    if (isDelivery) {
      role = "delivery";
    } else if (isPickup) {
      role = "pickup";
    }
    svg("circle", { cx: x, cy: y, r: 11, class: location === start ? "start" : role }, group);

    const label = svg("text", { x, y, class: location === start ? "start-label" : "" }, group);
    label.textContent = location;

    const summary = (tasks[location] || []).join(", ");
    svg("title", {}, group).textContent = summary ? `${location}: ${summary}` : location;
  }

  state.rider = svg("g", { class: "rider" }, state.layers.rider);
  svg("circle", { r: 15, class: "pulse" }, state.rider);
  svg("circle", { r: 7, class: "core" }, state.rider);
}

function drawRoute() {
  const layer = state.layers.route;
  layer.replaceChildren();

  state.data.steps.slice(0, state.current).forEach((step, index) => {
    if (step.path.length < 2) {
      return;
    }
    const points = step.path.map((node) => `${state.positions[node].x},${state.positions[node].y}`).join(" ");
    svg("polyline", { points, class: index === state.current - 1 ? "leg active" : "leg done" }, layer);
  });
}

function moveRider(point) {
  state.rider.setAttribute("transform", `translate(${point.x} ${point.y})`);
}

function animateRider(points, duration, onDone) {
  cancelAnimationFrame(state.frame);

  if (reduceMotion || points.length < 2) {
    moveRider(points[points.length - 1]);
    if (onDone) {
      onDone();
    }
    return;
  }

  const lengths = [];
  let total = 0;
  for (let i = 1; i < points.length; i++) {
    const length = Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y);
    lengths.push(length);
    total += length;
  }

  const startTime = performance.now();
  const frame = (now) => {
    const progress = Math.min(1, (now - startTime) / duration);
    let remaining = progress * total;
    let segment = 0;
    while (segment < lengths.length - 1 && remaining > lengths[segment]) {
      remaining -= lengths[segment];
      segment++;
    }

    const ratio = lengths[segment] === 0 ? 1 : remaining / lengths[segment];
    moveRider({
      x: points[segment].x + (points[segment + 1].x - points[segment].x) * ratio,
      y: points[segment].y + (points[segment + 1].y - points[segment].y) * ratio,
    });

    if (progress < 1) {
      state.frame = requestAnimationFrame(frame);
    } else if (onDone) {
      onDone();
    }
  };
  state.frame = requestAnimationFrame(frame);
}

// ---------------------------------------------------------------- step list

function buildSteps() {
  const list = $("steps");
  list.replaceChildren();

  state.data.steps.forEach((step, index) => {
    const item = html("li");
    const button = html("button", "step");
    button.type = "button";
    button.addEventListener("click", () => {
      stopPlayback();
      goTo(index + 1);
    });

    button.append(html("span", "order", step.order));
    button.append(html("span", "where", step.travels ? `${step.from} → ${step.to}` : `En ${step.to}`));
    button.append(html("span", "meters", step.travels ? `${step.meters} m` : ""));

    const chips = html("span", "chips");
    const deliveries = step.actions.filter((action) => action.kind === "Delivery").map((action) => action.packageId);
    const pickups = step.actions.filter((action) => action.kind === "Pickup").map((action) => action.packageId);
    if (deliveries.length > 0) {
      chips.append(html("span", "chip delivery", `entregar ${deliveries.join(", ")}`));
    }
    if (pickups.length > 0) {
      chips.append(html("span", "chip pickup", `recoger ${pickups.join(", ")}`));
    }
    button.append(chips);

    if (step.travels) {
      button.append(html("span", "path", step.path.join(" → ")));
    }

    item.append(button);
    list.append(item);
  });
}

function highlightStep() {
  const buttons = $("steps").querySelectorAll(".step");
  buttons.forEach((button, index) => button.classList.toggle("active", index === state.current - 1));

  if (state.current > 0) {
    buttons[state.current - 1].scrollIntoView({ block: "nearest" });
  }
}

function updateProgress() {
  const { steps, start, totalMeters } = state.data;
  const covered = steps.slice(0, state.current).reduce((sum, step) => sum + step.meters, 0);
  $("progress").textContent = state.current === 0
    ? `Salida en ${start}`
    : `Parada ${state.current} de ${steps.length} · ${kilometers(covered)} de ${kilometers(totalMeters)} km`;
}

// ---------------------------------------------------------------- navigation and playback

function speed() {
  return Number($("speed").value);
}

function goTo(index, animate, onDone) {
  const steps = state.data.steps;
  state.current = Math.max(0, Math.min(steps.length, index));
  $("slider").value = state.current;
  highlightStep();
  drawRoute();
  updateProgress();

  if (animate && state.current > 0) {
    const step = steps[state.current - 1];
    const points = step.path.map((node) => state.positions[node]);
    const duration = Math.min(1800, Math.max(500, (step.meters * 2) / speed()));
    animateRider(points, duration, onDone);
  } else {
    cancelAnimationFrame(state.frame);
    moveRider(state.positions[currentPlace()]);
    if (onDone) {
      onDone();
    }
  }
}

function tick() {
  if (!state.playing) {
    return;
  }
  if (state.current >= state.data.steps.length) {
    stopPlayback();
    return;
  }
  goTo(state.current + 1, true, () => setTimeout(tick, 250 / speed()));
}

function startPlayback() {
  if (state.current >= state.data.steps.length) {
    goTo(0);
  }
  state.playing = true;
  $("play").textContent = "Pausar";
  tick();
}

function stopPlayback() {
  state.playing = false;
  $("play").textContent = "Reproducir";
  cancelAnimationFrame(state.frame);
  if (state.data) {
    moveRider(state.positions[currentPlace()]);
  }
}

function step(delta) {
  stopPlayback();
  goTo(state.current + delta);
}

// ---------------------------------------------------------------- start

document.addEventListener("DOMContentLoaded", () => {
  $("play").addEventListener("click", () => (state.playing ? stopPlayback() : startPlayback()));
  $("prev").addEventListener("click", () => step(-1));
  $("next").addEventListener("click", () => step(1));
  $("slider").addEventListener("input", (event) => {
    stopPlayback();
    goTo(Number(event.target.value));
  });
  $("startSelect").addEventListener("change", (event) => loadRoute(event.target.value));

  document.addEventListener("keydown", (event) => {
    if (!state.data || event.target.tagName === "SELECT") {
      return;
    }
    if (event.key === "ArrowRight") {
      step(1);
    } else if (event.key === "ArrowLeft") {
      step(-1);
    }
  });

  loadRoute(null);
});
