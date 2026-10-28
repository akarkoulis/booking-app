import { api } from './api.js';
import { element, loadSession, clearErrors, renderError, numberInputValue } from './ui.js';

const main = document.querySelector('main');
const form = document.querySelector('#resource-form');
const fields = form.elements;
const editor = document.querySelector('#resource-editor');
const status = document.querySelector('#page-status');
const numberFields = ['seats', 'minDurationMinutes', 'maxDurationMinutes', 'maxAdvanceDays', 'minNoticeMinutes'];
let currentId = null, busy = false;

function updateType() {
  const room = fields.type.value === 'Room';
  fields.quantity.disabled = room;
  if (room) fields.quantity.value = '1';
}

function setBusy(value) {
  busy = value;
  document.querySelector('#resource-fields').disabled = value;
  main.querySelectorAll('button').forEach(button => {
    button.disabled = value;
  });
  form.setAttribute('aria-busy', String(value));
}

function showResource(resource = null) {
  currentId = resource?.id ?? null;
  for (const name of ['name', 'location', 'opensAt', 'closesAt', ...numberFields]) fields[name].value = resource?.[name] ?? '';
  fields.type.value = resource?.type ?? 'Room';
  fields.quantity.value = resource?.quantity ?? 1;
  fields.isActive.checked = resource?.isActive ?? true;
  updateType();
  clearErrors(form);
  document.querySelector('#resource-heading').textContent = resource ? `Edit ${resource.name}` : 'Create resource';
  editor.hidden = false;
  fields.name.focus();
}

async function loadResources() {
  const resources = await api('/api/resources?includeInactive=true');
  const body = document.querySelector('#resources-body');
  body.replaceChildren();
  for (const resource of resources) {
    const row = element('tr');
    for (const value of [resource.name, resource.type, resource.location ?? '—', resource.quantity, resource.seats ?? '—', resource.isActive ? 'Active' : 'Inactive']) row.append(element('td', value));
    const action = element('td');
    const edit = element('button', 'Edit', 'btn btn-outline-primary btn-sm');
    edit.type = 'button';
    edit.setAttribute('aria-label', `Edit ${resource.name}`);
    edit.addEventListener('click', async () => {
      if (busy) return;
      setBusy(true);
      clearErrors(main);
      try {
        showResource(await api(`/api/resources/${resource.id}`));
      } catch (error) {
        renderError(error, main);
      } finally {
        setBusy(false);
        if (!editor.hidden) fields.name.focus();
      }
    });
    action.append(edit);
    row.append(action);
    body.append(row);
  }
  if (!resources.length) {
    const row = element('tr');
    const cell = element('td', 'No resources.');
    cell.colSpan = 6;
    row.append(cell);
    body.append(row);
  }
}

fields.type.addEventListener('change', updateType);
document.querySelector('#new-resource').addEventListener('click', () => {
  if (!busy) showResource();
});
document.querySelector('#close-resource').addEventListener('click', () => {
  if (busy) return;
  editor.hidden = true;
  document.querySelector('#new-resource').focus();
});
form.addEventListener('submit', async event => {
  event.preventDefault();
  if (busy) return;
  clearErrors(form);
  status.textContent = 'Saving resource…';
  const time = value => value === '' ? null : value.length === 5 ? `${value}:00` : value;
  try {
    const body = { name: fields.name.value, type: fields.type.value, location: fields.location.value || null,
      quantity: fields.type.value === 'Room' ? 1 : numberInputValue(fields.quantity),
      opensAt: time(fields.opensAt.value), closesAt: time(fields.closesAt.value), isActive: fields.isActive.checked };
    for (const name of numberFields) body[name] = numberInputValue(fields[name]);
    setBusy(true);
    const resource = await api(currentId === null ? '/api/resources' : `/api/resources/${currentId}`, { method: currentId === null ? 'POST' : 'PUT', body });
    showResource(resource);
    status.textContent = 'Resource saved.';
    await loadResources();
  } catch (error) {
    renderError(error, form);
    if (status.textContent === 'Saving resource…') status.textContent = 'Resource could not be saved.';
  } finally {
    setBusy(false);
  }
});

try {
  await loadSession({ admin: true });
  await loadResources();
  document.querySelector('#admin-content').hidden = false;
  status.textContent = '';
} catch (error) {
  renderError(error, main);
  status.textContent = error.status === 403 ? '' : 'Resources could not be loaded. Reload the page to retry.';
}
