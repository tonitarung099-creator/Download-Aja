const interceptBox = document.getElementById("intercept");
const detectMediaBox = document.getElementById("detectMedia");
const cookiesBox = document.getElementById("cookies");

chrome.storage.local.get([
  "interceptDownloads",
  "detectMedia",
  "sendSessionCookies"
]).then(values => {
  interceptBox.checked = Boolean(values.interceptDownloads);
  detectMediaBox.checked = values.detectMedia !== false;
  cookiesBox.checked = Boolean(values.sendSessionCookies);
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
