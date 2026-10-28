import { api } from './api.js';
import { element, loadSession, clearErrors, renderError, withBusy, officeWeek } from './ui.js';

const main = document.querySelector('main');
const form = document.querySelector('#utilisation-form');
const status = document.querySelector('#page-status');
const body = document.querySelector('#utilisation-body');
const hours = new Intl.NumberFormat('en-GB', { maximumFractionDigits: 2 });
const percent = new Intl.NumberFormat('en-GB', { style: 'percent', maximumFractionDigits: 2 });

async function runReport() {
  clearErrors(form);
  body.replaceChildren();
  await withBusy(form, async () => {
    document.querySelector('#report-fields').disabled = true;
    status.textContent = 'Loading booked utilisation…';
    const query = new URLSearchParams({ from: form.elements.from.value, to: form.elements.to.value });
    if (form.elements.type.value) query.set('type', form.elements.type.value);
    try {
      const rows = await api(`/api/reports/utilisation?${query}`);
      for (const resource of rows) {
        const row = element('tr');
        for (const value of [resource.name, resource.type, resource.isActive ? 'Active' : 'Inactive', resource.quantity,
          resource.bookingCount, hours.format(resource.bookedHours), hours.format(resource.availableHours),
          resource.bookedUtilisation === null ? 'N/A' : percent.format(resource.bookedUtilisation)]) row.append(element('td', value));
        body.append(row);
      }
      if (!rows.length) {
        const row = element('tr');
        const cell = element('td', 'No resources match this type.');
        cell.colSpan = 8;
        row.append(cell);
        body.append(row);
      }
      status.textContent = `${rows.length} resources in booked utilisation report.`;
    } catch (error) {
      renderError(error, form);
      status.textContent = error.status === 400
        ? 'Correct the problem below and run the report again.' : 'Report could not be loaded.';
    } finally {
      document.querySelector('#report-fields').disabled = false;
    }
  });
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  if (form.getAttribute('aria-busy') === 'true') return;
  await runReport();
});

try {
  const me = await loadSession({ admin: true });
  const week = officeWeek(me.officeTimeZone);
  form.elements.from.value = week.from;
  form.elements.to.value = week.to;
  document.querySelector('#admin-content').hidden = false;
  await runReport();
} catch (error) {
  renderError(error, main);
  status.textContent = error.status === 403 ? '' : 'Report could not be loaded. Reload the page to retry.';
}
