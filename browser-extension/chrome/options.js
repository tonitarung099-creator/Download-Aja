const interceptBox = document.getElementById("intercept");
const detectMediaBox = document.getElementById("detectMedia");
const cookiesBox = document.getElementById("cookies");
const excludedHostsBox = document.getElementById("excludedHosts");

function normalizeExcludedHosts(text) {
  return [...new Set(
    String(text || "")
      .split(/\r?\n/)
      .map(value => value.trim().toLowerCase())
      .filter(Boolean)
      .map(value => value.replace(/^https?:\/\//, "").split("/")[0])
  )];
}

chrome.storage.local.get([
  "interceptDownloads",
  "detectMedia",
  "sendSessionCookies",
  "excludedHosts"
]).then(values => {
  interceptBox.checked = Boolean(values.interceptDownloads);
  detectMediaBox.checked = values.detectMedia !== false;
  cookiesBox.checked = Boolean(values.sendSessionCookies);
  excludedHostsBox.value = Array.isArray(values.excludedHosts)
    ? values.excludedHosts.join("\n")
    : "";
});

interceptBox.addEventListener("change", () => {
  chrome.storage.local.set({ interceptDownloads: interceptBox.checked });
});

detectMediaBox.addEventListener("change", () => {
  chrome.storage.local.set({ detectMedia: detectMediaBox.checked });
});

cookiesBox.addEventListener("change", () => {
  chrome.storage.local.set({ sendSessionCookies: cookiesBox.checked });
});

excludedHostsBox.addEventListener("change", () => {
  const excludedHosts = normalizeExcludedHosts(excludedHostsBox.value);
  excludedHostsBox.value = excludedHosts.join("\n");
  chrome.storage.local.set({ excludedHosts });
});
