import { api } from './api.js';
import { element, loadSession, clearErrors, renderError, withBusy, timeText, showTimeZone, compareBookingsDescending } from './ui.js';

const main = document.querySelector('main');
const form = document.querySelector('#history-form');
const status = document.querySelector('#page-status');
const body = document.querySelector('#history-body');
let me;

function addOptions(select, rows, label) {
  for (const row of rows) {
    const option = element('option', `${label(row)}${row.isActive ? '' : ' (inactive)'}`);
    option.value = row.id;
    select.append(option);
  }
}

async function search() {
  clearErrors(form);
  body.replaceChildren();
  await withBusy(form, async () => {
    document.querySelector('#history-fields').disabled = true;
    status.textContent = 'Loading booking history…';
    const query = new URLSearchParams();
    for (const name of ['resourceId', 'userId', 'from', 'to']) if (form.elements[name].value !== '') query.set(name, form.elements[name].value);
    try {
      const bookings = await api(`/api/bookings?${query}`);
      const instants = bookings.flatMap(booking => [booking.start, booking.end]);
      showTimeZone(me.officeTimeZone, instants.length ? instants : undefined);
      for (const booking of bookings.sort(compareBookingsDescending)) {
        const row = element('tr');
        for (const value of [booking.resourceName, booking.userDisplayName, booking.title ?? '—',
        timeText(booking.start, me.officeTimeZone), timeText(booking.end, me.officeTimeZone), booking.quantity, booking.status]) row.append(element('td', value));
        body.append(row);
      }
      if (!bookings.length) {
        const row = element('tr');
        const cell = element('td', 'No bookings match these filters.');
        cell.colSpan = 7;
        row.append(cell);
        body.append(row);
      }
      status.textContent = `${bookings.length} bookings found.`;
    } catch (error) {
      renderError(error, form);
      status.textContent = error.status === 400
        ? 'Correct the problem below and search again.' : 'Booking history could not be loaded.';
    } finally {
      document.querySelector('#history-fields').disabled = false;
    }
  });
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  if (form.getAttribute('aria-busy') === 'true') return;
  await search();
});

try {
  me = await loadSession({ admin: true });
  const [resources, users] = await Promise.all([api('/api/resources?includeInactive=true'), api('/api/users')]);
  addOptions(form.elements.resourceId, resources, resource => resource.name);
  addOptions(form.elements.userId, users, user => `${user.displayName} (${user.email})`);
  document.querySelector('#admin-content').hidden = false;
  await search();
} catch (error) {
  renderError(error, main);
  status.textContent = error.status === 403 ? '' : 'Booking history could not be loaded. Reload the page to retry.';
}
