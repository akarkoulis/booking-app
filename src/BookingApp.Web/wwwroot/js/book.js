import { api } from './api.js';
import { element, loadSession, clearErrors, renderError, withBusy, officeTomorrow, showTimeZone, timeText, bookingIntervalText, dualTimeHint } from './ui.js';

const main = document.querySelector('main');
const availabilityForm = document.querySelector('#availability-form');
const bookingForm = document.querySelector('#booking-form');
const results = document.querySelector('#resources');
const status = document.querySelector('#availability-status');
const confirmation = document.querySelector('#confirmation');
const fieldMap = { start: ['startDate', 'startTime'], end: ['endDate', 'endTime'] };
let me, checked = null, selected = null, controller = null, generation = 0, bookingInFlight = false;
const draftSlot = document.querySelector('#booking-draft-slot');
const draftForm = document.querySelector('#draft-form');
const draftBookingForm = document.querySelector('#draft-booking-form');
const draftText = document.querySelector('#draft-text');
const draftStatus = document.querySelector('#draft-status');
const draftChoices = document.querySelector('#draft-choices');
const draftConfirmation = document.querySelector('#draft-confirmation');
let draft = null, draftSelected = null, draftController = null, draftGeneration = 0;

function criteria() {
  const fields = availabilityForm.elements;
  const room = fields.type.value === 'Room';
  return {
    type: fields.type.value,
    start: `${fields.startDate.value}T${fields.startTime.value}`,
    end: `${fields.endDate.value}T${fields.endTime.value}`,
    quantity: room ? 1 : (fields.quantity.value === '' ? '' : Number(fields.quantity.value)),
    minSeats: room && fields.minSeats.value !== '' ? Number(fields.minSeats.value) : null
  };
}

function invalidate() {
  clearErrors(availabilityForm);
  generation++;
  controller?.abort();
  checked = selected = null;
  results.replaceChildren();
  document.querySelector('#checked-times').textContent = '';
  bookingForm.hidden = true;
  status.textContent = 'Choose dates and times to see availability.';
  showTimeZone(me.officeTimeZone);
}

function updateType() {
  const room = availabilityForm.elements.type.value === 'Room';
  document.querySelector('#seats-group').hidden = !room;
  document.querySelector('#quantity-group').hidden = room;
}

function choose(row, button) {
  selected = row;
  results.querySelectorAll('button').forEach(node => {
    node.textContent = 'Choose';
    node.setAttribute('aria-pressed', 'false');
  });
  button.textContent = 'Selected';
  button.setAttribute('aria-pressed', 'true');
  document.querySelector('#selected-resource').textContent = `${row.resourceName} · Quantity ${checked.quantity}`;
  clearErrors(bookingForm);
  bookingForm.hidden = false;
  bookingForm.elements.title.focus();
}

async function checkAvailability() {
  invalidate();
  const current = generation;
  const submitted = criteria();
  controller = new AbortController();
  clearErrors(availabilityForm);
  status.textContent = 'Checking availability…';
  try {
    const query = new URLSearchParams();
    Object.entries(submitted).forEach(([key, value]) => {
      if (value !== null) query.set(key, value);
    });
    const rows = await api(`/api/availability?${query}`, { signal: controller.signal });
    if (current !== generation) return;
    checked = submitted;
    status.textContent = rows.length ? `${rows.filter(row => row.available).length} of ${rows.length} matching ${rows.length === 1 ? 'resource' : 'resources'} available.` : 'No resources match these criteria.';
    if (rows.length) {
      const row = rows[0];
      document.querySelector('#checked-times').textContent = `${timeText(row.start, me.officeTimeZone)} → ${timeText(row.end, me.officeTimeZone)}`;
      showTimeZone(me.officeTimeZone, [row.start, row.end]);
    }
    for (const row of rows) {
      const column = element('div', null, 'col-md-6');
      const card = element('article', null, 'card');
      const body = element('div', null, 'card-body');
      body.append(element('h3', row.resourceName, 'h5 card-title'));
      body.append(element('p', [row.resourceType, row.location, row.seats === null ? null : `${row.seats} seats`, `Quantity ${row.quantity}`].filter(Boolean).join(' · '), 'card-text'));
      body.append(element('p', row.available ? 'Available' : 'Unavailable', `fw-semibold ${row.available ? 'text-success' : 'text-danger'}`));
      if (row.violations.length) {
        const list = element('ul');
        row.violations.forEach(violation => list.append(element('li', violation.message)));
        body.append(list);
      }
      const button = element('button', 'Choose', 'btn btn-outline-primary');
      button.type = 'button';
      button.disabled = !row.available;
      button.setAttribute('aria-label', `Choose ${row.resourceName}`);
      button.setAttribute('aria-pressed', 'false');
      button.addEventListener('click', () => choose(row, button));
      body.append(button);
      card.append(body);
      column.append(card);
      results.append(column);
    }
  } catch (error) {
    if (current !== generation || error.name === 'AbortError') return;
    status.textContent = error.status === 400 || error.status === 422
      ? 'Correct the problem above and check again.' : 'Availability could not be checked.';
    renderError(error, availabilityForm, { fieldMap });
  }
}

availabilityForm.addEventListener('input', () => {
  if (!me) return;
  invalidate();
  confirmation.hidden = true;
});
availabilityForm.elements.type.addEventListener('change', updateType);
availabilityForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (availabilityForm.getAttribute('aria-busy') === 'true' || bookingInFlight) return;
  invalidateDraft();
  confirmation.hidden = true;
  await withBusy(availabilityForm, checkAvailability);
});

function showConfirmation(target, booking) {
  target.replaceChildren(element('h2', 'Booking confirmed', 'h4'),
    element('p', `${booking.resourceName} · Quantity ${booking.quantity}`),
    element('p', `${timeText(booking.start, me.officeTimeZone)} → ${timeText(booking.end, me.officeTimeZone)}`));
  if (booking.title) target.append(element('p', booking.title));
  const link = element('a', 'View My bookings');
  link.href = '/my-bookings.html';
  target.append(link);
  showTimeZone(me.officeTimeZone, [booking.start, booking.end]);
  target.hidden = false;
  target.focus();
}

async function submitBooking(request, form, errorContainer, target, onSuccess, onConflict, onValidation) {
  if (bookingInFlight) return;
  bookingInFlight = true;
  clearErrors(errorContainer);
  const submitButtons = [...form.querySelectorAll('button[type="submit"]')];
  const controls = [...availabilityForm.querySelectorAll('input, select, button'), ...results.querySelectorAll('button'),
    ...draftSlot.querySelectorAll('input, textarea, button')].filter(control => !submitButtons.includes(control));
  const disabled = controls.map(control => control.disabled);
  controls.forEach(control => {
    control.disabled = true;
  });
  await withBusy(form, async () => {
    try {
      const booking = await api('/api/bookings', { method: 'POST', body: request });
      onSuccess();
      showConfirmation(target, booking);
      form.reset();
    } catch (error) {
      if (error.status === 409) await onConflict(request.resourceId);
      renderError(error, errorContainer, { bookingConflict: true });
      if (error.status === 400 && onValidation) onValidation(error);
    } finally {
      controls.forEach((control, i) => {
        control.disabled = disabled[i];
      });
      bookingInFlight = false;
    }
  });
}

bookingForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (!checked || !selected || bookingInFlight) return;
  clearErrors(availabilityForm);
  const request = {
    resourceId: selected.resourceId, start: checked.start, end: checked.end,
    quantity: checked.quantity, title: bookingForm.elements.title.value || null
  };
  await submitBooking(request, bookingForm, bookingForm, confirmation, invalidate, async () => {
    await checkAvailability();
    renderError({ status: 409 }, availabilityForm, { bookingConflict: true });
  }, error => renderError(error, availabilityForm, { fieldMap }));
});

function invalidateDraft() {
  draftGeneration++;
  draftController?.abort();
  draft = draftSelected = null;
  draftChoices.replaceChildren();
  draftBookingForm.hidden = true;
  draftConfirmation.hidden = true;
  draftStatus.textContent = '';
  clearErrors(draftForm);
  clearErrors(draftSlot);
}

function selectDraft(row) {
  if (bookingInFlight) return;
  draftSelected = row;
  draftBookingForm.hidden = false;
  document.querySelector('#draft-selected-resource').textContent = `${row.resourceName} · Quantity ${draft.quantity ?? 1}`;
  document.querySelector('#draft-confirm-line').textContent = bookingIntervalText(row.start, row.end, me.officeTimeZone);
  document.querySelector('#draft-time-hint').textContent = dualTimeHint(row.start, me.officeTimeZone);
  draftChoices.querySelectorAll('button').forEach(button => {
    const isSelected = button.dataset.resourceId === String(row.resourceId);
    button.setAttribute('aria-pressed', String(isSelected));
    button.className = isSelected ? 'btn btn-primary' : 'btn btn-outline-primary';
  });
  showTimeZone(me.officeTimeZone, [row.start, row.end]);
}

function renderDraftChoices() {
  draftChoices.replaceChildren();
  draftBookingForm.hidden = true;
  draftSelected = null;
  if (draft.resource && !draft.resource.available) {
    draftStatus.textContent = `${draft.resource.resourceName} isn't available:`;
    for (const violation of draft.resource.violations) draftChoices.append(element('p', violation.message, 'w-100 mb-0'));
  }
  const choices = [draft.resource, ...draft.alternatives].filter(row => row?.available);
  for (const row of choices) {
    const button = element('button', row.resourceName, 'btn btn-outline-primary');
    button.type = 'button';
    button.dataset.resourceId = String(row.resourceId);
    button.setAttribute('aria-pressed', 'false');
    button.addEventListener('click', () => selectDraft(row));
    draftChoices.append(button);
  }
  if (draft.resource?.available) selectDraft(draft.resource);
}

draftText.addEventListener('input', invalidateDraft);

draftForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (bookingInFlight || draftForm.getAttribute('aria-busy') === 'true') return;
  invalidate();
  confirmation.hidden = true;
  invalidateDraft();
  const current = draftGeneration;
  draftController = new AbortController();
  draftStatus.textContent = 'Drafting…';
  await withBusy(draftForm, async () => {
    try {
      const response = await api('/api/booking-drafts', { method: 'POST', body: { text: draftText.value }, signal: draftController.signal });
      if (current !== draftGeneration) return;
      draft = response;
      document.querySelector('#draft-title').value = draft.title || '';
      const complete = draft.date && draft.startTime && draft.endTime && (draft.resourceType || draft.resourceName);
      if (!complete) draftStatus.textContent = draft.notUnderstood || 'Add a date, times and what to book.';
      else if (!draft.resource) draftStatus.textContent = draft.resourceName && !draft.resourceType
        ? 'No resource matches that name.' : 'No suitable resources are available at these times.';
      else if (draft.resourceName && draft.resourceName.toLowerCase() !== draft.resource.resourceName.toLowerCase())
        draftStatus.textContent = `No resource matches "${draft.resourceName}"; suggesting ${draft.resource.resourceName}.`;
      else draftStatus.textContent = 'Check the draft, then book your chosen resource.';
      renderDraftChoices();
    } catch (error) {
      if (current !== draftGeneration || error.name === 'AbortError') return;
      draftStatus.textContent = error.status === 400 || error.status === 422
        ? 'Correct the problem above and try again.'
        : error.status === 503 ? 'Drafting is unavailable; use the form below.' : 'Drafting could not be completed.';
      if (error.status !== 503) renderError(error, draftForm);
    }
  });
});

draftBookingForm.addEventListener('submit', async event => {
  event.preventDefault();
  if (!draftSelected || bookingInFlight) return;
  const request = {
    resourceId: draftSelected.resourceId, start: draftSelected.start, end: draftSelected.end,
    quantity: draft.quantity ?? 1, title: document.querySelector('#draft-title').value || null
  };
  await submitBooking(request, draftBookingForm, draftSlot, draftConfirmation, invalidateDraft, async resourceId => {
    if (draft.resource?.resourceId === resourceId) draft.resource = null;
    draft.alternatives = draft.alternatives.filter(row => row.resourceId !== resourceId);
    renderDraftChoices();
  });
});

try {
  me = await loadSession();
  const tomorrow = officeTomorrow(me.officeTimeZone);
  availabilityForm.elements.startDate.value = tomorrow;
  availabilityForm.elements.endDate.value = tomorrow;
  updateType();
  draftBookingForm.hidden = true;
  draftSlot.hidden = false;
  document.querySelector('#book-content').hidden = false;
  document.querySelector('#page-status').textContent = '';
} catch (error) {
  document.querySelector('#page-status').textContent = 'Booking could not be loaded.';
  renderError(error, main);
}
