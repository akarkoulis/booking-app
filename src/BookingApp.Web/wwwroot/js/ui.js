import { api } from './api.js';

export function reloadRestoredPage(window) {
  window.addEventListener('pageshow', event => {
    if (event.persisted) window.location.reload();
  });
}

if (typeof window !== 'undefined') reloadRestoredPage(window);

export function element(tag, text, className) {
  const node = document.createElement(tag);
  if (text !== undefined && text !== null) node.textContent = text;
  if (className) node.className = className;
  return node;
}

function city(zone) {
  return zone.split('/').at(-1).replaceAll('_', ' ');
}

export function timeZoneLabel(zone, instants = [new Date()]) {
  const formatter = new Intl.DateTimeFormat('en-GB', { timeZone: zone, timeZoneName: 'short' });
  const abbreviations = [...new Set(instants.map(value => formatter.formatToParts(new Date(value))
    .find(part => part.type === 'timeZoneName').value))];
  return `All times are ${city(zone)} time (${abbreviations.join(' / ')})`;
}

export function showTimeZone(zone, instants) {
  document.querySelectorAll('[data-time-zone]').forEach(node => {
    node.textContent = timeZoneLabel(zone, instants);
  });
}

export function formatInstant(instant, zone) {
  return new Intl.DateTimeFormat('en-GB', { timeZone: zone, dateStyle: 'medium', timeStyle: 'short' }).format(new Date(instant));
}

export function bookingIntervalText(start, end, zone) {
  const date = new Intl.DateTimeFormat('en-GB', { timeZone: zone, weekday: 'short', day: 'numeric', month: 'short', year: 'numeric' });
  const time = new Intl.DateTimeFormat('en-GB', { timeZone: zone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' });
  const parts = date.formatToParts(new Date(start));
  const part = name => parts.find(value => value.type === name).value;
  return `${part('weekday')} ${part('day')} ${part('month')} ${part('year')}, ${time.format(new Date(start))}–${time.format(new Date(end))}`;
}

export function dualTimeHint(instant, officeZone, browserZone = Intl.DateTimeFormat().resolvedOptions().timeZone) {
  const value = new Date(instant); // Only server instants, never form date/time strings.
  const date = zone => new Intl.DateTimeFormat('en-GB', { timeZone: zone, dateStyle: 'medium' }).format(value);
  const time = zone => new Intl.DateTimeFormat('en-GB', { timeZone: zone, hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(value);
  const officeDate = date(officeZone);
  const localDate = date(browserZone);
  const officeTime = time(officeZone);
  const localTime = time(browserZone);
  if (officeDate === localDate && officeTime === localTime) return '';
  return `${officeTime} ${city(officeZone)} (${localTime}${officeDate !== localDate ? `, ${localDate}` : ''} your time)`;
}

export function timeText(instant, zone) {
  const hint = dualTimeHint(instant, zone);
  return `${formatInstant(instant, zone)}${hint ? ` — ${hint}` : ''}`;
}

function officeCalendar(zone, now) {
  const parts = new Intl.DateTimeFormat('en-GB', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(now);
  const part = name => Number(parts.find(p => p.type === name).value);
  return new Date(Date.UTC(part('year'), part('month') - 1, part('day')));
}

function calendarString(value) {
  return `${value.getUTCFullYear()}-${String(value.getUTCMonth() + 1).padStart(2, '0')}-${String(value.getUTCDate()).padStart(2, '0')}`;
}

export function officeToday(zone, now = new Date()) {
  return calendarString(officeCalendar(zone, now));
}

export function officeTomorrow(zone, now = new Date()) {
  const tomorrow = officeCalendar(zone, now);
  tomorrow.setUTCDate(tomorrow.getUTCDate() + 1);
  return calendarString(tomorrow);
}

export function canCancel(booking, now = new Date()) {
  return booking.status === 'Confirmed' && new Date(booking.end).getTime() > Number(now);
}

export function compareBookingsDescending(a, b) {
  return new Date(b.start).getTime() - new Date(a.start).getTime() || b.id - a.id;
}

export function numberInputValue(input) {
  // Native number controls also expose '' for incomplete tokens such as '-'.
  if (input.validity?.badInput) {
    throw { status: 400, problem: { errors: { [input.name]: ['Enter a whole number.'] } } };
  }
  return input.value === '' ? null : Number(input.value);
}

export function officeWeek(zone, now = new Date()) {
  const monday = officeCalendar(zone, now);
  monday.setUTCDate(monday.getUTCDate() - (monday.getUTCDay() + 6) % 7);
  const sunday = new Date(monday);
  sunday.setUTCDate(sunday.getUTCDate() + 6);
  return { from: calendarString(monday), to: calendarString(sunday) };
}

export function clearErrors(container) {
  container.querySelectorAll('.is-invalid').forEach(input => {
    input.classList.remove('is-invalid');
    input.removeAttribute('aria-invalid');
    const errorId = `${input.id}-error`;
    const descriptions = (input.getAttribute('aria-describedby') || '').split(' ').filter(id => id && id !== errorId);
    if (descriptions.length) input.setAttribute('aria-describedby', descriptions.join(' '));
    else input.removeAttribute('aria-describedby');
  });
  container.querySelectorAll('[data-field-error]').forEach(node => node.remove());
  const summary = container.querySelector('[data-errors]');
  if (summary) {
    summary.replaceChildren();
    summary.hidden = true;
  }
}

export function renderError(error, container, { fieldMap = {}, bookingConflict = false } = {}) {
  if (error.name === 'AbortError' || (error.status === 401 && !container.matches('[data-login]'))) return;
  const summary = container.querySelector('[data-errors]');
  if (!summary) return;
  summary.replaceChildren(element('p', 'Please check the following:', 'fw-semibold mb-1'));
  const list = element('ul', null, 'mb-0');
  const messages = [];
  if (error.status === 400 && error.problem?.errors) {
    for (const [rawKey, values] of Object.entries(error.problem.errors)) {
      const key = rawKey.replace(/^\$\./, '');
      const fields = fieldMap[key] || [key];
      for (const name of fields) {
        const input = Array.from(container.querySelectorAll('[name]')).find(node => node.name === name);
        if (!input) continue;
        const feedback = element('div', values.join(' '), 'invalid-feedback');
        feedback.id = `${input.id}-error`;
        feedback.dataset.fieldError = '';
        input.classList.add('is-invalid');
        input.setAttribute('aria-invalid', 'true');
        input.setAttribute('aria-describedby', [input.getAttribute('aria-describedby'), feedback.id].filter(Boolean).join(' '));
        const group = input.closest('.input-group');
        if (group) group.append(feedback);
        else input.insertAdjacentElement('afterend', feedback);
      }
      messages.push(...values);
    }
  } else if (error.status === 422 && error.problem?.violations?.length) {
    messages.push(...error.problem.violations.map(v => v.message));
  } else if (error.status === 409) {
    messages.push(bookingConflict ? 'That resource was just taken; choose another.' :
      (error.problem?.title || 'The request could not be completed. Please try again.'));
  } else if (error.status === 403) {
    messages.push('You do not have access to this page or action.');
  } else if (error.status === 401 && container.matches('[data-login]')) {
    messages.push('Invalid email or password.');
  } else {
    messages.push('The request could not be completed. Please try again.');
  }
  if (!messages.length) messages.push('The request could not be completed. Please try again.');
  new Set(messages).forEach(message => list.append(element('li', message)));
  summary.append(list);
  summary.hidden = false;
  summary.focus();
}

export function setupPasswordToggle(password, togglePassword) {
  function setVisible(show) {
    password.type = show ? 'text' : 'password';
    togglePassword.setAttribute('aria-pressed', String(show));
    togglePassword.querySelector('[data-icon="show"]').hidden = show;
    togglePassword.querySelector('[data-icon="hide"]').hidden = !show;
  }
  togglePassword.addEventListener('click', () => setVisible(password.type === 'password'));
  return () => setVisible(false);
}

export async function withBusy(form, action) {
  const buttons = [...form.querySelectorAll('button[type="submit"]')];
  const previous = buttons.map(button => button.disabled);
  buttons.forEach(button => {
    button.disabled = true;
  });
  form.setAttribute('aria-busy', 'true');
  try {
    return await action();
  } finally {
    buttons.forEach((button, i) => {
      button.disabled = previous[i];
    });
    form.removeAttribute('aria-busy');
  }
}

export async function loadSession({ login = false, admin = false } = {}) {
  let me;
  try {
    me = await api('/api/me', { allowAnonymous: login });
  } catch (error) {
    if (login && error.status === 401) return null;
    throw error;
  }
  if (login) {
    window.location.replace('/book.html');
    return null;
  }
  const navigation = document.querySelector('[data-navigation]');
  if (navigation) {
    navigation.replaceChildren();
    const links = [['/book.html', 'Book'], ['/my-bookings.html', 'My bookings']];
    if (me.role === 'Admin') links.push(['/users.html', 'Users'], ['/resources.html', 'Resources'],
      ['/booking-policy.html', 'Booking policy'], ['/utilisation.html', 'Utilisation'], ['/booking-history.html', 'Booking history']);
    for (const [href, label] of links) {
      const link = element('a', label, 'nav-link');
      link.href = href;
      if (window.location.pathname === href) link.setAttribute('aria-current', 'page');
      navigation.append(link);
    }
    navigation.append(element('span', me.displayName, 'nav-link text-body-secondary'));
    const logout = element('button', 'Log out', 'btn btn-outline-secondary btn-sm ms-sm-auto');
    logout.type = 'button';
    logout.addEventListener('click', async () => {
      logout.disabled = true;
      try {
        await api('/api/auth/logout', { method: 'POST', body: {} });
        window.location.replace('/login.html');
      } catch (error) {
        renderError(error, document.querySelector('main'));
        logout.disabled = false;
      }
    });
    navigation.append(logout);
  }
  showTimeZone(me.officeTimeZone);
  if (admin && me.role !== 'Admin') {
    const error = new Error();
    error.status = 403;
    throw error;
  }
  return me;
}
