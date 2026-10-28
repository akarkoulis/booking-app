import { api } from './api.js';
import { loadSession, clearErrors, renderError, withBusy, numberInputValue } from './ui.js';

const main = document.querySelector('main');
const form = document.querySelector('#policy-form');
const status = document.querySelector('#page-status');
const names = ['maxActiveRoomBookingsPerUser', 'defaultMinNoticeMinutes'];
function showPolicy(policy) {
  for (const name of names) form.elements[name].value = policy[name] ?? '';
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  if (form.getAttribute('aria-busy') === 'true') return;
  clearErrors(form);
  await withBusy(form, async () => {
    try {
      const body = Object.fromEntries(names.map(name => [name, numberInputValue(form.elements[name])]));
      document.querySelector('#policy-fields').disabled = true;
      status.textContent = 'Saving policy…';
      showPolicy(await api('/api/booking-policy', { method: 'PUT', body }));
      status.textContent = 'Policy saved.';
    } catch (error) {
      renderError(error, form);
      status.textContent = 'Policy could not be saved.';
    } finally {
      document.querySelector('#policy-fields').disabled = false;
    }
  });
});

try {
  await loadSession({ admin: true });
  showPolicy(await api('/api/booking-policy'));
  document.querySelector('#admin-content').hidden = false;
  status.textContent = '';
} catch (error) {
  renderError(error, main);
  status.textContent = error.status === 403 ? '' : 'Policy could not be loaded. Reload the page to retry.';
}
