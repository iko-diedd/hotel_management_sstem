const API_BASE = window.HK_API_BASE || '';
const STATUSES = ['Available', 'Occupied', 'Dirty', 'Cleaning', 'Out of Service'];
const ROLE_LABEL = { RoomAssistant: 'Room Assistant', Housekeeper: 'Housekeeper', Admin: 'Admin' };
const ROLE_HOME = { RoomAssistant: 'assistant.html', Housekeeper: 'housekeeper.html', Admin: 'admin.html' };
const ADMIN_PAGES = ['dashboard.html', 'audit.html'];
const $ = s => document.querySelector(s);
const esc = s => String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const cls = s => s.toLowerCase().replace(/ /g, '-');
const fmt = d => d ? new Date(d.endsWith?.('Z') || /[+-]\d\d:\d\d$/.test(d) ? d : d + 'Z').toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' }) : '—';
const msgRow = (cols, text, err) => `<tr><td colspan="${cols}" class="msg ${err ? 'error' : ''}">${esc(text)}</td></tr>`;
const label = s => s === 'OutOfService' ? 'Out of Service' : s;

const auth = {
  get token() { try { return sessionStorage.getItem('hk.token'); } catch { return null; } },
  get user() { try { return JSON.parse(sessionStorage.getItem('hk.user') || 'null'); } catch { return null; } },
  set(token, user) { try { sessionStorage.setItem('hk.token', token); sessionStorage.setItem('hk.user', JSON.stringify(user)); } catch { } },
  clear() { try { sessionStorage.removeItem('hk.token'); sessionStorage.removeItem('hk.user'); } catch { } }
};
const here = () => location.pathname.split('/').pop() || 'index.html';
function toLogin() { location.replace('login.html?next=' + encodeURIComponent(here())); }

function requireRole(...roles) {
  const u = auth.user;
  if (!auth.token || !u) { toLogin(); throw new Error('redirecting'); }
  if (u.role !== 'Admin' && !roles.includes(u.role)) { location.replace(ROLE_HOME[u.role] || 'login.html'); throw new Error('redirecting'); }
  return u;
}

async function api(path, opt = {}) {
  const headers = { 'Content-Type': 'application/json' };
  if (auth.token) headers.Authorization = 'Bearer ' + auth.token;
  let r;
  try { r = await fetch(API_BASE + '/api' + path, { ...opt, headers, body: opt.body ? JSON.stringify(opt.body) : undefined }); }
  catch { throw new Error('Cannot reach the server. Check your connection.'); }
  if (r.status === 401 && !opt.noRedirect) { auth.clear(); toLogin(); throw new Error('Your session has expired. Please sign in again.'); }
  if (!r.ok) {
    let m = ({ 403: 'Not permitted for your role.', 404: 'Not found.', 409: 'That conflicts with the current state. Reload and try again.', 429: 'Too many requests. Please wait a moment.' })[r.status] || 'Request failed.';
    try {
      const p = await r.json();
      const first = p.errors && Object.values(p.errors).flat()[0];
      m = p.detail || first || p.title || m;
    } catch { }
    const e = new Error(m); e.status = r.status; throw e;
  }
  return r.status === 204 ? null : r.json();
}

const normRoom = r => ({ ...r, status: label(r.status), availability: r.isAvailable ? 'Available' : 'Unavailable' });
const normNotice = n => ({ ...n, status: label(n.status) });

const ICONS = {
  building: '<rect x="5" y="3" width="14" height="18" rx="1"/><path d="M9 7h2M13 7h2M9 11h2M13 11h2M10 21v-4h4v4"/>',
  bed: '<path d="M3 18V7M3 14h18v4M21 14v-2a3 3 0 0 0-3-3h-7v5"/><circle cx="7" cy="11" r="1.5"/>',
  sparkles: '<path d="M12 3l1.8 4.7L18.5 9.5l-4.7 1.8L12 16l-1.8-4.7L5.5 9.5l4.7-1.8zM19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8z"/>',
  clipboard: '<rect x="6" y="4" width="12" height="17" rx="1"/><path d="M9 4h6v3H9zM9 12h6M9 16h4"/>',
  globe: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18"/>',
  user: '<circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/>',
  users: '<circle cx="9" cy="8" r="3.5"/><path d="M2 20a7 7 0 0 1 14 0M16 5a3.5 3.5 0 0 1 0 6.5M18 20a6 6 0 0 0-3-5.2"/>',
  checkCircle: '<circle cx="12" cy="12" r="9"/><path d="M8 12.5l3 3 5-6"/>',
  check: '<path d="M5 12.5l4.5 4.5L19 7"/>',
  droplet: '<path d="M12 3s6 6.2 6 10.5a6 6 0 0 1-12 0C6 9.2 12 3 12 3z"/>',
  ban: '<circle cx="12" cy="12" r="9"/><path d="M5.6 5.6l12.8 12.8"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>',
  key: '<circle cx="8" cy="15" r="4"/><path d="M11 12l9-9M16 7l3 3M14 9l2 2"/>',
  send: '<path d="M21 3L10 14M21 3l-7 18-4-7-7-4z"/>',
  play: '<path d="M7 4l12 8-12 8z"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  pencil: '<path d="M4 20l4-1 11-11-3-3L5 16zM14 6l3 3"/>',
  trash: '<path d="M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13M10 11v6M14 11v6"/>',
  undo: '<path d="M9 14L4 9l5-5M4 9h10a6 6 0 0 1 0 12h-3"/>',
  lock: '<rect x="5" y="11" width="14" height="9" rx="1"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/>',
  search: '<circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/>',
  sun: '<circle cx="12" cy="12" r="4"/><path d="M12 2v2M12 20v2M2 12h2M20 12h2M5 5l1.5 1.5M17.5 17.5L19 19M5 19l1.5-1.5M17.5 6.5L19 5"/>',
  moon: '<path d="M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5z"/>',
  logout: '<path d="M10 4H5v16h5M15 8l4 4-4 4M19 12H9"/>',
  login: '<path d="M14 4h5v16h-5M9 8l-4 4 4 4M5 12h10"/>'
};
const icon = n => `<svg class="i" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONS[n] || ''}</svg>`;
const STATUS_ICON = { Available: 'checkCircle', Occupied: 'user', Dirty: 'droplet', Cleaning: 'sparkles', 'Out of Service': 'ban', Unavailable: 'ban',
  Pending: 'clock', Acknowledged: 'check', Completed: 'checkCircle', Cancelled: 'ban', Active: 'checkCircle', Inactive: 'ban' };

const badge = s => `<span class="badge ${cls(s)}">${icon(STATUS_ICON[s])}${esc(s)}</span>`;

const btn = (text, ic, { data = {}, cls: c = '', tip = '' } = {}) =>
  `<button class="btn sm ${text ? '' : 'icon'} ${c}"${Object.entries(data).map(([k, v]) => ` data-${k}="${esc(v)}"`).join('')}${tip ? ` title="${esc(tip)}" aria-label="${esc(tip)}"` : ''}>${icon(ic)}${text ? esc(text) : ''}</button>`;

function toast(msg, err) {
  const t = document.createElement('div');
  t.className = 'toast' + (err ? ' err' : ''); t.textContent = msg; t.setAttribute('role', err ? 'alert' : 'status');
  $('#toast').append(t); setTimeout(() => t.remove(), err ? 6000 : 3500);
}

function confirmBox(msg, okText = 'Confirm') {
  return new Promise(res => {
    const d = $('#confirm'); $('#confirmMsg').textContent = msg; d.querySelector('button[value=ok]').textContent = okText; d.returnValue = '';
    d.onclose = () => res(d.returnValue === 'ok'); d.showModal();
  });
}

function askText(msg, { type = 'text', placeholder = '', max = 500 } = {}) {
  return new Promise(res => {
    const d = $('#ask'), i = $('#askIn'); $('#askMsg').textContent = msg; i.type = type; i.placeholder = placeholder; i.maxLength = max; i.value = ''; d.returnValue = '';
    d.onclose = () => res(d.returnValue === 'ok' ? i.value.trim() : null); d.showModal(); i.focus();
  });
}

const statTile = (l, n, ic, c) => `<div class="stat ${c}">${icon(ic)}<div><b>${n}</b><span>${l}</span></div></div>`;

function summary(rooms) {
  $('#summary').innerHTML = statTile('Total Rooms', rooms.length, 'bed', '') +
    STATUSES.map(s => statTile(s, rooms.filter(r => r.status === s).length, STATUS_ICON[s], cls(s))).join('');
}

function filtered(rooms) {
  const q = $('#q').value.trim(), s = $('#fs')?.value, a = $('#fa').value;
  return rooms.filter(r => r.roomNumber.includes(q) && (!s || r.status === s) && (!a || r.availability === a));
}

const NAV = [
  { href: 'index.html', text: 'Availability', ic: 'globe' },
  { href: 'assistant.html', text: 'Room Assistant', ic: 'bed', roles: ['RoomAssistant', 'Admin'] },
  { href: 'housekeeper.html', text: 'Housekeeping', ic: 'sparkles', roles: ['Housekeeper', 'Admin'] },
  { href: 'admin.html', text: 'Admin', ic: 'clipboard', roles: ['Admin'] },
  { href: 'dashboard.html', text: 'Dashboard', ic: 'building', roles: ['Admin'] },
  { href: 'audit.html', text: 'Audit log', ic: 'clock', roles: ['Admin'] }
];

function renderAppBar() {
  const u = auth.user, bar = $('#appbar');
  const links = NAV.filter(n => !n.roles || (u && n.roles.includes(u.role)));
  if (!u) links.push({ href: 'login.html', text: 'Staff sign in', ic: 'login' });
  bar.innerHTML = `<a class="skip" href="#main">Skip to content</a><div class="wrap">
    <a class="brand" href="index.html">${icon('building')}<span>Hotel Housekeeping</span></a>
    <nav class="nav" aria-label="Main">${links.map(n => `<a href="${n.href}"${n.href === here() ? ' aria-current="page"' : ''}>${icon(n.ic)}<span>${n.text}</span></a>`).join('')}</nav>
    <div class="bar-end">${u ? `<span class="who">${esc(u.name)} · ${esc(ROLE_LABEL[u.role] || u.role)}</span>` : ''}
    <button class="btn icon" id="theme" type="button"></button>
    ${u ? `<button class="btn" id="signout" type="button" title="Sign out" aria-label="Sign out">${icon('logout')}<span class="label-sm">Sign out</span></button>` : ''}</div></div>`;
  const t = $('#theme');
  const paint = () => { const dark = HKTheme.get() === 'dark', tip = dark ? 'Switch to light mode' : 'Switch to dark mode'; t.innerHTML = icon(dark ? 'sun' : 'moon'); t.title = tip; t.setAttribute('aria-label', tip); };
  t.onclick = () => { HKTheme.toggle(); paint(); };
  paint();
  if (u) $('#signout').onclick = () => { auth.clear(); location.href = 'login.html'; };
}

// table labels
function labelCells(table) {
  const heads = [...table.querySelectorAll('thead th')].map(h => h.textContent.trim());
  table.querySelectorAll('tbody tr').forEach(tr => [...tr.children].forEach((td, i) => { if (!td.classList.contains('msg')) td.dataset.label = heads[i] || ''; }));
}
function watchTables() {
  document.querySelectorAll('table.stack').forEach(t => {
    new MutationObserver(() => labelCells(t)).observe(t.tBodies[0], { childList: true }); labelCells(t);
  });
}

// shell setup
function initShell(redraw, opts = {}) {
  renderAppBar(); watchTables();
  document.body.insertAdjacentHTML('beforeend', `<div id="toast" aria-live="polite"></div>
    <dialog id="confirm"><form method="dialog"><p id="confirmMsg"></p><div class="row">
    <button class="btn" value="cancel">Cancel</button><button class="btn danger" value="ok">Confirm</button></div></form></dialog>
    <dialog id="ask"><form method="dialog"><label><span id="askMsg"></span><input id="askIn" required></label><div class="row">
    <button class="btn" value="cancel" formnovalidate>Cancel</button><button class="btn primary" value="ok">OK</button></div></form></dialog>`);
  const tb = $('#toolbar'); if (!tb || !redraw) return;
  tb.innerHTML = `<span class="search">${icon('search')}<input id="q" type="search" placeholder="Search room number" aria-label="Search room number"></span>
    ${opts.publicView ? '' : `<select id="fs" aria-label="Filter by status"><option value="">All statuses</option>${STATUSES.map(s => `<option>${s}</option>`).join('')}</select>`}
    <select id="fa" aria-label="Filter by availability"><option value="">Any availability</option><option>Available</option><option>Unavailable</option></select>`;
  tb.querySelectorAll('input,select').forEach(e => e.addEventListener('input', redraw));
}
