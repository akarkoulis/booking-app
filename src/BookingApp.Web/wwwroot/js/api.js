export class ApiError extends Error {
  constructor(status, problem = {}) {
    super(problem.title || 'The request could not be completed. Please try again.');
    this.status = status;
    this.problem = problem;
  }
}

// Every API call uses this function, including session checks and logout.
export async function api(path, { method = 'GET', body, signal, allowAnonymous = false } = {}) {
  let response;
  try {
    response = await fetch(path, {
      method, credentials: 'same-origin', signal,
      headers: { Accept: 'application/json', ...(body !== undefined ? { 'Content-Type': 'application/json' } : {}) },
      body: body === undefined ? undefined : JSON.stringify(body)
    });
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new ApiError(0);
  }
  if (response.status === 401 && !allowAnonymous) {
    window.location.replace('/login.html');
    throw new ApiError(401);
  }
  if (response.status === 204) return null;
  let data;
  try {
    data = await response.json();
  } catch {
    throw new ApiError(response.ok ? 0 : response.status);
  }
  if (!response.ok) throw new ApiError(response.status, data);
  return data;
}
