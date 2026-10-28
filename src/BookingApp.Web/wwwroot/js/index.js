import { loadSession, renderError } from './ui.js';

try {
  await loadSession();
  window.location.replace('/book.html');
} catch (error) {
  renderError(error, document.querySelector('main'));
}
