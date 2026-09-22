const box = document.getElementById("intercept");

chrome.storage.local.get("interceptDownloads").then(({ interceptDownloads }) => {
  box.checked = Boolean(interceptDownloads);
});

box.addEventListener("change", () => {
  chrome.storage.local.set({ interceptDownloads: box.checked });
});
