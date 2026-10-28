import { api } from './api.js';
import { element, loadSession, clearErrors, renderError, timeText, showTimeZone, canCancel, compareBookingsDescending } from './ui.js';

const main = document.querySelector('main');
const status = document.querySelector('#page-status');
const refresh = document.querySelector('#refresh');
let me, busy = false;

function bookingCard(booking) {
  const column = element('div', null, 'col-md-6');
  const card = element('article', null, booking.status === 'Cancelled' ? 'card text-body-secondary bg-body-tertiary' : 'card');
  const body = element('div', null, 'card-body');
  body.append(element('h3', booking.resourceName, 'h5 card-title'));
  if (booking.title) body.append(element('p', booking.title, 'card-text'));
  body.append(element('p', `${timeText(booking.start, me.officeTimeZone)} → ${timeText(booking.end, me.officeTimeZone)}`, 'card-text'),
    element('p', `${booking.resourceType} · Quantity ${booking.quantity} · ${booking.status}`, 'fw-semibold'));
  if (booking.cancelledAt) body.append(element('p', `Cancelled ${timeText(booking.cancelledAt, me.officeTimeZone)}`, 'small'));
  if (canCancel(booking)) {
    const cancel = element('button', 'Cancel booking', 'btn btn-outline-danger');
    cancel.type = 'button';
    cancel.setAttribute('aria-label', `Cancel ${booking.title || booking.resourceName}`);
    const prompt = element('div', null, 'mt-3');
    prompt.hidden = true;
    const question = element('p', 'Cancel this booking? This frees the resource for others.');
    const confirm = element('button', 'Confirm cancellation', 'btn btn-danger me-2');
    confirm.type = 'button';
    const keep = element('button', 'Keep booking', 'btn btn-outline-secondary');
    keep.type = 'button';
    prompt.append(question, confirm, keep);
    cancel.addEventListener('click', () => {
      cancel.hidden = true;
      prompt.hidden = false;
      keep.focus();
    });
    keep.addEventListener('click', () => {
      prompt.hidden = true;
      cancel.hidden = false;
      cancel.focus();
    });
    confirm.addEventListener('click', async () => {
      if (busy) return;
      busy = true;
      const buttons = [...main.querySelectorAll('button')];
      buttons.forEach(button => {
        button.disabled = true;
      });
      clearErrors(main);
      status.textContent = 'Cancelling booking…';
      try {
        await api(`/api/bookings/${booking.id}/cancel`, { method: 'POST', body: {} });
        await refreshBookings();
        status.textContent = 'Booking cancelled.';
        refresh.focus();
      } catch (error) {
        renderError(error, main);
        status.textContent = 'Cancellation could not be completed.';
      } finally {
        busy = false;
        buttons.forEach(button => {
          button.disabled = false;
        });
        refresh.disabled = false;
      }
    });
    body.append(cancel, prompt);
  }
  card.append(body);
  column.append(card);
  return column;
}

async function refreshBookings() {
  const bookings = await api(`/api/bookings?${new URLSearchParams({ userId: me.id })}`);
  const now = Date.now();
  const upcoming = document.querySelector('#upcoming');
  const past = document.querySelector('#past');
  const cancelled = document.querySelector('#cancelled');
  upcoming.replaceChildren();
  past.replaceChildren();
  cancelled.replaceChildren();
  const instants = bookings.flatMap(booking => [booking.start, booking.end]);
  showTimeZone(me.officeTimeZone, instants.length ? instants : undefined);
  const groups = [
    [upcoming, bookings.filter(booking => canCancel(booking, now)).sort((a, b) => compareBookingsDescending(b, a))],
    [past, bookings.filter(booking => booking.status === 'Confirmed' && !canCancel(booking, now)).sort(compareBookingsDescending)],
    [cancelled, bookings.filter(booking => booking.status === 'Cancelled').sort(compareBookingsDescending)]
  ];
  for (const [container, rows] of groups) {
    for (const booking of rows) container.append(bookingCard(booking));
    if (!container.childElementCount) container.append(element('p', 'No bookings in this group.'));
  }
  status.textContent = `${bookings.length} booking${bookings.length === 1 ? '' : 's'} loaded.`;
}

refresh.addEventListener('click', async () => {
  if (busy) return;
  busy = true;
  refresh.disabled = true;
  clearErrors(main);
  status.textContent = 'Refreshing bookings…';
  try {
    await refreshBookings();
  } catch (error) {
    renderError(error, main);
    status.textContent = 'Bookings could not be refreshed.';
  } finally {
    busy = false;
    refresh.disabled = false;
  }
});

try {
  me = await loadSession();
  await refreshBookings();
} catch (error) {
  renderError(error, main);
  status.textContent = 'Bookings could not be loaded.';
} finally {
  if (me) refresh.disabled = false;
}
