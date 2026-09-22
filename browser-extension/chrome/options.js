const interceptBox = document.getElementById("intercept");
const cookiesBox = document.getElementById("cookies");

chrome.storage.local.get(["interceptDownloads", "sendSessionCookies"]).then(values => {
  interceptBox.checked = Boolean(values.interceptDownloads);
  cookiesBox.checked = Boolean(values.sendSessionCookies);
});

interceptBox.addEventListener("change", () => {
  chrome.storage.local.set({ interceptDownloads: interceptBox.checked });
});

cookiesBox.addEventListener("change", () => {
  chrome.storage.local.set({ sendSessionCookies: cookiesBox.checked });
});
