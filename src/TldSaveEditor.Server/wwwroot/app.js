"use strict";

const $ = (id) => document.getElementById(id);
const statusEl = $("status");

function setStatus(msg, kind) {
  statusEl.textContent = msg || "";
  statusEl.className = "status" + (kind ? " " + kind : "");
}

async function api(method, url, body) {
  const opts = { method, headers: {} };
  if (body !== undefined) {
    opts.headers["Content-Type"] = "application/json";
    opts.body = JSON.stringify(body);
  }
  const res = await fetch(url, opts);
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error((data && data.error) || res.statusText);
  return data;
}

function esc(s) {
  return String(s ?? "").replace(/[&<>]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;" }[c]));
}

let hasProfile = false;
let mapState = null;

// ---- Save list -----------------------------------------------------------

async function loadFolder() {
  const r = await api("GET", "/api/saves-folder");
  $("folder").value = r.folder || "";
}

async function rescan() {
  await api("POST", "/api/saves-folder", { folder: $("folder").value.trim() });
  await refreshSaves();
}

async function refreshSaves() {
  const list = $("saveList");
  list.innerHTML = "";
  let saves = [];
  try { saves = await api("GET", "/api/saves"); }
  catch (e) { setStatus(e.message, "error"); }
  if (!saves.length) {
    const li = document.createElement("li");
    li.className = "empty";
    li.textContent = "No save files found in this folder.";
    list.appendChild(li);
    return;
  }
  for (const s of saves) {
    const li = document.createElement("li");
    li.textContent = s.name;
    li.title = s.path;
    li.onclick = () => loadSave(s.path, li);
    list.appendChild(li);
  }
}

async function loadSave(path, li) {
  try {
    setStatus("Loading…");
    const info = await api("POST", "/api/load", { path });
    document.querySelectorAll(".save-list li").forEach((x) => x.classList.remove("active"));
    if (li) li.classList.add("active");
    await applyLoaded(info);
    setStatus("Loaded", "ok");
  } catch (e) {
    setStatus(e.message, "error");
  }
}

// Populates every tab from a LoadedSaveDto. Shared by initial load and backup restore.
async function applyLoaded(info) {
  hasProfile = info.hasProfile;
  $("loadedName").textContent = info.displayName + (info.region ? "  ·  " + info.region : "");
  fillRegions(info.availableRegions, info.region);
  fillSelect("expMode", info.availableModes);
  $("editor").classList.remove("hidden");
  await Promise.all([loadPlayer(), loadItems(), loadInventory(), loadSkills(),
    loadAfflictions(), loadMap(), loadProfile(), loadBackups()]);
}

async function loadBackups() {
  const list = await api("GET", "/api/backups");
  const sel = $("backupSelect"), btn = $("restoreBtn");
  sel.innerHTML = "";
  if (!list.length) {
    const o = document.createElement("option");
    o.value = ""; o.textContent = "No backups";
    sel.appendChild(o); sel.disabled = true; btn.disabled = true;
    return;
  }
  sel.disabled = false; btn.disabled = false;
  for (const b of list) {
    const o = document.createElement("option");
    o.value = b.path; o.textContent = b.label;
    sel.appendChild(o);
  }
}

async function restoreBackup() {
  const bp = $("backupSelect").value;
  if (!bp) return;
  if (!confirm("Restore this backup? The current save file will be overwritten.\n(A snapshot of the current state is kept, so this is reversible.)"))
    return;
  try {
    setStatus("Restoring…");
    const info = await api("POST", "/api/restore", { backupPath: bp });
    await applyLoaded(info);
    setStatus("Restored from backup", "ok");
  } catch (e) { setStatus(e.message, "error"); }
}

// ---- Player --------------------------------------------------------------

function fillRegions(regions, current) { fillSelect("region", regions, current); }

function fillSelect(id, values, current) {
  const sel = $(id);
  sel.innerHTML = "";
  for (const v of values || []) {
    const o = document.createElement("option");
    o.value = v; o.textContent = v;
    if (v === current) o.selected = true;
    sel.appendChild(o);
  }
}

function bindSlider(id) {
  const inp = $(id), out = $(id + "Out");
  const upd = () => (out.value = inp.value);
  inp.oninput = upd;
  return upd;
}
const sliders = ["health", "thirst", "fatigue", "freezing"].map(bindSlider);

async function loadPlayer() {
  const p = await api("GET", "/api/player");
  $("health").value = Math.round(p.health);
  $("thirst").value = Math.round(p.thirst);
  $("fatigue").value = Math.round(p.fatigue);
  $("freezing").value = Math.round(p.freezing);
  $("calories").value = Math.round(p.calories);
  $("posX").value = p.positionX;
  $("posY").value = p.positionY;
  $("posZ").value = p.positionZ;
  $("neverDie").checked = p.neverDie;
  $("invulnerable").checked = p.invulnerable;
  $("infiniteCarry").checked = p.infiniteCarry;
  $("carryWeight").value = round2(p.carryWeightLimit);
  if (p.experienceMode) $("expMode").value = p.experienceMode;
  $("customMode").value = p.customMode || "";
  sliders.forEach((f) => f());
}

function readPlayer() {
  return {
    health: +$("health").value, thirst: +$("thirst").value, fatigue: +$("fatigue").value,
    freezing: +$("freezing").value, calories: +$("calories").value,
    positionX: +$("posX").value, positionY: +$("posY").value, positionZ: +$("posZ").value,
    neverDie: $("neverDie").checked, invulnerable: $("invulnerable").checked,
    infiniteCarry: $("infiniteCarry").checked, carryWeightLimit: +$("carryWeight").value,
    region: $("region").value,
    experienceMode: $("expMode").value, customMode: $("customMode").value,
  };
}

// ---- Skills --------------------------------------------------------------

const SKILLS = [
  ["firestarting", "Firestarting"], ["carcassHarvesting", "Carcass Harvesting"],
  ["cooking", "Cooking"], ["iceFishing", "Ice Fishing"], ["rifle", "Rifle"],
  ["archery", "Archery"], ["clothingRepair", "Clothing Repair"],
  ["revolver", "Revolver"], ["gunsmith", "Gunsmithing"],
];

function buildNumberGrid(gridId, fields, prefix) {
  const grid = $(gridId);
  grid.innerHTML = "";
  for (const [key, label] of fields) {
    const lab = document.createElement("label");
    lab.textContent = label;
    const inp = document.createElement("input");
    inp.type = "number"; inp.step = "any"; inp.id = prefix + "_" + key;
    grid.appendChild(lab); grid.appendChild(inp);
  }
}

async function loadSkills() {
  buildNumberGrid("skillsGrid", SKILLS, "sk");
  const s = await api("GET", "/api/skills");
  for (const [key] of SKILLS) $("sk_" + key).value = s[key];
}

function readSkills() {
  const out = {};
  for (const [key] of SKILLS) out[key] = +$("sk_" + key).value;
  return out;
}

// ---- Afflictions ---------------------------------------------------------

async function loadAfflictions() {
  const list = await api("GET", "/api/afflictions");
  const body = $("afflBody");
  body.innerHTML = "";
  if (!list.length) {
    body.innerHTML = '<tr><td colspan="3" class="muted-note" style="padding:12px">No afflictions.</td></tr>';
    return;
  }
  for (const a of list) {
    const tr = document.createElement("tr");
    tr.innerHTML = `<td>${esc(a.displayName)}</td><td>${a.positive ? "Buff" : "Negative"}</td>`;
    const td = document.createElement("td");
    const btn = document.createElement("button");
    btn.className = "link"; btn.textContent = a.positive ? "Remove" : "Cure";
    btn.onclick = () => removeAffliction(a.positive, a.index);
    td.appendChild(btn); tr.appendChild(td); body.appendChild(tr);
  }
}

async function removeAffliction(positive, index) {
  try { await api("POST", "/api/afflictions/remove", { positive, index }); await loadAfflictions(); }
  catch (e) { setStatus(e.message, "error"); }
}

async function cureAll() {
  try { await api("POST", "/api/afflictions/cure-all"); await loadAfflictions(); setStatus("All negative afflictions cured (Apply & Save to persist)", "ok"); }
  catch (e) { setStatus(e.message, "error"); }
}

// ---- Map -----------------------------------------------------------------

async function loadMap() {
  const m = await api("GET", "/api/map");
  mapState = m;
  const wrap = $("mapWrap"), img = $("mapImg"), info = $("mapInfo");
  if (!m.hasMap) {
    wrap.classList.add("hidden");
    info.textContent = m.region
      ? `No map image available for region "${m.region}". Position: ${fmt(m.playerX)}, ${fmt(m.playerY)}`
      : "No region loaded.";
    return;
  }
  wrap.classList.remove("hidden");
  info.textContent = `${m.region} — click the map to move the player. Position: ${fmt(m.playerX)}, ${fmt(m.playerY)}`;
  img.onload = positionMarker;
  img.src = "/maps/" + m.image;
  if (img.complete) positionMarker();
}

function fmt(n) { return Math.round(n * 100) / 100; }

function positionMarker() {
  const marker = $("mapMarker");
  if (!mapState || !mapState.hasMap) { marker.classList.add("hidden"); return; }
  const m = mapState;
  const lx = m.playerX * m.pixelsPerCoordinate + m.origoX;
  const ly = m.playerY * -m.pixelsPerCoordinate + m.origoY;
  marker.style.left = (lx / m.width * 100) + "%";
  marker.style.top = (ly / m.height * 100) + "%";
  marker.title = `Player: ${fmt(m.playerX)}, ${fmt(m.playerY)}`;
  marker.classList.remove("hidden");
}

async function mapClick(e) {
  if (!mapState || !mapState.hasMap) return;
  const img = $("mapImg");
  const rect = img.getBoundingClientRect();
  const fracX = (e.clientX - rect.left) / rect.width;
  const fracY = (e.clientY - rect.top) / rect.height;
  const lx = fracX * mapState.width, ly = fracY * mapState.height;
  const x = (lx - mapState.origoX) / mapState.pixelsPerCoordinate;
  const y = (ly - mapState.origoY) / -mapState.pixelsPerCoordinate;
  try {
    await api("POST", "/api/map/position", { x, y });
    await Promise.all([loadMap(), loadPlayer()]);
    setStatus("Position set (Apply & Save to persist)", "ok");
  } catch (err) { setStatus(err.message, "error"); }
}

// ---- Inventory -----------------------------------------------------------

async function loadItems() {
  const items = await api("GET", "/api/items");
  const sel = $("addItem");
  sel.innerHTML = "";
  let group = null, optgroup = null;
  for (const it of items) {
    if (it.category !== group) {
      group = it.category;
      optgroup = document.createElement("optgroup");
      optgroup.label = group;
      sel.appendChild(optgroup);
    }
    const o = document.createElement("option");
    o.value = it.prefabName; o.textContent = it.displayName;
    optgroup.appendChild(o);
  }
}

async function loadInventory() {
  const items = await api("GET", "/api/inventory");
  const body = $("invBody");
  body.innerHTML = "";
  $("invCount").textContent = `${items.length} item${items.length === 1 ? "" : "s"}`;
  for (const it of items) {
    const tr = document.createElement("tr");
    tr.appendChild(cell(esc(it.displayName)));
    tr.appendChild(cell(esc(it.category)));

    // Condition (% editable)
    const condInput = numInput(Math.round((it.condition || 0) * 100), 0, 100, 1, "70px");
    tr.appendChild(wrapTd(condInput));

    // Qty / Liters / In-clip: each shown only when the item has that attribute.
    const qtyInput = optionalNum(tr, it.quantity, 0, null, 1, "70px");
    const litInput = optionalNum(tr, it.liters, 0, null, 0.1, "80px", round2);
    const rndInput = optionalNum(tr, it.rounds, 0, null, 1, "70px");

    const save = () => updateItem(it.instanceId, (+condInput.value) / 100,
      qtyInput ? +qtyInput.value : null,
      litInput ? +litInput.value : null,
      rndInput ? +rndInput.value : null);
    [condInput, qtyInput, litInput, rndInput].forEach((i) => { if (i) i.onchange = save; });

    const td = document.createElement("td");
    const btn = document.createElement("button");
    btn.className = "link"; btn.textContent = "Remove";
    btn.onclick = () => removeItem(it.instanceId);
    td.appendChild(btn); tr.appendChild(td);
    body.appendChild(tr);
  }
}

function wrapTd(el) { const td = document.createElement("td"); td.appendChild(el); return td; }

// Appends a numeric-input cell when `value` is present, otherwise an "—" cell. Returns the input (or null).
function optionalNum(tr, value, min, max, step, width, fmt) {
  if (value === null || value === undefined) { tr.appendChild(cell("—")); return null; }
  const inp = numInput(fmt ? fmt(value) : value, min, max, step, width);
  tr.appendChild(wrapTd(inp));
  return inp;
}

function cell(html) { const td = document.createElement("td"); td.innerHTML = html; return td; }
function round2(n) { return Math.round(n * 100) / 100; }
function numInput(value, min, max, step, width) {
  const i = document.createElement("input");
  i.type = "number"; i.value = value; i.step = step;
  if (min !== null) i.min = min;
  if (max !== null) i.max = max;
  i.style.width = width;
  return i;
}

async function updateItem(instanceId, condition, quantity, liters, rounds) {
  try {
    await api("PUT", "/api/inventory/" + instanceId, { condition, quantity, liters, rounds });
    setStatus("Item updated (Apply & Save to persist)", "ok");
  } catch (e) { setStatus(e.message, "error"); }
}

async function addItem() {
  const prefabName = $("addItem").value;
  if (!prefabName) return;
  try { await api("POST", "/api/inventory/add", { prefabName }); await loadInventory(); setStatus("Item added (Apply & Save to persist)", "ok"); }
  catch (e) { setStatus(e.message, "error"); }
}

async function removeItem(instanceId) {
  try { await api("DELETE", "/api/inventory/" + instanceId); await loadInventory(); }
  catch (e) { setStatus(e.message, "error"); }
}

// ---- Profile (feats / badges) -------------------------------------------

const FEATS = [
  ["bookSmartsHoursResearch", "Book Smarts — hours research"],
  ["coldFusionElapsedDays", "Cold Fusion — days"],
  ["efficientMachineElapsedHours", "Efficient Machine — hours"],
  ["fireMasterFiresStarted", "Fire Master — fires started"],
  ["freeRunnerKilometers", "Free Runner — km"],
  ["snowWalkerKilometers", "Snow Walker — km"],
  ["expertTrapperRabbitsSnared", "Expert Trapper — rabbits snared"],
  ["straightToHeartItemsConsumed", "Straight to the Heart — items consumed"],
  ["blizzardWalkerHoursOutside", "Blizzard Walker — hours outside"],
];

async function loadProfile() {
  const note = $("profileNote"), grid = $("profileGrid");
  if (!hasProfile) {
    note.classList.remove("hidden");
    grid.innerHTML = "";
    return;
  }
  note.classList.add("hidden");
  buildNumberGrid("profileGrid", FEATS, "ft");
  // badges
  for (const [key, label] of [["badge4DON", "4DON badge complete"], ["badge4DON2019", "4DON 2019 badge complete"]]) {
    const lab = document.createElement("label"); lab.textContent = label;
    const wrap = document.createElement("div");
    const cb = document.createElement("input"); cb.type = "checkbox"; cb.id = "ft_" + key;
    wrap.appendChild(cb); grid.appendChild(lab); grid.appendChild(wrap);
  }
  const p = await api("GET", "/api/profile");
  for (const [key] of FEATS) $("ft_" + key).value = p[key];
  $("ft_badge4DON").checked = p.badge4DON;
  $("ft_badge4DON2019").checked = p.badge4DON2019;
}

function readProfile() {
  const out = {};
  for (const [key] of FEATS) out[key] = +$("ft_" + key).value;
  out.badge4DON = $("ft_badge4DON").checked;
  out.badge4DON2019 = $("ft_badge4DON2019").checked;
  return out;
}

// ---- Save ----------------------------------------------------------------

async function saveAll() {
  try {
    setStatus("Saving…");
    await api("PUT", "/api/player", readPlayer());
    await api("PUT", "/api/skills", readSkills());
    if (hasProfile) await api("PUT", "/api/profile", readProfile());
    await api("POST", "/api/save");
    await loadBackups();
    setStatus("Saved (a backup of the original was created)", "ok");
  } catch (e) { setStatus(e.message, "error"); }
}

// ---- Tabs & init ---------------------------------------------------------

document.querySelectorAll(".tab").forEach((t) => {
  t.onclick = () => {
    document.querySelectorAll(".tab").forEach((x) => x.classList.remove("active"));
    t.classList.add("active");
    document.querySelectorAll(".tab-panel").forEach((p) => p.classList.add("hidden"));
    $("tab-" + t.dataset.tab).classList.remove("hidden");
    if (t.dataset.tab === "map") positionMarker();
  };
});

$("rescan").onclick = rescan;
$("addBtn").onclick = addItem;
$("cureAllBtn").onclick = cureAll;
$("saveBtn").onclick = saveAll;
$("restoreBtn").onclick = restoreBackup;
$("mapImg").addEventListener("click", mapClick);

(async function init() {
  await loadFolder();
  await refreshSaves();
})();
