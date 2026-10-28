import test from 'node:test';
import assert from 'node:assert/strict';
import { officeToday, officeWeek, timeZoneLabel, dualTimeHint } from '../../src/BookingApp.Web/wwwroot/js/ui.js';
import { api, ApiError } from '../../src/BookingApp.Web/wwwroot/js/api.js';
import * as ui from '../../src/BookingApp.Web/wwwroot/js/ui.js';

test('Draft interval weekday and clock use office time even when the server offset has another date', () => {
  assert.equal(ui.bookingIntervalText('2026-10-20T23:30:00-05:00', '2026-10-21T00:30:00-05:00', 'Europe/London'),
    'Wed 21 Oct 2026, 05:30–06:30');
});

for (const persisted of [true, false]) {
  test(`pageshow persisted=${persisted} ${persisted ? 'reloads' : 'keeps'} the page`, () => {
    let listener;
    let reloads = 0;
    const window = {
      addEventListener(name, handler) {
        assert.equal(name, 'pageshow');
        listener = handler;
      },
      location: { reload() { reloads++; } }
    };
    ui.reloadRestoredPage(window);
    listener({ persisted });
    assert.equal(reloads, persisted ? 1 : 0);
  });
}

test('office tomorrow crosses month/year and both clock-change dates using the office calendar', () => {
  for (const [now, expected] of [
    ['2026-10-31T12:00:00Z', '2026-11-01'],
    ['2026-12-31T12:00:00Z', '2027-01-01'],
    ['2026-03-28T12:00:00Z', '2026-03-29'],
    ['2026-03-29T12:00:00Z', '2026-03-30'],
    ['2026-10-24T12:00:00Z', '2026-10-25'],
    ['2026-10-25T12:00:00Z', '2026-10-26'],
    ['2026-10-20T23:30:00Z', '2026-10-22']
  ]) assert.equal(ui.officeTomorrow('Europe/London', new Date(now)), expected);
});

test('canCancel requires confirmed status and end strictly after now', () => {
  const now = new Date('2026-10-27T10:00:00Z');
  assert.equal(ui.canCancel({ status: 'Confirmed', end: '2026-10-27T10:00:00Z' }, now), false);
  assert.equal(ui.canCancel({ status: 'Confirmed', end: '2026-10-27T10:00:01Z' }, now), true);
  assert.equal(ui.canCancel({ status: 'Confirmed', end: '2026-10-27T09:59:59Z' }, now), false);
  assert.equal(ui.canCancel({ status: 'Cancelled', end: '2026-10-27T11:00:00Z' }, now), false);
});

test('renderError uses a non-booking 409 title or a generic fallback and retains booking conflict copy', () => {
  const original = globalThis.document;
  const makeNode = () => ({ children: [], append(...children) { this.children.push(...children); },
    replaceChildren(...children) { this.children = children; }, focus() {} });
  const summary = makeNode();
  const container = { matches: () => false, querySelector: () => summary };
  try {
    globalThis.document = { createElement: makeNode };
    const text = node => [node.textContent || '', ...node.children.map(text)].join(' ');
    ui.renderError(new ApiError(409, { title: 'A different conflict.' }), container);
    assert.match(text(summary), /A different conflict\./);
    ui.renderError(new ApiError(409), container);
    assert.match(text(summary), /The request could not be completed\. Please try again\./);
    assert.doesNotMatch(text(summary), /email/);
    ui.renderError(new ApiError(409, { title: 'Capacity' }), container, { bookingConflict: true });
    assert.match(text(summary), /just taken; choose another/);
  } finally { globalThis.document = original; }
});

test('office today and week use office calendar across midnight, DST and year boundaries', () => {
  assert.equal(officeToday('Europe/London', new Date('2026-10-20T23:30:00Z')), '2026-10-21');
  assert.deepEqual(officeWeek('Europe/London', new Date('2026-10-25T12:00:00Z')), { from: '2026-10-19', to: '2026-10-25' });
  assert.deepEqual(officeWeek('Europe/London', new Date('2027-01-01T12:00:00Z')), { from: '2026-12-28', to: '2027-01-03' });
});

test('zone labels derive city and date-specific abbreviations', () => {
  assert.equal(timeZoneLabel('Europe/London', ['2026-10-20T13:00:00Z']), 'All times are London time (BST)');
  assert.equal(timeZoneLabel('Europe/London', ['2026-10-27T14:00:00Z']), 'All times are London time (GMT)');
  assert.match(timeZoneLabel('America/New_York', ['2026-10-20T13:00:00Z']), /^All times are New York time \(.+\)$/);
});

test('dual hint compares formatted dates and times rather than IANA identifiers', () => {
  assert.equal(dualTimeHint('2026-10-20T13:00:00Z', 'Europe/London', 'Europe/Dublin'), '');
  assert.equal(dualTimeHint('2026-10-20T13:00:00Z', 'Europe/London', 'Europe/Lisbon'), '');
  assert.equal(dualTimeHint('2026-10-20T13:00:00Z', 'Europe/London', 'Asia/Singapore'), '14:00 London (21:00 your time)');
  assert.match(dualTimeHint('2026-10-20T23:00:00Z', 'Europe/London', 'America/New_York'), /20 Oct 2026/);
});

test('API keeps JSON numeric types, cookies and empty 204 responses', async () => {
  const original = globalThis.fetch;
  try {
    globalThis.fetch = async (path, options) => {
      assert.equal(path, '/api/bookings');
      assert.equal(options.credentials, 'same-origin');
      assert.equal(JSON.parse(options.body).quantity, 2);
      return new Response(null, { status: 204 });
    };
    assert.equal(await api('/api/bookings', { method: 'POST', body: { quantity: 2 } }), null);
  } finally { globalThis.fetch = original; }
});

test('API preserves field and violation details and normalizes network errors', async () => {
  const original = globalThis.fetch;
  try {
    for (const [status, body] of [[400, { errors: { start: ['Bad date'] } }], [422, { violations: [{ code: 'dst', message: 'Choose another time' }] }]]) {
      globalThis.fetch = async () => new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/problem+json' } });
      await assert.rejects(api('/api/bookings'), error => error instanceof ApiError && error.status === status && JSON.stringify(error.problem) === JSON.stringify(body));
    }
    globalThis.fetch = async () => { throw new TypeError('offline'); };
    await assert.rejects(api('/api/bookings'), error => error instanceof ApiError && error.status === 0);
  } finally { globalThis.fetch = original; }
});
