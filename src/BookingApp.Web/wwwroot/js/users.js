import { api } from './api.js';
import { element, loadSession, clearErrors, renderError, setupPasswordToggle } from './ui.js';

const main = document.querySelector('main');
const form = document.querySelector('#user-form');
const fields = form.elements;
const resetPasswordToggle = setupPasswordToggle(fields.password, document.querySelector('#toggle-password'));
const editor = document.querySelector('#user-editor');
const status = document.querySelector('#page-status');
let me, currentId = null, busy = false;

function setBusy(value) {
  busy = value;
  document.querySelector('#user-fields').disabled = value;
  main.querySelectorAll('button').forEach(button => {
    button.disabled = value;
  });
  form.setAttribute('aria-busy', String(value));
}

function showUser(user = null) {
  currentId = user?.id ?? null;
  fields.email.value = user?.email ?? '';
  fields.email.readOnly = user !== null;
  fields.displayName.value = user?.displayName ?? '';
  fields.role.value = user?.role ?? 'Employee';
  fields.password.value = '';
  resetPasswordToggle();
  fields.isActive.checked = user?.isActive ?? true;
  const own = user?.id === me.id;
  fields.role.disabled = own;
  fields.isActive.disabled = own;
  for (const field of [fields.role, fields.isActive]) {
    if (own) {
      field.setAttribute('aria-describedby', 'own-access-help');
    } else {
      field.removeAttribute('aria-describedby');
    }
  }
  document.querySelector('#own-access-help').hidden = !own;
  document.querySelector('#password-group').hidden = user !== null;
  document.querySelector('#active-group').hidden = user === null;
  document.querySelector('#user-heading').textContent = user ? `Edit ${user.displayName}` : 'Add user';
  clearErrors(form);
  editor.hidden = false;
  fields.displayName.focus();
}

async function loadUsers() {
  const users = await api('/api/users');
  const body = document.querySelector('#users-body');
  body.replaceChildren();
  for (const user of users) {
    const row = element('tr');
    for (const value of [user.displayName, user.email, user.role, user.isActive ? 'Active' : 'Inactive']) row.append(element('td', value));
    const action = element('td');
    const edit = element('button', 'Edit', 'btn btn-outline-primary btn-sm');
    edit.type = 'button';
    edit.setAttribute('aria-label', `Edit ${user.displayName}`);
    edit.addEventListener('click', async () => {
      if (busy) return;
      setBusy(true);
      clearErrors(main);
      try {
        showUser(await api(`/api/users/${user.id}`));
      } catch (error) {
        renderError(error, main);
      } finally {
        setBusy(false);
        if (!editor.hidden) fields.displayName.focus();
      }
    });
    action.append(edit);
    row.append(action);
    body.append(row);
  }
  if (!users.length) {
    const row = element('tr');
    const cell = element('td', 'No users.');
    cell.colSpan = 5;
    row.append(cell);
    body.append(row);
  }
}

document.querySelector('#new-user').addEventListener('click', () => {
  if (!busy) showUser();
});
document.querySelector('#close-user').addEventListener('click', () => {
  if (busy) return;
  editor.hidden = true;
  resetPasswordToggle();
  document.querySelector('#new-user').focus();
});
form.addEventListener('submit', async event => {
  event.preventDefault();
  if (busy) return;
  clearErrors(form);
  setBusy(true);
  status.textContent = 'Saving user…';
  const body = { displayName: fields.displayName.value, role: fields.role.value };
  if (currentId === null) Object.assign(body, { email: fields.email.value, password: fields.password.value });
  else body.isActive = fields.isActive.checked;
  try {
    const user = await api(currentId === null ? '/api/users' : `/api/users/${currentId}`, { method: currentId === null ? 'POST' : 'PUT', body });
    showUser(user);
    status.textContent = 'User saved.';
    await loadUsers();
  } catch (error) {
    renderError(error, form);
    if (status.textContent === 'Saving user…') status.textContent = 'User could not be saved.';
  } finally {
    setBusy(false);
  }
});

try {
  me = await loadSession({ admin: true });
  await loadUsers();
  document.querySelector('#admin-content').hidden = false;
  status.textContent = '';
} catch (error) {
  renderError(error, main);
  status.textContent = error.status === 403 ? '' : 'Users could not be loaded. Reload the page to retry.';
}
