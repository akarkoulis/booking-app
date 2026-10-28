import test from 'node:test';
import assert from 'node:assert/strict';
import { renderError } from '../../src/BookingApp.Web/wwwroot/js/ui.js';

// Tests page handlers through their API boundary using a small DOM substitute, no browser/dependencies.
function node(tag = 'div') {
  return {
    tag, value: '', checked: false, disabled: false, hidden: false, textContent: '', children: [], listeners: {}, attributes: {}, dataset: {},
    classList: { add() { }, remove() { } },
    append(...children) { this.children.push(...children); }, replaceChildren(...children) { this.children = children; },
    addEventListener(name, action) { this.listeners[name] = action; },
    setAttribute(name, value) { this.attributes[name] = value; }, getAttribute(name) { return this.attributes[name]; },
    removeAttribute(name) { delete this.attributes[name]; }, focus() { }, matches() { return false; },
    insertAdjacentElement(position, feedback) {
      this.feedbacks ??= [];
      this.feedbacks.push(feedback);
    },
    querySelector() { return null; }, querySelectorAll() { return []; }, reset() { }, closest() { return null; }
  };
}

function dom(formId, names, navigation = false) {
  const nodes = new Map();
  const get = id => { if (!nodes.has(id)) nodes.set(id, node()); return nodes.get(id); };
  get('#admin-content').hidden = true;
  const form = get(formId);
  form.elements = Object.fromEntries(names.map(name => [name, Object.assign(node('input'), { name, id: name })]));
  const fieldset = formId === '#resource-form' ? get('#resource-fields') : formId === '#policy-form' ? get('#policy-fields') : null;
  for (const input of Object.values(form.elements)) {
    input.validity = {
      get badInput() { return Boolean(input.badInput && !input.disabled && !fieldset?.disabled); }
    };
  }
  const summary = node();
  form.querySelector = selector => selector === '[data-errors]' ? summary : null;
  get('main').querySelector = form.querySelector;
  form.querySelectorAll = selector => selector === '[name]' ? Object.values(form.elements) : [];
  return {
    get, form, document: {
      createElement: node,
      querySelector: selector => selector === '[data-navigation]' && !navigation ? null : get(selector), querySelectorAll: () => []
    }
  };
}

const admin = { id: 1, role: 'Admin', officeTimeZone: 'Europe/London' };

for (const identical of [true, false]) {
  test(`400 summary keeps distinct message order and per-field feedback: identical=${identical}`, () => {
    const tree = dom('#utilisation-form', ['from', 'to']);
    const original = globalThis.document;
    globalThis.document = tree.document;
    const fromMessage = 'The end date must be on or after the start date.';
    const toMessage = identical ? fromMessage : 'An end date is required.';
    const summary = tree.form.querySelector('[data-errors]');
    let focused = false;
    summary.focus = () => { focused = true; };
    for (const input of Object.values(tree.form.elements)) {
      input.classList.add = name => { input.invalidClass = name; };
      input.setAttribute('aria-describedby', `${input.id}-help`);
    }
    try {
      renderError({ status: 400, problem: { errors: { to: [toMessage], from: [fromMessage] } } }, tree.form);
      for (const [name, message] of [['from', fromMessage], ['to', toMessage]]) {
        const input = tree.form.elements[name];
        assert.equal(input.invalidClass, 'is-invalid');
        assert.equal(input.getAttribute('aria-invalid'), 'true');
        assert.equal(input.getAttribute('aria-describedby'), `${name}-help ${name}-error`);
        assert.equal(input.feedbacks.length, 1);
        assert.equal(input.feedbacks[0].id, `${name}-error`);
        assert.equal(input.feedbacks[0].textContent, message);
      }
      assert.equal(summary.hidden, false);
      assert.equal(focused, true);
      assert.deepEqual(summary.children[1].children.map(item => item.textContent),
        identical ? [fromMessage] : [toMessage, fromMessage]);
    } finally {
      globalThis.document = original;
    }
  });
}
const resourceNames = ['name', 'type', 'location', 'seats', 'quantity', 'opensAt', 'closesAt', 'minDurationMinutes', 'maxDurationMinutes', 'maxAdvanceDays', 'minNoticeMinutes', 'isActive'];
const resource = { id: 9, name: 'Test room', type: 'Room', location: null, seats: null, quantity: 1, opensAt: '08:00:00', closesAt: '18:00:00', minDurationMinutes: null, maxDurationMinutes: null, maxAdvanceDays: null, minNoticeMinutes: 0, isActive: true };
const json = (body, status = 200) => new Response(JSON.stringify(body), { status });
const submit = form => form.listeners.submit({ preventDefault() { } });
const find = (root, text) => root.textContent === text ? root : root.children.map(child => find(child, text)).find(Boolean);

let runId = 0;
async function run(name, tree, handler, action, session = admin) {
  const original = { document: globalThis.document, fetch: globalThis.fetch, window: globalThis.window };
  try {
    globalThis.document = tree.document;
    globalThis.window = { addEventListener() {}, location: { pathname: `/${name}.html`, replace(path) { tree.redirect = path; } } };
    globalThis.fetch = async (path, options) => path === '/api/me' ? (typeof session === 'function' ? session() : json(session)) : handler(path, options);
    await import(`../../src/BookingApp.Web/wwwroot/js/${name}.js?test=${runId++}`);
    await action();
  } finally { Object.assign(globalThis, original); }
}

test('Resources edit loads GET detail and saves every constraint with strict numbers, nulls and HH:mm:ss', async () => {
  const tree = dom('#resource-form', resourceNames);
  let saved, detail = false;
  await run('resources', tree, async (path, options) => {
    if (options.method === 'PUT') {
      saved = JSON.parse(options.body);
      if (saved.maxAdvanceDays === '') return json({ errors: { '$.maxAdvanceDays': ['A JSON number is required.'] } }, 400);
      return json({ ...resource, ...saved });
    }
    if (path === '/api/resources/9') { detail = true; return json(resource); }
    assert.equal(path, '/api/resources?includeInactive=true'); return json([resource]);
  }, async () => {
    assert.equal(tree.get('#admin-content').hidden, false);
    assert.equal(tree.get('#page-status').textContent, '');
    assert.equal(tree.get('#resources-body').children[0].children[4].textContent, '—');
    await find(tree.get('#resources-body'), 'Edit').listeners.click();
    assert.equal(detail, true);
    assert.equal(tree.form.elements.quantity.disabled, true);
    tree.form.elements.opensAt.value = '09:15'; tree.form.elements.closesAt.value = '17:45';
    await submit(tree.form);
    assert.deepEqual(Object.keys(saved).sort(), [...resourceNames].sort());
    assert.equal(saved.opensAt, '09:15:00'); assert.equal(saved.closesAt, '17:45:00');
    assert.equal(saved.quantity, 1); assert.equal(saved.minNoticeMinutes, 0);
    assert.equal(saved.maxAdvanceDays, null); assert.equal(saved.isActive, true);
    tree.form.elements.minNoticeMinutes.value = '30';
    await submit(tree.form);
    assert.equal(saved.minNoticeMinutes, 30);
  });
});

function userPasswordPage() {
  const tree = dom('#user-form', ['email', 'displayName', 'role', 'password', 'isActive']);
  const password = tree.form.elements.password;
  password.type = 'password';
  password.setAttribute('aria-describedby', 'password-help');
  const toggle = tree.get('#toggle-password');
  toggle.setAttribute('aria-pressed', 'false');
  const show = node();
  const hide = node();
  hide.hidden = true;
  toggle.querySelector = selector => selector === '[data-icon="show"]' ? show : hide;
  const group = node();
  group.append(password, toggle);
  password.closest = selector => selector === '.input-group' ? group : null;
  return { tree, password, toggle, show, hide, group };
}

const existingUser = { id: 2, email: 'other@example.com', displayName: 'Other', role: 'Employee', isActive: true };
const userHandler = async path => json(path === '/api/users' ? [existingUser] : existingUser);

function assertPasswordHidden(view) {
  assert.equal(view.password.type, 'password');
  assert.equal(view.toggle.getAttribute('aria-pressed'), 'false');
  assert.equal(view.show.hidden, false);
  assert.equal(view.hide.hidden, true);
}

test('Users create password toggles visibility and pressed state both ways', async () => {
  const view = userPasswordPage();
  await run('users', view.tree, userHandler, async () => {
    view.tree.get('#new-user').listeners.click();
    assert.equal(typeof view.toggle.listeners.click, 'function');
    view.toggle.listeners.click();
    assert.equal(view.password.type, 'text');
    assert.equal(view.toggle.getAttribute('aria-pressed'), 'true');
    assert.equal(view.show.hidden, true);
    assert.equal(view.hide.hidden, false);
    view.toggle.listeners.click();
    assertPasswordHidden(view);
  });
});

for (const action of ['opens', 'closes']) {
  test(`Users create password resets when the form ${action}`, async () => {
    const view = userPasswordPage();
    await run('users', view.tree, userHandler, async () => {
      view.tree.get('#new-user').listeners.click();
      view.password.type = 'text';
      view.toggle.setAttribute('aria-pressed', 'true');
      view.show.hidden = true;
      view.hide.hidden = false;
      view.tree.get(action === 'opens' ? '#new-user' : '#close-user').listeners.click();
      assertPasswordHidden(view);
      assert.equal(view.tree.get('#user-editor').hidden, action === 'closes');
    });
  });
}

test('Users edit hides the password and its toggle and sends no password', async () => {
  const view = userPasswordPage();
  let saved;
  await run('users', view.tree, async (path, options) => {
    if (options.method === 'PUT') saved = JSON.parse(options.body);
    return userHandler(path);
  }, async () => {
    await find(view.tree.get('#users-body'), 'Edit').listeners.click();
    assert.equal(view.tree.get('#password-group').hidden, true);
    await submit(view.tree.form);
    assert.equal(Object.hasOwn(saved, 'password'), false);
  });
});

test('Users 400 password feedback follows the input and toggle inside the group and retains help association', async () => {
  const view = userPasswordPage();
  await run('users', view.tree, async (path, options) => options.method === 'POST'
    ? json({ errors: { password: ['The password must be at least 8 characters.'] } }, 400) : userHandler(path), async () => {
      view.tree.get('#new-user').listeners.click();
      await submit(view.tree.form);
      assert.equal(view.group.children[0], view.password);
      assert.equal(view.group.children[1], view.toggle);
      assert.equal(view.group.children[2].textContent, 'The password must be at least 8 characters.');
      assert.equal(view.password.getAttribute('aria-describedby'), 'password-help password-error');
    });
});

test('Users self-edit preserves access and descriptions, and other-user/create forms remove those descriptions', async () => {
  const { tree } = userPasswordPage();
  const user = { id: 1, email: 'admin@example.com', displayName: 'Administrator', role: 'Admin', isActive: true };
  const otherUser = { id: 2, email: 'other@example.com', displayName: 'Other user', role: 'Employee', isActive: true };
  let saved;
  await run('users', tree, async (path, options) => {
    if (options.method === 'PUT') { saved = JSON.parse(options.body); return json({ ...user, ...saved }); }
    return json(path === '/api/users' ? [user, otherUser] : path === '/api/users/2' ? otherUser : user);
  }, async () => {
    assert.equal(tree.get('#page-status').textContent, '');
    await find(tree.get('#users-body'), 'Edit').listeners.click();
    assert.equal(tree.form.elements.role.disabled, true); assert.equal(tree.form.elements.isActive.disabled, true);
    for (const field of ['role', 'isActive']) {
      assert.equal(tree.form.elements[field].getAttribute('aria-describedby'), 'own-access-help');
    }
    tree.form.elements.displayName.value = 'Updated admin';
    await submit(tree.form);
    assert.deepEqual(saved, { displayName: 'Updated admin', role: 'Admin', isActive: true });
    const otherRow = tree.get('#users-body').children.find(row => find(row, 'Other user'));
    await find(otherRow, 'Edit').listeners.click();
    for (const field of ['role', 'isActive']) {
      assert.equal(tree.form.elements[field].getAttribute('aria-describedby'), undefined);
      assert.equal(tree.form.elements[field].disabled, false);
    }
    await find(tree.get('#users-body'), 'Edit').listeners.click();
    tree.get('#new-user').listeners.click();
    for (const field of ['role', 'isActive']) {
      assert.equal(tree.form.elements[field].getAttribute('aria-describedby'), undefined);
    }
  });
});

test('Policy always sends both values, converting an empty limit to null and a supplied value to a number', async () => {
  const tree = dom('#policy-form', ['maxActiveRoomBookingsPerUser', 'defaultMinNoticeMinutes']);
  let saved;
  await run('booking-policy', tree, async (path, options) => {
    assert.equal(path, '/api/booking-policy');
    if (options.method === 'PUT') {
      saved = JSON.parse(options.body);
      if (saved.defaultMinNoticeMinutes === '') return json({ errors: { '$.defaultMinNoticeMinutes': ['A JSON number is required.'] } }, 400);
    }
    return json(saved || { maxActiveRoomBookingsPerUser: 3, defaultMinNoticeMinutes: 60 });
  }, async () => {
    assert.equal(tree.get('#page-status').textContent, '');
    tree.form.elements.maxActiveRoomBookingsPerUser.value = ''; tree.form.elements.defaultMinNoticeMinutes.value = '30';
    await submit(tree.form);
    assert.deepEqual(saved, { maxActiveRoomBookingsPerUser: null, defaultMinNoticeMinutes: 30 });
  });
});

for (const page of ['resources', 'booking-policy']) {
  test(`${page} incomplete integer renders one field message and summary without fetching`, async () => {
    const resources = page === 'resources';
    const tree = dom(resources ? '#resource-form' : '#policy-form', resources ? resourceNames : ['maxActiveRoomBookingsPerUser', 'defaultMinNoticeMinutes']);
    let requests = 0;
    await run(page, tree, async path => {
      requests++;
      return json(resources ? [resource] : { maxActiveRoomBookingsPerUser: 3, defaultMinNoticeMinutes: 60 });
    }, async () => {
      if (resources) tree.get('#new-resource').listeners.click();
      const input = tree.form.elements[resources ? 'minNoticeMinutes' : 'defaultMinNoticeMinutes'];
      input.value = '';
      input.badInput = true;
      assert.equal(input.validity.badInput, true);
      const before = requests;
      await submit(tree.form);
      assert.equal(requests, before, 'Bad input must send no request');
      const feedback = Object.values(tree.form.elements).flatMap(field => field.feedbacks ?? []);
      assert.equal(feedback.length, 1);
      assert.equal(feedback[0].textContent, 'Enter a whole number.');
      assert.equal(input.getAttribute('aria-invalid'), 'true');
      const summary = tree.form.querySelector('[data-errors]');
      assert.equal(summary.hidden, false);
      assert.equal(summary.children[1].children.length, 1);
      assert.equal(summary.children[1].children[0].textContent, 'Enter a whole number.');
    });
  });
}

test('History sends independent raw office dates and resource/user filters without empty parameters', async () => {
  const tree = dom('#history-form', ['resourceId', 'userId', 'from', 'to']);
  let requested;
  await run('booking-history', tree, async path => {
    if (path === '/api/resources?includeInactive=true' || path === '/api/users') return json([]);
    requested = path; return json([]);
  }, async () => {
    tree.form.elements.resourceId.value = '9'; tree.form.elements.userId.value = '7'; tree.form.elements.to.value = '2026-10-27';
    await submit(tree.form);
    const query = new URL(requested, 'http://localhost').searchParams;
    assert.equal(query.get('resourceId'), '9'); assert.equal(query.get('userId'), '7');
    assert.equal(query.get('to'), '2026-10-27'); assert.equal(query.has('from'), false);
  });
});

for (const failure of [400, 'network', 500]) {
  test(`History failure ${failure} gives the appropriate status line`, async () => {
    const tree = dom('#history-form', ['resourceId', 'userId', 'from', 'to']);
    const message = 'The end date must be on or after the start date.';
    let fail = false;
    await run('booking-history', tree, async path => {
      if (!fail || !path.startsWith('/api/bookings')) return json([]);
      if (failure === 'network') throw new TypeError('offline');
      return json({ errors: { from: [message], to: [message] } }, failure);
    }, async () => {
      fail = true;
      tree.form.elements.from.value = '2026-11-05';
      tree.form.elements.to.value = '2026-11-01';
      await submit(tree.form);
      assert.equal(tree.get('#page-status').textContent, failure === 400
        ? 'Correct the problem below and search again.' : 'Booking history could not be loaded.');
      const summary = tree.form.querySelector('[data-errors]');
      assert.equal(summary.hidden, false);
      if (failure === 400) {
        assert.deepEqual(summary.children[1].children.map(item => item.textContent), [message]);
        for (const name of ['from', 'to']) {
          assert.equal(tree.form.elements[name].feedbacks[0].textContent, message);
        }
      }
    });
  });
}

test('Utilisation renders booked ratio as a percentage and flags inactive resources', async () => {
  const tree = dom('#utilisation-form', ['from', 'to', 'type']);
  await run('utilisation', tree, async path => {
    assert.match(path, /^\/api\/reports\/utilisation\?/);
    return json([{ name: 'Inactive room', type: 'Room', isActive: false, quantity: 3, bookingCount: 1, bookedHours: 1, availableHours: 70, bookedUtilisation: 0.0143 }]);
  }, async () => {
    assert.ok(find(tree.get('#utilisation-body'), '1.43%'));
    assert.ok(find(tree.get('#utilisation-body'), 'Inactive'));
    assert.equal(tree.get('#utilisation-body').children[0].children[3].textContent, 3);
    assert.equal(tree.get('#page-status').textContent, '1 resources in booked utilisation report.');
  });
});

for (const failure of [400, 'network', 500]) {
  test(`Utilisation report failure ${failure} gives the appropriate status line`, async () => {
    const tree = dom('#utilisation-form', ['from', 'to', 'type']);
    let fail = false;
    await run('utilisation', tree, async () => {
      if (!fail) return json([]);
      if (failure === 'network') throw new TypeError('offline');
      return json({ errors: { from: ['A start date is required.'] } }, failure);
    }, async () => {
      fail = true;
      await submit(tree.form);
      assert.equal(tree.get('#page-status').textContent, failure === 400
        ? 'Correct the problem below and run the report again.' : 'Report could not be loaded.');
    });
  });
}

const adminPages = [
  ['users', '#user-form', ['email', 'displayName', 'role', 'password', 'isActive'], '#users-body', 'Users'],
  ['resources', '#resource-form', resourceNames, '#resources-body', 'Resources'],
  ['booking-policy', '#policy-form', ['maxActiveRoomBookingsPerUser', 'defaultMinNoticeMinutes'], null, 'Policy'],
  ['utilisation', '#utilisation-form', ['from', 'to', 'type'], '#utilisation-body', 'Report'],
  ['booking-history', '#history-form', ['resourceId', 'userId', 'from', 'to'], '#history-body', 'Booking history']
];
const employee = { id: 2, email: 'alice@example.com', displayName: 'Alice', role: 'Employee', officeTimeZone: 'Europe/London' };
const employeeLinks = ['/book.html', '/my-bookings.html'];
const adminLinks = [...employeeLinks, '/users.html', '/resources.html', '/booking-policy.html', '/utilisation.html', '/booking-history.html'];
function adminPage(formId, names) {
  const tree = dom(formId, names, true);
  tree.get('#page-status').textContent = 'Loading...';
  const mainSummary = node();
  mainSummary.hidden = true;
  tree.get('main').querySelector = selector => selector === '[data-errors]' ? mainSummary : null;
  tree.get('#user-editor').hidden = true;
  tree.get('#resource-editor').hidden = true;
  tree.get('#toggle-password').querySelector = selector => tree.get(`toggle ${selector}`);
  return tree;
}
for (const [page, formId, names, bodyId, label] of adminPages) {
  test(`${page} Employee gets employee navigation and the access-denied summary without admin requests`, async () => {
    const tree = adminPage(formId, names);
    await run(page, tree, () => { assert.fail('Employee must not call an admin API'); }, async () => {
      const navigation = tree.get('[data-navigation]').children;
      assert.deepEqual(navigation.filter(item => item.tag === 'a').map(item => item.href), employeeLinks);
      assert.deepEqual(navigation.map(item => item.textContent), ['Book', 'My bookings', 'Alice', 'Log out']);
      const summary = tree.get('main').querySelector('[data-errors]');
      assert.equal(summary.hidden, false);
      assert.equal(summary.children[0].textContent, 'Please check the following:');
      assert.deepEqual(summary.children[1].children.map(item => item.textContent), ['You do not have access to this page or action.']);
      assert.equal(tree.get('#page-status').textContent, '');
      assert.equal(tree.get('#admin-content').hidden, true);
      assert.equal(tree.get('#user-editor').hidden, true);
      assert.equal(tree.get('#resource-editor').hidden, true);
      if (bodyId) assert.equal(tree.get(bodyId).children.length, 0);
      assert.ok(Object.values(tree.form.elements).every(input => input.value === ''));
    }, employee);
  });
  test(`${page} Admin gets full navigation and a loaded page`, async () => {
    const tree = adminPage(formId, names);
    await run(page, tree, async path => json(path === '/api/booking-policy'
      ? { maxActiveRoomBookingsPerUser: 3, defaultMinNoticeMinutes: 60 }
      : path === '/api/users' ? [{ ...admin, displayName: 'Administrator', email: 'admin@example.com', isActive: true }]
      : path.startsWith('/api/resources') ? [resource] : []), async () => {
      assert.deepEqual(tree.get('[data-navigation]').children.filter(item => item.tag === 'a').map(item => item.href), adminLinks);
      assert.equal(tree.get('#admin-content').hidden, false);
      assert.ok(!tree.get('#page-status').textContent.includes('could not be loaded'));
      if (page === 'booking-policy') assert.equal(tree.form.elements.defaultMinNoticeMinutes.value, 60);
      if (page === 'users') assert.ok(find(tree.get(bodyId), 'Administrator'));
      if (page === 'resources') assert.ok(find(tree.get(bodyId), 'Test room'));
      if (bodyId && ['utilisation', 'booking-history'].includes(page)) assert.ok(tree.get(bodyId).children.length > 0);
    });
  });
  for (const failure of ['network', 500, 401]) {
    test(`${page} page-load ${failure} preserves retry status or central login redirect`, async () => {
      const tree = adminPage(formId, names);
      await run(page, tree, () => { assert.fail('Session failure must stop page requests'); }, async () => {
        assert.equal(tree.get('#admin-content').hidden, true);
        if (failure === 401) assert.equal(tree.redirect, '/login.html');
        else assert.equal(tree.get('#page-status').textContent, `${label} could not be loaded. Reload the page to retry.`);
      }, () => {
        if (failure === 'network') throw new TypeError('offline');
        return json({ title: 'Session failed' }, failure);
      });
    });
  }
}
