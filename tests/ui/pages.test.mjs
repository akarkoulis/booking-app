import test from 'node:test';
import assert from 'node:assert/strict';

// Minimal DOM surface for exercising page requests and retry controls without a browser package.
function node() {
  return {
    value: '', disabled: false, hidden: false, children: [], dataset: {}, attributes: {}, listeners: {},
    classList: { add() { }, remove() { } },
    get childElementCount() { return this.children.length; },
    append(...items) { this.children.push(...items); },
    replaceChildren(...items) { this.children = items; },
    addEventListener(event, action) { this.listeners[event] = action; },
    setAttribute(key, value) { this.attributes[key] = value; },
    getAttribute(key) { return this.attributes[key]; },
    removeAttribute(key) { delete this.attributes[key]; },
    querySelectorAll() { return []; }, querySelector() { return null; },
    focus() { }, matches() { return false; }, reset() { }, closest() { return null; }
  };
}

function page() {
  const nodes = new Map();
  const get = selector => { if (!nodes.has(selector)) nodes.set(selector, node()); return nodes.get(selector); };
  get('main').querySelector = selector => get(`main ${selector}`);
  return {
    get,
    document: {
      querySelector: selector => selector === '[data-navigation]' ? null : get(selector),
      querySelectorAll: () => [], createElement: () => node()
    }
  };
}

const me = { id: 7, role: 'Employee', officeTimeZone: 'Europe/London' };
const json = (value, status = 200) => new Response(JSON.stringify(value), { status });

for (const session of [401, 200, 500, 'network']) {
  test(`Login password toggle runs after session path ${session}`, async () => {
    const original = { document: globalThis.document, fetch: globalThis.fetch, window: globalThis.window };
    const dom = page();
    const form = dom.get('#login-form');
    form.querySelector = selector => dom.get(`login ${selector}`);
    const password = dom.get('#password');
    password.type = 'password';
    const toggle = dom.get('#toggle-password');
    toggle.querySelector = selector => dom.get(`toggle ${selector}`);
    try {
      globalThis.document = dom.document;
      globalThis.window = { addEventListener() {}, location: { replace() {} } };
      globalThis.fetch = async () => {
        if (session === 'network') throw new TypeError('offline');
        return json(me, session);
      };
      await import(`../../src/BookingApp.Web/wwwroot/js/login.js?toggle-session=${session}`);
      toggle.listeners.click();
      assert.equal(password.type, 'text');
      assert.equal(toggle.getAttribute('aria-pressed'), 'true');
      assert.equal(dom.get('toggle [data-icon="show"]').hidden, true);
      assert.equal(dom.get('toggle [data-icon="hide"]').hidden, false);
      toggle.listeners.click();
      assert.equal(password.type, 'password');
      assert.equal(toggle.getAttribute('aria-pressed'), 'false');
      assert.equal(dom.get('toggle [data-icon="show"]').hidden, false);
      assert.equal(dom.get('toggle [data-icon="hide"]').hidden, true);
    } finally { Object.assign(globalThis, original); }
  });
}

function bookPage() {
  const dom = page();
  dom.get('#availability-form').elements = Object.fromEntries(
    ['type', 'quantity', 'minSeats', 'startDate', 'startTime', 'endDate', 'endTime'].map(name => [name, node()]));
  dom.get('#availability-form').elements.type.value = 'Room';
  dom.get('#booking-form').elements = { title: node() };
  dom.get('#booking-form').hidden = true;
  dom.get('#draft-choices').querySelectorAll = selector => selector === 'button'
    ? dom.get('#draft-choices').children.filter(child => child.type === 'button') : [];
  return dom;
}

const availableRow = {
  resourceId: 1, resourceName: 'Room A', resourceType: 'Room', location: null,
  seats: 4, quantity: 1, available: true, violations: [], start: '2026-10-27T09:00:00+00:00', end: '2026-10-27T10:00:00+00:00'
};

const completeDraft = {
  date: '2026-10-30', startTime: '09:00', endTime: '10:30', resourceType: 'Room',
  resourceName: null, location: null, minSeats: null, quantity: null, title: 'Review', notUnderstood: null,
  resource: { ...availableRow, start: '2026-10-30T09:00:00Z', end: '2026-10-30T10:30:00Z' }, alternatives: []
};

for (const scenario of ['initial', 'click', 'conflict-alternative', 'conflict-primary', 'unavailable']) {
  test(`Draft choice marks follow selection: ${scenario}`, async () => {
    const original = { fetch: globalThis.fetch, document: globalThis.document };
    const dom = bookPage();
    const draft = structuredClone(completeDraft);
    draft.alternatives = [{ ...draft.resource, resourceId: 2, resourceName: 'Other' }];
    if (scenario === 'unavailable') draft.resource.available = false;
    const assertMarks = expected => {
      const buttons = dom.get('#draft-choices').querySelectorAll('button');
      assert.deepEqual(buttons.map(button => [button.dataset.resourceId, button.getAttribute('aria-pressed'), button.className]), expected);
    };
    try {
      globalThis.document = dom.document;
      globalThis.fetch = async path => path === '/api/me' ? json(me)
        : path === '/api/booking-drafts' ? json(draft) : json({ status: 409 }, 409);
      await import(`../../src/BookingApp.Web/wwwroot/js/book.js?marks=${scenario}`);
      await dom.get('#draft-form').listeners.submit({ preventDefault() { } });
      if (scenario === 'unavailable') {
        assertMarks([['2', 'false', 'btn btn-outline-primary']]);
        assert.equal(dom.get('#draft-booking-form').hidden, true);
        return;
      }
      if (scenario === 'initial') {
        assertMarks([['1', 'true', 'btn btn-primary'], ['2', 'false', 'btn btn-outline-primary']]);
      }
      if (scenario === 'click' || scenario === 'conflict-alternative') {
        dom.get('#draft-choices').children[1].listeners.click();
        if (scenario === 'click') assertMarks([['1', 'false', 'btn btn-outline-primary'], ['2', 'true', 'btn btn-primary']]);
      }
      if (scenario.startsWith('conflict')) {
        await dom.get('#draft-booking-form').listeners.submit({ preventDefault() { } });
        assertMarks(scenario === 'conflict-alternative'
          ? [['1', 'false', 'btn btn-outline-primary']] : [['2', 'false', 'btn btn-outline-primary']]);
      }
    } finally {
      Object.assign(globalThis, original);
    }
  });
}

test('Manual submit clears a draft 422 alert and status while keeping the draft text', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = bookPage();
  const errors = node();
  dom.get('#draft-form').querySelector = selector => selector === '[data-errors]' ? errors : null;
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => path === '/api/me' ? json(me) : path === '/api/booking-drafts'
      ? json({ violations: [{ code: 'draft_not_understood', message: 'Try a room tomorrow.' }] }, 422) : json([availableRow]);
    await import('../../src/BookingApp.Web/wwwroot/js/book.js?manual-clears-draft-error');
    dom.get('#draft-text').value = 'Keep this request';
    await dom.get('#draft-form').listeners.submit({ preventDefault() { } });
    assert.equal(errors.hidden, false);
    assert.ok(dom.get('#draft-status').textContent);
    await dom.get('#availability-form').listeners.submit({ preventDefault() { } });
    assert.equal(errors.hidden, true);
    assert.equal(errors.children.length, 0);
    assert.equal(dom.get('#draft-status').textContent, '');
    assert.equal(dom.get('#draft-text').value, 'Keep this request');
  } finally {
    Object.assign(globalThis, original);
  }
});

test('Draft submit clears a manual 400 alert, results and confirmation', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = bookPage();
  const errors = node();
  dom.get('#availability-form').querySelector = selector => selector === '[data-errors]' ? errors : null;
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => path === '/api/me' ? json(me) : path === '/api/booking-drafts'
      ? json(completeDraft) : json({ errors: { minSeats: ['Minimum seats must be at least 1.'] } }, 400);
    await import('../../src/BookingApp.Web/wwwroot/js/book.js?draft-clears-manual-error');
    await dom.get('#availability-form').listeners.submit({ preventDefault() { } });
    assert.equal(errors.hidden, false);
    // Include previously rendered output so the cross-flow reset must clear every region.
    dom.get('#resources').append(node());
    dom.get('#confirmation').hidden = false;
    await dom.get('#draft-form').listeners.submit({ preventDefault() { } });
    assert.equal(errors.hidden, true);
    assert.equal(errors.children.length, 0);
    assert.equal(dom.get('#resources').children.length, 0);
    assert.equal(dom.get('#confirmation').hidden, true);
  } finally {
    Object.assign(globalThis, original);
  }
});

for (const failure of [400, 422, 500, 'network']) {
  test(`Draft status explains correction or failure: ${failure}`, async () => {
    const original = { fetch: globalThis.fetch, document: globalThis.document };
    const dom = bookPage();
    try {
      globalThis.document = dom.document;
      globalThis.fetch = async path => {
        if (path === '/api/me') return json(me);
        if (failure === 'network') throw new TypeError('Failed to fetch');
        return json({ violations: [{ code: 'draft_not_understood', message: 'Try a room tomorrow.' }] }, failure);
      };
      await import(`../../src/BookingApp.Web/wwwroot/js/book.js?draft-status=${failure}`);
      await dom.get('#draft-form').listeners.submit({ preventDefault() { } });
      assert.equal(dom.get('#draft-status').textContent, [400, 422].includes(failure)
        ? 'Correct the problem above and try again.' : 'Drafting could not be completed.');
    } finally {
      Object.assign(globalThis, original);
    }
  });
}

for (const kind of ['complete', 'partial', 'partial-no-explanation', 'unavailable', 'unavailable-with-alternative', 'no-name-match', 'mismatch', 'same-name', '422', '503']) {
  test(`Draft section handles ${kind} independently of the manual form`, async () => {
    const original = { fetch: globalThis.fetch, document: globalThis.document };
    const dom = bookPage();
    const errors = node();
    dom.get('#draft-form').querySelector = selector => selector === '[data-errors]' ? errors : null;
    const draft = structuredClone(completeDraft);
    if (kind === 'partial') Object.assign(draft, { date: null, resource: null, notUnderstood: 'Add a date.' });
    if (kind === 'partial-no-explanation') Object.assign(draft, { date: null, resource: null });
    if (kind.startsWith('unavailable')) draft.resource = { ...draft.resource, available: false, violations: [{ code: 'capacity_exceeded', message: 'This room is already booked.' }] };
    if (kind === 'unavailable-with-alternative') draft.alternatives = [{ ...completeDraft.resource, resourceId: 2, resourceName: 'Other' }];
    if (kind === 'no-name-match') Object.assign(draft, { resourceType: null, resourceName: 'Missing', resource: null });
    if (kind === 'mismatch') draft.resourceName = 'Unknown';
    if (kind === 'same-name') draft.resourceName = 'rOoM a';
    try {
      globalThis.document = dom.document;
      globalThis.fetch = async path => path === '/api/me' ? json(me) : ['422', '503'].includes(kind)
        ? json({ violations: [{ code: 'draft_not_understood', message: 'Try a room tomorrow.' }] }, Number(kind)) : json(draft);
      await import(`../../src/BookingApp.Web/wwwroot/js/book.js?draft=${kind}`);
      dom.get('#draft-text').value = 'A booking';
      await dom.get('#draft-form').listeners.submit({ preventDefault() { } });
      assert.equal(dom.get('#resources').children.length, 0);
      assert.equal(dom.get('#booking-form').hidden, true);
      if (kind === 'complete') {
        assert.equal(dom.get('#draft-confirm-line').textContent, 'Fri 30 Oct 2026, 09:00–10:30');
        assert.equal(dom.get('#draft-title').value, 'Review');
        assert.equal(dom.get('#draft-booking-form').hidden, false);
        assert.equal(dom.get('#draft-selected-resource').textContent, 'Room A · Quantity 1');
      }
      if (kind === 'partial') assert.equal(dom.get('#draft-status').textContent, 'Add a date.');
      if (kind === 'partial-no-explanation') assert.equal(dom.get('#draft-status').textContent, 'Add a date, times and what to book.');
      if (kind.startsWith('unavailable')) {
        assert.match(dom.get('#draft-status').textContent, /Room A isn't available:/);
        assert.equal(dom.get('#draft-booking-form').hidden, true);
        assert.equal(dom.get('#draft-choices').children[0].textContent, 'This room is already booked.');
        if (kind === 'unavailable-with-alternative') {
          dom.get('#draft-choices').children.at(-1).listeners.click();
          assert.equal(dom.get('#draft-booking-form').hidden, false);
          assert.equal(dom.get('#draft-selected-resource').textContent, 'Other · Quantity 1');
        }
      }
      if (kind === 'no-name-match') assert.equal(dom.get('#draft-status').textContent, 'No resource matches that name.');
      if (kind === 'mismatch') assert.equal(dom.get('#draft-status').textContent, 'No resource matches "Unknown"; suggesting Room A.');
      if (kind === 'same-name') assert.doesNotMatch(dom.get('#draft-status').textContent, /No resource matches/);
      if (kind === '422') assert.equal(errors.children[1].children[0].textContent, 'Try a room tomorrow.');
      if (kind === '503') assert.equal(dom.get('#draft-status').textContent, 'Drafting is unavailable; use the form below.');
    } finally {
      Object.assign(globalThis, original);
    }
  });
}

test('Draft alternative confirms through bookings, uses edited title, and removes a conflicting choice', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = bookPage();
  const errors = node();
  dom.get('#booking-draft-slot').querySelector = () => errors;
  let submitted;
  let attempts = 0;
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async (path, options) => {
      if (path === '/api/me') return json(me);
      if (path === '/api/booking-drafts') return json({ ...completeDraft, alternatives: [{ ...completeDraft.resource, resourceId: 2, resourceName: 'Other' }] });
      submitted = JSON.parse(options.body);
      attempts++;
      return attempts === 1 ? json({ status: 409 }, 409) : json({ ...completeDraft.resource, title: submitted.title }, 201);
    };
    await import('../../src/BookingApp.Web/wwwroot/js/book.js?draft-conflict');
    await dom.get('#draft-form').listeners.submit({ preventDefault() { } });
    const firstChoices = dom.get('#draft-choices').children;
    firstChoices.at(-1).listeners.click();
    dom.get('#draft-title').value = 'Edited';
    await dom.get('#draft-booking-form').listeners.submit({ preventDefault() { } });
    assert.equal(submitted.resourceId, 2);
    assert.equal(submitted.quantity, 1);
    assert.equal(submitted.title, 'Edited');
    assert.equal(dom.get('#draft-booking-form').hidden, true);
    assert.equal(dom.get('#draft-choices').children.length, 1);
    assert.equal(errors.children[1].children[0].textContent, 'That resource was just taken; choose another.');
    dom.get('#draft-choices').children[0].listeners.click();
    await dom.get('#draft-booking-form').listeners.submit({ preventDefault() { } });
    assert.equal(dom.get('#draft-confirmation').hidden, false);
    assert.equal(dom.get('#confirmation').children.length, 0);
  } finally {
    Object.assign(globalThis, original);
  }
});

test('Editing draft text invalidates selection and ignores an old request response', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = bookPage();
  let resolve;
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => path === '/api/me' ? json(me) : await new Promise(done => { resolve = done; });
    await import('../../src/BookingApp.Web/wwwroot/js/book.js?draft-stale');
    const pending = dom.get('#draft-form').listeners.submit({ preventDefault() { } });
    await Promise.resolve();
    dom.get('#draft-text').listeners.input();
    resolve(json(completeDraft));
    await pending;
    assert.equal(dom.get('#draft-booking-form').hidden, true);
    assert.equal(dom.get('#draft-choices').children.length, 0);
  } finally {
    Object.assign(globalThis, original);
  }
});

test('Draft Book button is enabled again after a failed booking attempt', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = bookPage();
  const book = node();
  dom.get('#booking-draft-slot').querySelectorAll = selector => selector === 'input, textarea, button' ? [book] : [];
  dom.get('#draft-booking-form').querySelectorAll = selector => selector === 'button[type="submit"]' ? [book] : [];
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => path === '/api/me' ? json(me) : path === '/api/booking-drafts' ? json(completeDraft) : json({}, 422);
    await import('../../src/BookingApp.Web/wwwroot/js/book.js?draft-retry-button');
    await dom.get('#draft-form').listeners.submit({ preventDefault() { } });
    await dom.get('#draft-booking-form').listeners.submit({ preventDefault() { } });
    assert.equal(book.disabled, false);
  } finally {
    Object.assign(globalThis, original);
  }
});

test('Draft submit resets the manual selection, and the in-flight guard blocks a second booking', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = bookPage();
  let resolve;
  let submissions = 0;
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => {
      if (path === '/api/me') return json(me);
      if (path === '/api/booking-drafts') return json(completeDraft);
      if (path.startsWith('/api/availability')) return json([availableRow]);
      submissions++;
      return await new Promise(done => { resolve = done; });
    };
    await import('../../src/BookingApp.Web/wwwroot/js/book.js?shared-booking-guard');
    await dom.get('#availability-form').listeners.submit({ preventDefault() { } });
    dom.get('#resources').children[0].children[0].children[0].children.at(-1).listeners.click();
    await dom.get('#draft-form').listeners.submit({ preventDefault() { } });
    const pending = dom.get('#draft-booking-form').listeners.submit({ preventDefault() { } });
    await dom.get('#booking-form').listeners.submit({ preventDefault() { } });
    await dom.get('#draft-booking-form').listeners.submit({ preventDefault() { } });
    assert.equal(submissions, 1);
    resolve(json({ ...completeDraft.resource, title: 'Review' }, 201));
    await pending;
    assert.equal(dom.get('#draft-confirmation').hidden, false);
    assert.equal(dom.get('#booking-form').hidden, true);
  } finally {
    Object.assign(globalThis, original);
  }
});

for (const count of [1, 2]) {
  test(`Book results use the matching resource count for pluralisation: ${count}`, async () => {
    const original = { fetch: globalThis.fetch, document: globalThis.document };
    const dom = bookPage();
    try {
      globalThis.document = dom.document;
      const rows = count === 1 ? [{ ...availableRow, available: false }] : [availableRow, { ...availableRow, available: false }];
      globalThis.fetch = async path => path === '/api/me' ? json(me) : json(rows);
      await import(`../../src/BookingApp.Web/wwwroot/js/book.js?count=${count}`);
      await dom.get('#availability-form').listeners.submit({ preventDefault() { } });
      assert.equal(dom.get('#availability-status').textContent, count === 1
        ? '0 of 1 matching resource available.' : '1 of 2 matching resources available.');
    } finally {
      Object.assign(globalThis, original);
    }
  });
}

for (const failure of [400, 422, 500, 403, 'network']) {
  test(`Book results explain availability failure: ${failure}`, async () => {
    const original = { fetch: globalThis.fetch, document: globalThis.document };
    const dom = bookPage();
    try {
      globalThis.document = dom.document;
      globalThis.fetch = async path => {
        if (path === '/api/me') return json(me);
        if (failure === 'network') throw new TypeError('Failed to fetch');
        return json({
          status: failure, errors: { minSeats: ['Minimum seats must be at least 1.'] },
          violations: [{ code: 'ambiguous_local_time', message: 'Choose another time.' }]
        }, failure);
      };
      await import(`../../src/BookingApp.Web/wwwroot/js/book.js?failure=${failure}`);
      await dom.get('#availability-form').listeners.submit({ preventDefault() { } });
      assert.equal(dom.get('#availability-status').textContent, [400, 422].includes(failure)
        ? 'Correct the problem above and check again.' : 'Availability could not be checked.');
      assert.equal(dom.get('#resources').children.length, 0);
    } finally {
      Object.assign(globalThis, original);
    }
  });
}

for (const title of ['<img src=x onerror=alert(1)>', '', null]) {
  test(`Book confirmation renders the response title as text only when nonempty: ${title}`, async () => {
    const original = { fetch: globalThis.fetch, document: globalThis.document };
    const dom = bookPage();
    try {
      globalThis.document = dom.document;
      globalThis.fetch = async path => {
        if (path === '/api/me') return json(me);
        if (path.startsWith('/api/availability')) return json([availableRow]);
        return json({ ...availableRow, id: 42, title }, 201);
      };
      await import(`../../src/BookingApp.Web/wwwroot/js/book.js?title=${encodeURIComponent(String(title))}`);
      await dom.get('#availability-form').listeners.submit({ preventDefault() { } });
      const body = dom.get('#resources').children[0].children[0].children[0];
      body.children.at(-1).listeners.click();
      dom.get('#booking-form').elements.title.value = 'Different request title';
      await dom.get('#booking-form').listeners.submit({ preventDefault() { } });
      const texts = dom.get('#confirmation').children.map(child => child.textContent);
      assert.equal(texts[1], 'Room A · Quantity 1');
      assert.equal(texts.includes('Different request title'), false);
      assert.equal(texts.includes(title), Boolean(title));
      assert.equal(dom.get('#confirmation').children.length, title ? 5 : 4);
    } finally {
      Object.assign(globalThis, original);
    }
  });
}

test('Book sends an empty pool quantity for server validation instead of silently defaulting to one', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = page();
  const form = dom.get('#availability-form');
  form.elements = Object.fromEntries(['type', 'quantity', 'minSeats', 'startDate', 'startTime', 'endDate', 'endTime'].map(name => [name, node()]));
  dom.get('#booking-form').elements = { title: node() };
  let requested;
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => {
      if (path === '/api/me') return json(me);
      requested = path;
      return json({ errors: { quantity: ['A quantity is required.'] } }, 400);
    };
    await import('../../src/BookingApp.Web/wwwroot/js/book.js?empty-quantity');
    Object.assign(form.elements.type, { value: 'Equipment' });
    form.elements.quantity.value = '';
    form.elements.startTime.value = '09:00'; form.elements.endTime.value = '10:00';
    await form.listeners.submit({ preventDefault() { } });
    const query = new URL(requested, 'http://localhost').searchParams;
    assert.equal(query.has('quantity'), true);
    assert.equal(query.get('quantity'), '');
  } finally { Object.assign(globalThis, original); }
});

test('My bookings permits retry when the initial collection request fails after session loads', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = page(); dom.get('#refresh').disabled = true;
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => path === '/api/me' ? json(me) : json({}, 500);
    await import('../../src/BookingApp.Web/wwwroot/js/my-bookings.js?initial-failure');
    assert.equal(dom.get('#refresh').disabled, false);
    assert.match(dom.get('#page-status').textContent, /could not be loaded/);
  } finally { Object.assign(globalThis, original); }
});

test('My bookings groups by status and end boundary, orders ties by id, and mutes cancellations', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const originalNow = Date.now;
  const dom = page();
  const now = '2026-10-26T12:00:00Z';
  const booking = (id, start, end, status = 'Confirmed') => ({
    id, start, end, status,
    resourceName: String(id), resourceType: 'Room', quantity: 1,
    cancelledAt: status === 'Cancelled' ? now : null
  });
  const rows = [
    booking(2, '2026-10-27T10:00Z', '2026-10-27T11:00Z'),
    booking(8, '2026-10-28T10:00Z', '2026-10-28T11:00Z', 'Cancelled'),
    booking(4, '2026-10-26T11:00Z', now),
    booking(1, '2026-10-27T10:00Z', '2026-10-27T11:00Z'),
    booking(7, '2026-10-25T10:00Z', '2026-10-25T11:00Z', 'Cancelled'),
    booking(3, '2026-10-26T11:00Z', '2026-10-26T13:00Z'),
    booking(5, '2026-10-26T11:00Z', now),
    booking(9, '2026-10-28T10:00Z', '2026-10-28T11:00Z', 'Cancelled'),
    booking(6, '2026-10-24T10:00Z', '2026-10-24T11:00Z')
  ];
  try {
    Date.now = () => Date.parse(now);
    globalThis.document = dom.document;
    globalThis.fetch = async path => path === '/api/me' ? json(me) : json(rows);
    await import('../../src/BookingApp.Web/wwwroot/js/my-bookings.js?groups');
    const ids = selector => dom.get(selector).children.map(column => column.children[0].children[0].children[0].textContent);
    assert.deepEqual(ids('#upcoming'), ['3', '1', '2']);
    assert.deepEqual(ids('#past'), ['5', '4', '6']);
    assert.deepEqual(ids('#cancelled'), ['9', '8', '7']);
    for (const column of dom.get('#cancelled').children) {
      const card = column.children[0];
      assert.match(card.className, /text-body-secondary/);
      const children = card.children[0].children;
      assert.ok(children.some(child => child.textContent?.includes('· Cancelled')));
      assert.ok(children.some(child => child.textContent?.startsWith('Cancelled ')));
      assert.equal(children.some(child => child.textContent === 'Cancel booking'), false);
    }
    assert.equal(dom.get('#page-status').textContent, '9 bookings loaded.');
  } finally {
    Date.now = originalNow;
    Object.assign(globalThis, original);
  }
});

for (const count of [0, 1]) {
  test(`My bookings empty groups and total count: ${count}`, async () => {
    const original = { fetch: globalThis.fetch, document: globalThis.document };
    const dom = page();
    try {
      globalThis.document = dom.document;
      globalThis.fetch = async path => path === '/api/me' ? json(me) : json(count ? [{
        id: 1,
        start: '2026-10-20T10:00Z', end: '2026-10-20T11:00Z', status: 'Cancelled', resourceName: 'Room', quantity: 1
      }] : []);
      await import(`../../src/BookingApp.Web/wwwroot/js/my-bookings.js?empty=${count}`);
      assert.equal(dom.get('#past').children[0].textContent, 'No bookings in this group.');
      if (!count) assert.equal(dom.get('#cancelled').children[0].textContent, 'No bookings in this group.');
      assert.equal(dom.get('#page-status').textContent, `${count} booking${count === 1 ? '' : 's'} loaded.`);
    } finally { Object.assign(globalThis, original); }
  });
}

test('Booking history displays newest starts first with descending id ties and keeps filters', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = page();
  const form = dom.get('#history-form');
  form.elements = Object.fromEntries(['resourceId', 'userId', 'from', 'to'].map(name => [name, node()]));
  form.elements.userId.value = '7';
  let requested;
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => {
      if (path === '/api/me') return json({ ...me, role: 'Admin' });
      if (!path.startsWith('/api/bookings?')) return json([]);
      requested = path;
      return json([1, 2, 3].map(id => ({
        id, resourceName: String(id), start: id === 1 ? '2026-10-25T10:00Z' : '2026-10-26T10:00Z',
        end: '2026-10-27T11:00Z', status: 'Confirmed', quantity: 1
      })));
    };
    await import('../../src/BookingApp.Web/wwwroot/js/booking-history.js?descending');
    assert.deepEqual(dom.get('#history-body').children.map(row => row.children[0].textContent), ['3', '2', '1']);
    assert.equal(requested, '/api/bookings?userId=7');
  } finally { Object.assign(globalThis, original); }
});

test('Book clears minSeats summary and field feedback on type input, then shows fresh errors on check', async () => {
  const original = { fetch: globalThis.fetch, document: globalThis.document };
  const dom = bookPage();
  const form = dom.get('#availability-form');
  const summary = node();
  const input = form.elements.minSeats;
  input.name = 'minSeats';
  input.id = 'min-seats';
  const classes = new Set();
  input.classList = { add: name => classes.add(name), remove: name => classes.delete(name) };
  const feedback = [];
  input.insertAdjacentElement = (_, child) => {
    feedback.push(child);
    child.remove = () => feedback.splice(feedback.indexOf(child), 1);
  };
  form.querySelector = selector => selector === '[data-errors]' ? summary : null;
  form.querySelectorAll = selector => {
    if (selector === '[name]') return [input];
    if (selector === '.is-invalid') return classes.has('is-invalid') ? [input] : [];
    if (selector === '[data-field-error]') return [...feedback];
    return [];
  };
  const assertCleared = () => {
    assert.equal(summary.hidden, true);
    assert.equal(summary.children.length, 0);
    assert.equal(classes.has('is-invalid'), false);
    assert.equal(input.getAttribute('aria-invalid'), undefined);
    assert.equal(input.getAttribute('aria-describedby'), undefined);
    assert.equal(feedback.length, 0);
  };
  const assertShown = () => {
    assert.equal(summary.hidden, false);
    assert.equal(summary.children[1].children[0].textContent, 'Minimum seats must be at least 1.');
    assert.equal(classes.has('is-invalid'), true);
    assert.equal(input.getAttribute('aria-invalid'), 'true');
    assert.equal(input.getAttribute('aria-describedby'), 'min-seats-error');
    assert.equal(feedback[0].textContent, 'Minimum seats must be at least 1.');
  };
  try {
    globalThis.document = dom.document;
    globalThis.fetch = async path => path === '/api/me' ? json(me)
      : json({ errors: { minSeats: ['Minimum seats must be at least 1.'] } }, 400);
    await import('../../src/BookingApp.Web/wwwroot/js/book.js?stale-errors');
    await form.listeners.submit({ preventDefault() { } });
    assertShown();
    form.elements.type.value = 'Equipment';
    form.elements.type.listeners.change();
    form.listeners.input();
    assertCleared();
    form.elements.type.value = 'Room';
    form.elements.type.listeners.change();
    form.listeners.input();
    assertCleared();
    await form.listeners.submit({ preventDefault() { } });
    assertShown();
  } finally { Object.assign(globalThis, original); }
});
