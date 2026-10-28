import { api } from './api.js';
import { loadSession, clearErrors, renderError, withBusy, setupPasswordToggle } from './ui.js';

const form = document.querySelector('#login-form');
const status = document.querySelector('#login-status');
try {
  await loadSession({ login: true });
} catch (error) {
  renderError(error, form);
}
status.textContent = '';
form.querySelector('button[type="submit"]').disabled = false;
form.addEventListener('submit', async event => {
  event.preventDefault();
  if (form.getAttribute('aria-busy') === 'true') return;
  clearErrors(form);
  await withBusy(form, async () => {
    status.textContent = 'Logging in…';
    try {
      await api('/api/auth/login', {
        method: 'POST', allowAnonymous: true, body: {
          email: form.elements.email.value, password: form.elements.password.value
        }
      });
      window.location.replace('/book.html');
    } catch (error) {
      renderError(error, form);
    } finally {
      status.textContent = '';
    }
  });
});
setupPasswordToggle(document.querySelector('#password'), document.querySelector('#toggle-password'));
