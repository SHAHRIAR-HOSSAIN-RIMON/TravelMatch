const app = document.querySelector("#app");
const toastRegion = document.querySelector("#toast-region");
const sessionKey = "travelmatch.session";
let session = readSession();
let renderSequence = 0;

function readSession() {
  try {
    return JSON.parse(sessionStorage.getItem(sessionKey) || "null");
  } catch {
    return null;
  }
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, character => ({
    "&": "&amp;",
    "<": "&lt;",
    ">": "&gt;",
    '"': "&quot;",
    "'": "&#39;"
  })[character]);
}

function formatDate(value) {
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, {
    month: "short",
    day: "numeric",
    year: "numeric"
  });
}

function formatMoney(value) {
  return new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: "BDT",
    maximumFractionDigits: 2
  }).format(value);
}

function formatTimestamp(value) {
  return new Date(value).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short"
  });
}

async function api(path, options = {}) {
  const headers = new Headers(options.headers || {});
  if (session?.token) headers.set("Authorization", `Bearer ${session.token}`);
  if (options.body && !headers.has("Content-Type")) headers.set("Content-Type", "application/json");

  const response = await fetch(path, { ...options, headers });
  if (response.status === 204) return null;

  const contentType = response.headers.get("content-type") || "";
  const payload = contentType.includes("application/json")
    ? await response.json()
    : await response.text();

  if (!response.ok) {
    const message = typeof payload === "string"
      ? payload
      : payload?.message || payload?.title || `Request failed (${response.status}).`;
    const error = new Error(message);
    error.status = response.status;
    error.payload = payload;
    throw error;
  }

  return payload;
}

function showToast(message, isError = false) {
  const toast = document.createElement("div");
  toast.className = `toast${isError ? " toast-error" : ""}`;
  toast.textContent = message;
  toastRegion.append(toast);
  window.setTimeout(() => toast.remove(), 4300);
}

function currentRoute() {
  return location.hash.replace(/^#/, "") || "/requests";
}

function navigate(path) {
  location.hash = path;
}

function navLink(path, text, activePath) {
  return `<a class="nav-link${activePath === path ? " active" : ""}" href="#${path}">${text}</a>`;
}

function shell(content, activePath) {
  const role = session?.role;
  const links = role === "Guide"
    ? [
      navLink("/requests", "Open requests", activePath),
      navLink("/proposals", "My proposals", activePath),
      navLink("/verification", "Verification", activePath)
    ]
    : [navLink("/notifications", "Notifications", activePath)];

  app.innerHTML = `
    <div class="shell">
      <header class="topbar">
        <a class="brand" href="#${role === "Guide" ? "/requests" : "/notifications"}" aria-label="TravelMatch home">
          <span class="brand-mark" aria-hidden="true">T</span><span>TravelMatch</span>
        </a>
        <nav class="nav" aria-label="Primary navigation">${links.join("")}</nav>
        <div class="user-actions">
          <span class="user-label">${escapeHtml(session?.fullName)}</span>
          <button class="button button-quiet" type="button" data-action="logout">Sign out</button>
        </div>
      </header>
      ${content}
    </div>`;

  app.querySelector('[data-action="logout"]')?.addEventListener("click", () => {
    sessionStorage.removeItem(sessionKey);
    session = null;
    navigate("/login");
    render();
  });
}

function loadingPage(label = "Loading") {
  app.innerHTML = `<main class="page"><p class="eyebrow">TravelMatch</p><h1>${escapeHtml(label)}</h1><div class="list"><div class="skeleton"></div><div class="skeleton"></div></div></main>`;
}

function emptyState(title, message, action = "") {
  return `<section class="empty-state"><h2>${escapeHtml(title)}</h2><p>${escapeHtml(message)}</p>${action}</section>`;
}

async function renderLogin() {
  app.innerHTML = `
    <main class="auth-page">
      <section class="auth-wrap">
        <div class="auth-visual">
          <div><p class="eyebrow">Local knowledge, better journeys</p><h1>Meet the place through its people.</h1><p>Find open trip requests, prepare a thoughtful plan, and send an offer that fits the journey.</p></div>
          <span class="auth-stamp">TRAVELMATCH · GUIDE PORTAL</span>
        </div>
        <div class="auth-form-wrap">
          <p class="eyebrow">Welcome back</p><h2>Sign in</h2><p>Use your TravelMatch account to continue.</p>
          <form class="auth-form" id="login-form">
            <div class="field"><label for="email">Email</label><input id="email" name="email" type="email" autocomplete="username" required /></div>
            <div class="field"><label for="password">Password</label><input id="password" name="password" type="password" autocomplete="current-password" required /></div>
            <p class="auth-error" id="login-error" role="alert"></p>
            <button class="button button-primary" id="login-submit" type="submit">Sign in</button>
          </form>
        </div>
      </section>
    </main>`;

  app.querySelector("#login-form").addEventListener("submit", async event => {
    event.preventDefault();
    const form = event.currentTarget;
    const button = app.querySelector("#login-submit");
    const errorBox = app.querySelector("#login-error");
    const data = new FormData(form);
    button.disabled = true;
    button.innerHTML = '<span class="spinner" aria-hidden="true"></span> Signing in';
    errorBox.textContent = "";

    try {
      const login = await api("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({ email: data.get("email"), password: data.get("password") })
      });
      session = login;
      sessionStorage.setItem(sessionKey, JSON.stringify(session));
      navigate(session.role === "Guide" ? "/requests" : "/notifications");
      render();
    } catch (error) {
      errorBox.textContent = error.message || "Sign in failed. Please try again.";
      button.disabled = false;
      button.textContent = "Sign in";
    }
  });
}

async function renderOpenRequests() {
  loadingPage("Open trip requests");
  try {
    const [requests, verification] = await Promise.all([
      api("/api/guide/triprequests"),
      api("/api/guide/verification")
    ]);
    if (currentRoute() !== "/requests") return;

    const requestRows = requests.map(request => `
      <a class="request-row" href="#/requests/${request.id}">
        <div class="request-main">
          <div class="row-title"><h3>${escapeHtml(request.destination)}</h3><span class="pill">${escapeHtml(request.numberOfTravelers)} travelers</span></div>
          <div class="row-meta"><span>${formatDate(request.startDate)} – ${formatDate(request.endDate)}</span><span>Request #${request.id}</span></div>
          ${request.description ? `<p class="row-description">${escapeHtml(request.description)}</p>` : ""}
        </div>
        <div class="row-action"><span class="budget">${formatMoney(request.budget)}</span><span class="button button-secondary">View request</span></div>
      </a>`).join("");

    const verificationNotice = verification.isVerified ? "" : `
      <section class="verification-banner">
        <div><h2>Verification ${escapeHtml(verification.status.toLowerCase())}</h2><p>You can browse requests, but only verified guides can submit proposals.</p></div>
        <a class="button button-secondary" href="#/verification">View status</a>
      </section>`;

    shell(`<main class="page">
      <div class="page-heading"><div class="heading-copy"><p class="eyebrow">Guide workspace</p><h1>Open trip requests</h1><p class="subhead">Find a journey that fits your expertise and prepare an offer the traveler can compare.</p></div><span class="count">${requests.length}</span></div>
      ${verificationNotice}
      <div class="list">${requestRows || emptyState("No open requests right now", "New trip requests will appear here when travelers post them.")}</div>
    </main>`, "/requests");
  } catch (error) {
    showToast(error.message || "Could not load trip requests.", true);
    shell(`<main class="page">${emptyState("Requests are unavailable", "Please check your connection and try again.", '<button class="button button-secondary" data-action="retry">Retry</button>')}</main>`, "/requests");
    app.querySelector('[data-action="retry"]')?.addEventListener("click", render);
  }
}

async function renderRequestDetail(tripRequestId) {
  loadingPage("Trip request details");
  try {
    const request = await api(`/api/guide/triprequests/${tripRequestId}`);
    if (currentRoute() !== `/requests/${tripRequestId}`) return;
    shell(`<main class="page">
      <a class="back-link" href="#/requests">← Open requests</a>
      <p class="eyebrow">Trip request #${request.id}</p><h1>${escapeHtml(request.destination)}</h1>
      <section class="context-band" aria-label="Trip request summary">
        <div class="context-cell"><span class="context-label">Dates</span><span class="context-value">${formatDate(request.startDate)} – ${formatDate(request.endDate)}</span></div>
        <div class="context-cell"><span class="context-label">Budget</span><span class="context-value">${formatMoney(request.budget)}</span></div>
        <div class="context-cell"><span class="context-label">Travelers</span><span class="context-value">${escapeHtml(request.numberOfTravelers)}</span></div>
        <div class="context-cell"><span class="context-label">Status</span><span class="context-value">${escapeHtml(request.status)}</span></div>
      </section>
      <section class="detail-panel"><p class="eyebrow">Traveler's brief</p><p>${escapeHtml(request.description || "No additional description provided.")}</p></section>
      <a class="button button-primary" href="#/requests/${request.id}/proposal">Submit proposal</a>
    </main>`, `/requests/${tripRequestId}`);
  } catch {
    if (currentRoute() !== `/requests/${tripRequestId}`) return;
    shell(`<main class="page"><a class="back-link" href="#/requests">← Open requests</a>${emptyState("Request no longer available", "This trip request may have closed. Refresh the open request list to see current opportunities.")}</main>`, "/requests");
  }
}

function updateProposalFormState(form) {
  const price = form.elements.price;
  const availability = form.elements.availability;
  const inclusions = form.elements.inclusions;
  const priceError = form.querySelector('[data-error="price"]');
  const availabilityError = form.querySelector('[data-error="availability"]');
  const inclusionsError = form.querySelector('[data-error="inclusions"]');
  const priceValue = Number(price.value);
  const showErrors = form.dataset.submitted === "true";

  price.setCustomValidity(price.value && priceValue <= 0 ? "Price must be greater than 0." : "");
  priceError.textContent = showErrors && (!price.value || priceValue <= 0)
    ? (!price.value ? "Price is required." : "Price must be greater than 0.")
    : "";
  availabilityError.textContent = showErrors && !availability.value.trim() ? "Availability is required." : "";
  inclusionsError.textContent = showErrors && !inclusions.value.trim() ? "Inclusions are required." : "";

  for (const field of [price, availability, inclusions]) {
    const invalid = !field.value.trim() || !field.checkValidity();
    field.setAttribute("aria-invalid", showErrors && invalid ? "true" : "false");
  }

  const valid = Boolean(price.value && priceValue > 0 && availability.value.trim() && inclusions.value.trim()) && form.checkValidity();
  form.querySelector("[type=submit]").disabled = !valid;
}

async function renderProposalForm(tripRequestId) {
  try {
    const verification = await api("/api/guide/verification");
    if (verification.status !== "Verified") {
      navigate("/verification");
      return render();
    }

    const request = await api(`/api/guide/triprequests/${tripRequestId}`);
    if (currentRoute() !== `/requests/${tripRequestId}/proposal`) return;

    shell(`<main class="page">
      <a class="back-link" href="#/requests/${request.id}">← Back to request</a>
      <p class="eyebrow">New offer · Request #${request.id}</p><h1>Submit a proposal</h1>
      <p class="subhead">Set a clear price and show the traveler what your plan includes.</p>
      <section class="context-band" aria-label="Read-only trip request summary">
        <div class="context-cell"><span class="context-label">Destination</span><span class="context-value">${escapeHtml(request.destination)}</span></div>
        <div class="context-cell"><span class="context-label">Dates</span><span class="context-value">${formatDate(request.startDate)} – ${formatDate(request.endDate)}</span></div>
        <div class="context-cell"><span class="context-label">Travelers</span><span class="context-value">${escapeHtml(request.numberOfTravelers)}</span></div>
        <div class="context-cell"><span class="context-label">Budget</span><span class="context-value">${formatMoney(request.budget)}</span></div>
      </section>
      <div class="form-layout">
        <form class="form" id="proposal-form" novalidate>
          <div class="field"><label for="price">Price <span class="required">*</span></label><input id="price" name="price" type="number" min="0.01" step="0.01" inputmode="decimal" required aria-describedby="price-error" /><span class="field-error" id="price-error" data-error="price"></span></div>
          <div class="field"><label for="estimatedExpenses">Estimated expenses</label><input id="estimatedExpenses" name="estimatedExpenses" type="number" min="0" step="0.01" inputmode="decimal" /><span class="field-hint">Optional breakdown of costs included in your price.</span></div>
          <div class="field field-wide"><label for="availability">Availability <span class="required">*</span></label><input id="availability" name="availability" type="text" maxlength="300" placeholder="Available for the requested dates" required aria-describedby="availability-error" /><span class="field-error" id="availability-error" data-error="availability"></span></div>
          <div class="field field-wide"><label for="inclusions">Inclusions <span class="required">*</span></label><textarea id="inclusions" name="inclusions" maxlength="4000" placeholder="Meals, local transport, accommodation..." required aria-describedby="inclusions-error"></textarea><span class="field-error" id="inclusions-error" data-error="inclusions"></span></div>
          <div class="field field-wide"><label for="exclusions">Exclusions</label><textarea id="exclusions" name="exclusions" maxlength="4000" placeholder="Anything the traveler should budget for separately"></textarea></div>
          <div class="field field-wide"><label for="additionalNotes">Additional notes</label><textarea id="additionalNotes" name="additionalNotes" maxlength="2000" placeholder="A detail that helps the traveler decide"></textarea></div>
          <div class="form-actions"><button class="button button-primary" type="submit" disabled>Submit proposal</button><a class="button button-secondary" href="#/requests/${request.id}">Cancel</a></div>
        </form>
        <aside class="form-aside"><h3>${escapeHtml(request.destination)}</h3><p>${formatDate(request.startDate)} – ${formatDate(request.endDate)} · ${escapeHtml(request.numberOfTravelers)} travelers</p><span class="aside-price">${formatMoney(request.budget)}</span><p>Traveler's stated budget</p></aside>
      </div>
    </main>`, `/requests/${tripRequestId}/proposal`);

    const form = app.querySelector("#proposal-form");
    for (const field of form.querySelectorAll("input, textarea")) {
      field.addEventListener("input", () => updateProposalFormState(form));
      field.addEventListener("blur", () => {
        if (!field.value.trim() && field.required) field.setAttribute("aria-invalid", "true");
      });
    }
    updateProposalFormState(form);

    form.addEventListener("submit", async event => {
      event.preventDefault();
      form.dataset.submitted = "true";
      updateProposalFormState(form);
      if (!form.checkValidity()) {
        form.reportValidity();
        return;
      }

      const button = form.querySelector('[type="submit"]');
      const formData = new FormData(form);
      button.disabled = true;
      button.innerHTML = '<span class="spinner" aria-hidden="true"></span> Submitting';

      try {
        const response = await api(`/api/triprequests/${tripRequestId}/proposals`, {
          method: "POST",
          body: JSON.stringify({
            price: Number(formData.get("price")),
            estimatedExpenses: formData.get("estimatedExpenses") ? Number(formData.get("estimatedExpenses")) : null,
            availability: formData.get("availability"),
            inclusions: formData.get("inclusions"),
            exclusions: formData.get("exclusions"),
            additionalNotes: formData.get("additionalNotes")
          })
        });
        showToast(response.message || "Proposal submitted!");
        navigate("/proposals");
      } catch (error) {
        showToast(error.status === 409 || error.status === 403
          ? error.message
          : "Failed to submit proposal. Please try again.", true);
        button.disabled = false;
        button.textContent = "Submit proposal";
      }
    });
  } catch (error) {
    if (currentRoute() !== `/requests/${tripRequestId}/proposal`) return;
    if (error.status === 403) {
      navigate("/verification");
      return render();
    }
    showToast(error.message || "Could not load this trip request.", true);
    navigate("/requests");
  }
}

async function renderMyProposals() {
  loadingPage("My proposals");
  try {
    const proposals = await api("/api/proposals/mine");
    if (currentRoute() !== "/proposals") return;
    const rows = proposals.map(proposal => {
      const status = proposal.status.toLowerCase();
      return `<article class="proposal-row">
        <div class="proposal-main"><div class="row-title"><h3>${escapeHtml(proposal.destination)}</h3><span class="pill pill-${status}">${escapeHtml(proposal.status)}</span></div>
          <div class="row-meta"><span>${formatDate(proposal.startDate)} – ${formatDate(proposal.endDate)}</span><span>Submitted ${formatTimestamp(proposal.submittedAt)}</span></div>
          <p class="row-description">${escapeHtml(proposal.inclusions)}</p>
        </div>
        <div class="row-action"><span class="budget">${formatMoney(proposal.price)}</span><span class="row-meta">Request budget ${formatMoney(proposal.tripRequestBudget)}</span></div>
      </article>`;
    }).join("");
    shell(`<main class="page"><div class="page-heading"><div class="heading-copy"><p class="eyebrow">Guide workspace</p><h1>My proposals</h1><p class="subhead">Track the offers you've sent and their current review status.</p></div><span class="count">${proposals.length}</span></div><div class="list">${rows || emptyState("No proposals yet", "Start with an open trip request and send your first offer.", '<a class="button button-primary" href="#/requests">Browse requests</a>')}</div></main>`, "/proposals");
  } catch (error) {
    showToast(error.message || "Could not load your proposals.", true);
    shell(`<main class="page">${emptyState("Proposals are unavailable", "Please check your connection and try again.", '<button class="button button-secondary" data-action="retry">Retry</button>')}</main>`, "/proposals");
    app.querySelector('[data-action="retry"]')?.addEventListener("click", render);
  }
}

async function renderNotifications() {
  loadingPage("Notifications");
  try {
    const notifications = await api("/api/notifications/mine");
    if (currentRoute() !== "/notifications") return;
    const rows = notifications.map(notification => `
      <article class="notice-row${notification.isRead ? "" : " notice-unread"}">
        <span class="notice-indicator" aria-hidden="true"></span>
        <div class="notice-copy"><strong>${escapeHtml(notification.title)}</strong><p>${escapeHtml(notification.message)}</p><time>${formatTimestamp(notification.createdAt)}</time></div>
        ${notification.isRead ? "" : `<button class="button button-secondary" type="button" data-read="${notification.id}">Mark read</button>`}
      </article>`).join("");

    shell(`<main class="page"><div class="page-heading"><div class="heading-copy"><p class="eyebrow">Traveler inbox</p><h1>Notifications</h1><p class="subhead">Updates about proposals sent for your trip requests.</p></div><span class="count">${notifications.filter(item => !item.isRead).length}</span></div><div class="list">${rows || emptyState("You're all caught up", "New proposal updates will appear here.")}</div></main>`, "/notifications");
    for (const button of app.querySelectorAll("[data-read]")) {
      button.addEventListener("click", async () => {
        button.disabled = true;
        try {
          await api(`/api/notifications/${button.dataset.read}/read`, { method: "PUT" });
          await renderNotifications();
        } catch (error) {
          button.disabled = false;
          showToast(error.message || "Could not update notification.", true);
        }
      });
    }
  } catch (error) {
    showToast(error.message || "Could not load notifications.", true);
    shell(`<main class="page">${emptyState("Notifications are unavailable", "Please check your connection and try again.", '<button class="button button-secondary" data-action="retry">Retry</button>')}</main>`, "/notifications");
    app.querySelector('[data-action="retry"]')?.addEventListener("click", render);
  }
}

async function renderVerification() {
  loadingPage("Guide verification");
  try {
    const verification = await api("/api/guide/verification");
    if (currentRoute() !== "/verification") return;
    const verified = verification.isVerified;
    shell(`<main class="page"><p class="eyebrow">Guide account</p><h1>Verification</h1><p class="subhead">Your account status controls access to proposal submission.</p>
      <section class="verification-banner${verified ? " is-verified" : ""}"><div><h2>${verified ? "You're verified" : `Status: ${escapeHtml(verification.status)}`}</h2><p>${verified ? "You can submit proposals for any open trip request." : "Proposal submission is blocked until your guide profile is verified."}</p></div>${verified ? '<a class="button button-primary" href="#/requests">Browse requests</a>' : ""}</section>
      ${verified ? "" : '<p class="row-description">Verification review is handled by the TravelMatch team. This page will reflect your status after it changes.</p>'}
    </main>`, "/verification");
  } catch (error) {
    showToast(error.message || "Could not load verification status.", true);
    navigate("/requests");
  }
}

async function render() {
  const sequence = ++renderSequence;
  const route = currentRoute();

  if (!session?.token) {
    return renderLogin();
  }

  if (session.role === "Guide") {
    if (route === "/login") return navigate("/requests");
    if (route === "/requests") return renderOpenRequests();

    const proposalMatch = route.match(/^\/requests\/(\d+)\/proposal$/);
    if (proposalMatch) return renderProposalForm(proposalMatch[1]);

    const requestMatch = route.match(/^\/requests\/(\d+)$/);
    if (requestMatch) return renderRequestDetail(requestMatch[1]);
    if (route === "/proposals") return renderMyProposals();
    if (route === "/verification") return renderVerification();
    return navigate("/requests");
  }

  if (session.role === "Tourist" && route !== "/login") {
    if (route === "/notifications") return renderNotifications();
    return navigate("/notifications");
  }

  sessionStorage.removeItem(sessionKey);
  session = null;
  if (sequence === renderSequence) return renderLogin();
}

window.addEventListener("hashchange", render);
render();