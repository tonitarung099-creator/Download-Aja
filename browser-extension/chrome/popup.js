const list = document.getElementById("list");
const clearButton = document.getElementById("clear");
const message = document.getElementById("message");
const youtubeCard = document.getElementById("youtubeCard");
const downloadPageButton = document.getElementById("downloadPage");
let tabId = null;
let currentTabUrl = "";

function isYouTubeVideoUrl(value) {
  try {
    const url = new URL(value);
    const host = url.hostname.toLowerCase();
    const path = url.pathname;

    if (host === "youtu.be")
      return path.split("/").filter(Boolean).length >= 1;

    const isYouTube = host === "youtube.com"
      || host.endsWith(".youtube.com")
      || host === "youtube-nocookie.com"
      || host.endsWith(".youtube-nocookie.com");

    if (!isYouTube)
      return false;

    if (path.toLowerCase() === "/watch")
      return Boolean(url.searchParams.get("v"));

    return /^\/(shorts|live|embed|v|clip)\/[^/]+/i.test(path);
  } catch {
    return false;
  }
}

function formatBytes(bytes) {
  if (!bytes) return "ukuran tidak diketahui";
  const units = ["B", "KB", "MB", "GB"];
  let value = bytes;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit++;
  }
  return `${value.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`;
}

function showMessage(text) {
  message.textContent = text;
  message.style.display = text ? "block" : "none";
}

function escapeText(value) {
  const span = document.createElement("span");
  span.textContent = value ?? "";
  return span.innerHTML;
}

function render(items) {
  if (!items.length) {
    list.innerHTML = '<div class="empty">Belum ada media yang terdeteksi pada tab ini.<br>Putar video/audio lalu buka popup lagi.</div>';
    return;
  }

  list.innerHTML = items.map((item, index) => {
    const stream = item.kind === "hls" || item.kind === "dash";
    const typeText = stream ? item.kind.toUpperCase() : (item.mime || item.kind || "media");
    return `
      <section class="item">
        <div class="name" title="${escapeText(item.url)}">${escapeText(item.name || "media")}</div>
        <div class="meta">
          <span>${escapeText(typeText)}</span>
          <span>${formatBytes(item.size)}</span>
          ${stream ? '<span class="stream">HLS/DASH via FFmpeg</span>' : ''}
        </div>
        <div class="row">
          <button class="primary download" data-index="${index}">
            Download
          </button>
        </div>
      </section>`;
  }).join("");

  document.querySelectorAll(".download").forEach(button => {
    button.addEventListener("click", async () => {
      const item = items[Number(button.dataset.index)];
      if (!item) return;

      button.disabled = true;
      button.textContent = "Mengirim…";
      const response = await chrome.runtime.sendMessage({
        type: "downloadDetectedMedia",
        item
      });

      if (response?.ok) {
        button.textContent = "Terkirim";
        showMessage("");
      } else {
        button.disabled = false;
        button.textContent = "Download";
        showMessage(response?.error || "Gagal mengirim ke Download Aja.");
      }
    });
  });
}

async function load() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  tabId = tab?.id ?? null;
  currentTabUrl = tab?.url || "";

  if (isYouTubeVideoUrl(currentTabUrl)) {
    youtubeCard.style.display = "block";
  } else {
    youtubeCard.style.display = "none";
  }

  if (tabId === null) {
    render([]);
    return;
  }

  const response = await chrome.runtime.sendMessage({
    type: "getMediaForTab",
    tabId
  });

  render(response?.items || []);
}

downloadPageButton.addEventListener("click", async () => {
  if (!isYouTubeVideoUrl(currentTabUrl))
    return;

  downloadPageButton.disabled = true;
  downloadPageButton.textContent = "Mengirim…";
  showMessage("");

  try {
    const response = await chrome.runtime.sendMessage({
      type: "downloadYouTubePage",
      url: currentTabUrl
    });

    if (response?.ok) {
      downloadPageButton.textContent = "Terkirim";
    } else {
      downloadPageButton.disabled = false;
      downloadPageButton.textContent = "Download video halaman ini";
      showMessage(response?.error || "Gagal mengirim URL YouTube ke Download Aja.");
    }
  } catch (error) {
    downloadPageButton.disabled = false;
    downloadPageButton.textContent = "Download video halaman ini";
    showMessage(String(error?.message || error));
  }
});

clearButton.addEventListener("click", async () => {
  if (tabId === null) return;
  await chrome.runtime.sendMessage({ type: "clearMediaForTab", tabId });
  render([]);
  showMessage("");
});

load().catch(error => {
  showMessage(String(error?.message || error));
  render([]);
});
