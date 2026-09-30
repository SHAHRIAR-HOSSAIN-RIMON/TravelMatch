const API_URL_KEY = "travelmatch.apiUrl";
const TOKEN_KEY = "travelmatch.guideToken";
const ROLE_KEY = "travelmatch.guideRole";

const elements = {
  apiUrl: document.querySelector("#api-url"),
  authPanel: document.querySelector("#auth-panel"),
  browsePanel: document.querySelector("#browse-panel"),
  loginForm: document.querySelector("#login-form"),
  loginError: document.querySelector("#login-error"),
  signOut: document.querySelector("#sign-out"),
  guideStatus: document.querySelector("#guide-status"),
  verificationBanner: document.querySelector("#verification-banner"),
  verificationTitle: document.querySelector("#verification-title"),
  verificationCopy: document.querySelector("#verification-copy"),
  verificationHelp: document.querySelector("#verification-help"),
  filters: document.querySelector("#filters"),
  requestList: document.querySelector("#request-list"),
  resultCount: document.querySelector("#result-count"),
  loadError: document.querySelector("#load-error"),
  emptyState: document.querySelector("#empty-state"),
  emptyEyebrow: document.querySelector("#empty-eyebrow"),
  emptyTitle: document.querySelector("#empty-title"),
  emptyCopy: document.querySelector("#empty-copy"),
  emptyReset: document.querySelector("#empty-reset"),
  detailDialog: document.querySelector("#detail-dialog"),
  detailId: document.querySelector("#detail-id"),
  detailContent: document.querySelector("#detail-content"),
  messageDialog: document.querySelector("#message-dialog"),
  messageTitle: document.querySelector("#message-title"),
  messageCopy: document.querySelector("#message-copy")
};

let requests = [];
let isVerified = false;
let loading = false;
let latestLoadId = 0;

function apiBase() {
  return (localStorage.getItem(API_URL_KEY) || elements.apiUrl.value || "http://localhost:5022")
    .trim()
    .replace(/\/+$/, "");
}

function authHeaders() {
  return { Authorization: `Bearer ${sessionStorage.getItem(TOKEN_KEY)}` };
}

function formatBudget(amount) {
  return new Intl.NumberFormat("en-BD", {
    style: "currency",
    currency: "BDT",
    maximumFractionDigits: 0
  }).format(amount);
}

function formatDate(value, options = { day: "numeric", month: "short", year: "numeric" }) {
  const date = new Date(`${value}T00:00:00`);
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat("en", options).format(date);
}

function formatDateRange(start, end) {
  const startDate = new Date(`${start}T00:00:00`);
  const endDate = new Date(`${end}T00:00:00`);
  if (Number.isNaN(startDate.getTime()) || Number.isNaN(endDate.getTime())) return `${start} – ${end}`;
  const sameYear = startDate.getFullYear() === endDate.getFullYear();
  const startText = new Intl.DateTimeFormat("en", { day: "numeric", month: "short" }).format(startDate);
  const endText = new Intl.DateTimeFormat("en", {
    day: "numeric",
    month: "short",
    ...(sameYear ? {} : { year: "numeric" })
  }).format(endDate);
  return `${startText} – ${endText}${sameYear ? `, ${startDate.getFullYear()}` : ""}`;
}

function showMessage(title, copy) {
  elements.messageTitle.textContent = title;
  elements.messageCopy.textContent = copy;
  elements.messageDialog.showModal();
}

function showLogin() {
  elements.authPanel.hidden = false;
  elements.browsePanel.hidden = true;
  elements.signOut.hidden = true;
  elements.guideStatus.hidden = true;
}

function showBrowse() {
  elements.authPanel.hidden = true;
  elements.browsePanel.hidden = false;
  elements.signOut.hidden = false;
  elements.guideStatus.hidden = false;
  elements.guideStatus.textContent = "GUIDE ACCOUNT";
}

function requestParameters() {
  const data = new FormData(elements.filters);
  const params = new URLSearchParams();
  for (const name of ["destination", "tripType", "travelDateFrom", "travelDateTo", "minBudget", "maxBudget"]) {
    const value = String(data.get(name) || "").trim();
    if (value) params.set(name, value);
  }
  const sortBy = String(data.get("sortBy") || "newest");
  if (sortBy !== "newest") params.set("sortBy", sortBy);
  return params;
}

function clearFilters() {
  for (const control of elements.filters.elements) {
    if (control instanceof HTMLSelectElement) {
      control.selectedIndex = 0;
    } else if (control instanceof HTMLInputElement) {
      control.value = "";
    }
  }
  loadRequests();
}

function setLoading(isLoading) {
  loading = isLoading;
  document.querySelector("#refresh").disabled = isLoading;
  document.querySelector("#retry").disabled = isLoading;
  elements.resultCount.textContent = isLoading ? "Loading requests" : elements.resultCount.textContent;
}

async function getVerificationStatus() {
  const response = await fetch(`${apiBase()}/api/guide/trip-requests/verification`, {
    headers: authHeaders()
  });
  if (response.status === 401 || response.status === 403) {
    throw new Error("Your session has expired. Sign in again to continue.");
  }
  if (!response.ok) throw new Error("Guide verification status could not be loaded.");
  return response.json();
}

async function loadRequests() {
  const loadId = ++latestLoadId;
  setLoading(true);
  elements.loadError.hidden = true;
  try {
    const query = requestParameters();
    const suffix = query.size ? `?${query.toString()}` : "";
    const response = await fetch(`${apiBase()}/api/guide/trip-requests${suffix}`, {
      headers: authHeaders()
    });
    if (response.status === 401 || response.status === 403) {
      throw new Error("Your session has expired. Sign in again to continue.");
    }
    if (!response.ok) throw new Error("The requests could not be loaded.");
    const result = await response.json();
    if (loadId !== latestLoadId) return;
    requests = result;
    renderRequests();
  } catch (error) {
    if (loadId !== latestLoadId) return;
    if (error.message.includes("Sign in again")) {
      showMessage("Sign in again", error.message);
      signOut();
      return;
    }
    elements.loadError.hidden = false;
    elements.requestList.replaceChildren();
    elements.emptyState.hidden = true;
    elements.resultCount.textContent = "Requests unavailable";
  } finally {
    if (loadId === latestLoadId) setLoading(false);
  }
}

function makeMeta(label, value, className = "") {
  const wrapper = document.createElement("div");
  wrapper.className = `request-meta ${className}`.trim();
  const labelNode = document.createElement("span");
  labelNode.className = "meta-label";
  labelNode.textContent = label;
  const valueNode = document.createElement("span");
  valueNode.className = "meta-value";
  valueNode.textContent = value;
  wrapper.append(labelNode, valueNode);
  return wrapper;
}

function makeRequestRow(request, index) {
  const row = document.createElement("article");
  row.className = "request-row";
  row.style.animationDelay = `${Math.min(index, 8) * 25}ms`;

  const place = document.createElement("div");
  place.className = "request-place";
  const destination = document.createElement("h3");
  destination.textContent = request.destination;
  const created = document.createElement("p");
  created.textContent = `Added ${formatDate(request.createdAt.slice(0, 10))}`;
  place.append(destination, created);

  const tripType = document.createElement("div");
  tripType.className = "request-meta row-type";
  const typeLabel = document.createElement("span");
  typeLabel.className = "meta-label";
  typeLabel.textContent = "Trip type";
  const typeChip = document.createElement("span");
  typeChip.className = "trip-chip";
  typeChip.textContent = request.tripType || "Other";
  tripType.append(typeLabel, typeChip);

  const dates = makeMeta("Travel dates", formatDateRange(request.startDate, request.endDate), "row-date");
  const budget = makeMeta("Budget", formatBudget(request.budget), "row-budget");
  budget.lastChild.classList.add("budget-value");
  const group = makeMeta("Group size", `${request.numberOfTravelers} ${request.numberOfTravelers === 1 ? "traveler" : "travelers"}`, "row-group");

  const actions = document.createElement("div");
  actions.className = "row-actions";
  const detailButton = document.createElement("button");
  detailButton.type = "button";
  detailButton.className = "detail-link";
  detailButton.textContent = "View details";
  detailButton.addEventListener("click", () => openDetails(request.id));
  const proposalButton = document.createElement("button");
  proposalButton.type = "button";
  proposalButton.className = "button button-dark proposal-button";
  proposalButton.textContent = "Submit proposal";
  proposalButton.addEventListener("click", () => explainProposal(request));
  actions.append(detailButton, proposalButton);

  row.append(place, tripType, dates, budget, group, actions);
  return row;
}

function renderRequests() {
  elements.requestList.replaceChildren(...requests.map(makeRequestRow));
  elements.loadError.hidden = true;
  elements.emptyState.hidden = requests.length > 0;
  elements.resultCount.textContent = `${requests.length} ${requests.length === 1 ? "request" : "requests"}`;
  if (requests.length === 0) {
    const hasFilters = [...requestParameters().keys()].some(key => key !== "sortBy");
    elements.emptyEyebrow.textContent = hasFilters ? "NO MATCHES YET" : "A QUIET BOARD";
    elements.emptyTitle.textContent = hasFilters
      ? "No trip requests match your filters."
      : "No open trip requests right now.";
    elements.emptyCopy.textContent = hasFilters
      ? "Try widening the dates or budget, or search a different destination."
      : "Check back later. New trips are added as tourists make plans.";
    elements.emptyReset.hidden = !hasFilters;
  }
}

function addFact(container, label, value) {
  const fact = document.createElement("div");
  fact.className = "detail-fact";
  const labelNode = document.createElement("span");
  labelNode.className = "meta-label";
  labelNode.textContent = label;
  const valueNode = document.createElement("span");
  valueNode.className = "meta-value";
  valueNode.textContent = value;
  fact.append(labelNode, valueNode);
  container.append(fact);
}

function addDetailSection(container, title, content) {
  const section = document.createElement("section");
  section.className = "detail-section";
  const heading = document.createElement("h3");
  heading.textContent = title;
  const paragraph = document.createElement("p");
  paragraph.textContent = content || "No notes provided.";
  section.append(heading, paragraph);
  container.append(section);
}

function renderDetails(request) {
  elements.detailId.textContent = `#${request.id}`;
  elements.detailContent.replaceChildren();

  const title = document.createElement("h2");
  title.id = "detail-title";
  title.className = "detail-title";
  title.textContent = request.destination;
  const subtitle = document.createElement("p");
  subtitle.className = "detail-subtitle";
  subtitle.textContent = `${request.tripType || "Other"} trip · Added ${formatDate(request.createdAt.slice(0, 10))}`;
  const facts = document.createElement("div");
  facts.className = "detail-facts";
  addFact(facts, "Travel dates", formatDateRange(request.startDate, request.endDate));
  addFact(facts, "Group size", `${request.numberOfTravelers} ${request.numberOfTravelers === 1 ? "traveler" : "travelers"}`);
  addFact(facts, "Total budget", formatBudget(request.budget));
  addFact(facts, "Request status", request.status);
  elements.detailContent.append(title, subtitle, facts);
  addDetailSection(elements.detailContent, "About this trip", request.description);
  addDetailSection(elements.detailContent, "Travel preferences", request.travelPreferences);

  const privacy = document.createElement("div");
  privacy.className = "detail-private";
  privacy.textContent = "Tourist contact details are hidden until a match.";
  elements.detailContent.append(privacy);

  const footer = document.createElement("div");
  footer.className = "detail-footer";
  const proposal = document.createElement("button");
  proposal.type = "button";
  proposal.className = "button button-dark";
  proposal.textContent = "Submit proposal";
  proposal.addEventListener("click", () => explainProposal(request));
  const close = document.createElement("button");
  close.type = "button";
  close.className = "button button-light";
  close.textContent = "Close details";
  close.addEventListener("click", () => elements.detailDialog.close());
  footer.append(proposal, close);
  elements.detailContent.append(footer);
  elements.detailDialog.showModal();
}

async function openDetails(requestId) {
  let request = requests.find(item => item.id === requestId);
  if (!request) return;

  try {
    const response = await fetch(`${apiBase()}/api/guide/trip-requests/${requestId}`, {
      headers: authHeaders()
    });
    if (response.status === 404) {
      elements.detailDialog.close();
      requests = requests.filter(item => item.id !== requestId);
      renderRequests();
      showMessage("Request no longer available", "This trip request has closed or been matched. The open list has been updated.");
      return;
    }
    if (response.status === 401 || response.status === 403) {
      elements.detailDialog.close();
      showMessage("Sign in again", "Your session has expired. Sign in again to continue.");
      signOut();
      return;
    }
    if (response.ok) {
      request = await response.json();
      requests = requests.map(item => item.id === requestId ? request : item);
    }
  } catch {
    request = requests.find(item => item.id === requestId) || request;
  }
  renderDetails(request);
}

function explainProposal(request) {
  if (!isVerified) {
    showMessage(
      "Guide verification required",
      "You can browse open requests, but only verified Guides can submit proposals. Verification document submission is not available in this project yet."
    );
    return;
  }
  showMessage(
    "Proposal submission is coming next",
    `“${request.destination}” is open. Proposal creation is the next workflow and is not available in this project yet.`
  );
}

async function signIn(event) {
  event.preventDefault();
  elements.loginError.hidden = true;
  const data = new FormData(elements.loginForm);
  const email = String(data.get("email") || "").trim();
  const password = String(data.get("password") || "");
  const base = String(data.get("apiUrl") || "").trim().replace(/\/+$/, "");
  localStorage.setItem(API_URL_KEY, base);
  elements.loginForm.querySelector("button[type=submit]").disabled = true;

  try {
    const response = await fetch(`${base}/api/auth/login`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password })
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(result.message || "Email or password is incorrect.");
    if ((result.role || "").toLowerCase() !== "guide") {
      throw new Error("This page is for Guide accounts. Sign in with a Guide account.");
    }
    sessionStorage.setItem(TOKEN_KEY, result.token);
    sessionStorage.setItem(ROLE_KEY, result.role);
    elements.loginForm.elements.password.value = "";
    await startBrowse();
  } catch (error) {
    elements.loginError.textContent = error.message || "Couldn't connect to TravelMatch. Check the API address and try again.";
    elements.loginError.hidden = false;
  } finally {
    elements.loginForm.querySelector("button[type=submit]").disabled = false;
  }
}

async function startBrowse() {
  isVerified = false;
  showBrowse();
  try {
    const status = await getVerificationStatus();
    isVerified = Boolean(status.isVerified);
    elements.verificationBanner.hidden = isVerified;
    if (!isVerified) {
      elements.verificationTitle.textContent = status.status === "Rejected"
        ? "Guide verification was rejected. You can still browse."
        : "You can browse while verification is pending.";
      elements.verificationCopy.textContent = "Only verified Guides can submit proposals. Verification document submission is not available yet.";
      elements.verificationHelp.hidden = false;
    }
  } catch (error) {
    if (error.message.includes("Sign in again")) {
      showMessage("Sign in again", error.message);
      signOut();
      return;
    }
    elements.verificationBanner.hidden = false;
    elements.verificationTitle.textContent = "Verification status unavailable.";
    elements.verificationCopy.textContent = "You can still browse; proposal submission requires verification.";
  }
  await loadRequests();
}

function signOut() {
  latestLoadId += 1;
  sessionStorage.removeItem(TOKEN_KEY);
  sessionStorage.removeItem(ROLE_KEY);
  requests = [];
  isVerified = false;
  showLogin();
}

elements.apiUrl.value = localStorage.getItem(API_URL_KEY) || elements.apiUrl.value;
elements.loginForm.addEventListener("submit", signIn);
elements.signOut.addEventListener("click", signOut);
elements.filters.addEventListener("submit", event => {
  event.preventDefault();
  const data = new FormData(elements.filters);
  const dateFrom = String(data.get("travelDateFrom") || "");
  const dateTo = String(data.get("travelDateTo") || "");
  const minBudget = Number(data.get("minBudget"));
  const maxBudget = Number(data.get("maxBudget"));
  if (dateFrom && dateTo && dateFrom > dateTo) {
    showMessage("Check the date range", "The ‘Travel from’ date must be before the ‘Travel to’ date.");
    return;
  }
  if (data.get("minBudget") && data.get("maxBudget") && minBudget > maxBudget) {
    showMessage("Check the budget range", "Minimum budget cannot be higher than maximum budget.");
    return;
  }
  loadRequests();
});
document.querySelector("#clear-filters").addEventListener("click", clearFilters);
document.querySelector("#refresh").addEventListener("click", loadRequests);
document.querySelector("#retry").addEventListener("click", loadRequests);
elements.emptyReset.addEventListener("click", clearFilters);
document.querySelector("#close-detail").addEventListener("click", () => elements.detailDialog.close());
document.querySelectorAll(".close-message").forEach(button => {
  button.addEventListener("click", () => elements.messageDialog.close());
});
elements.verificationHelp.addEventListener("click", () => showMessage(
  "Verification documents",
  "You may browse trip requests without verification. Document upload is not available in this project yet; only verified Guides can submit proposals."
));

if (sessionStorage.getItem(TOKEN_KEY) && (sessionStorage.getItem(ROLE_KEY) || "").toLowerCase() === "guide") {
  startBrowse();
} else {
  showLogin();
}