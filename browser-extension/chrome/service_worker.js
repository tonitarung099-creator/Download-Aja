const HOST = "com.downloadaja.bridge";
const MAX_MEDIA_PER_TAB = 30;
const DIRECT_MEDIA_EXTENSIONS = new Set([
  "mp4", "m4v", "webm", "mov", "avi", "mkv", "wmv",
  "mp3", "m4a", "aac", "flac", "wav", "ogg", "opus", "wma"
]);
const STREAM_EXTENSIONS = new Set(["m3u8", "mpd"]);

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.contextMenus.removeAll();
  chrome.contextMenus.create({
    id: "downloadaja-link",
    title: "Download dengan Download Aja",
    contexts: ["link", "video", "audio"]
  });

  const current = await chrome.storage.local.get([
    "interceptDownloads",
    "sendSessionCookies",
    "detectMedia"
  ]);

  const defaults = {};
  if (typeof current.interceptDownloads !== "boolean")
    defaults.interceptDownloads = false;
  if (typeof current.sendSessionCookies !== "boolean")
    defaults.sendSessionCookies = false;
  if (typeof current.detectMedia !== "boolean")
    defaults.detectMedia = true;

  if (Object.keys(defaults).length)
    await chrome.storage.local.set(defaults);
});

function getExtension(url) {
  try {
    const pathname = new URL(url).pathname.toLowerCase();
    const last = pathname.split("/").pop() || "";
    const dot = last.lastIndexOf(".");
    return dot >= 0 ? last.slice(dot + 1) : "";
  } catch {
    return "";
  }
}

function responseHeader(headers, name) {
  const target = name.toLowerCase();
  const header = (headers || []).find(item => item.name?.toLowerCase() === target);
  return header?.value || "";
}

function classifyMedia(details) {
  if (!details?.url || details.tabId < 0)
    return null;

  const extension = getExtension(details.url);
  const contentType = responseHeader(details.responseHeaders, "content-type")
    .split(";")[0]
    .trim()
    .toLowerCase();

  const isStream = STREAM_EXTENSIONS.has(extension)
    || contentType === "application/vnd.apple.mpegurl"
    || contentType === "application/x-mpegurl"
    || contentType === "application/dash+xml";

  if (isStream) {
    return {
      kind: extension === "mpd" || contentType === "application/dash+xml" ? "dash" : "hls",
      downloadable: false
    };
  }

  const mediaByMime = contentType.startsWith("video/") || contentType.startsWith("audio/");
  const mediaByExtension = DIRECT_MEDIA_EXTENSIONS.has(extension);

  if (!mediaByMime && !mediaByExtension)
    return null;

  return {
    kind: contentType.startsWith("audio/") ? "audio" : "video",
    downloadable: true
  };
}

function candidateName(url) {
  try {
    const pathname = new URL(url).pathname;
    const name = decodeURIComponent(pathname.split("/").pop() || "");
    return name || "media";
  } catch {
    return "media";
  }
}

async function mediaStorageKey(tabId) {
  return `media:${tabId}`;
}

async function getMedia(tabId) {
  const key = await mediaStorageKey(tabId);
  const data = await chrome.storage.session.get(key);
  return Array.isArray(data[key]) ? data[key] : [];
}

async function setMedia(tabId, items) {
  const key = await mediaStorageKey(tabId);
  await chrome.storage.session.set({ [key]: items.slice(0, MAX_MEDIA_PER_TAB) });

  try {
    await chrome.action.setBadgeText({
      tabId,
      text: items.length ? String(Math.min(items.length, 99)) : ""
    });
  } catch {
  }
}

async function rememberMedia(details) {
  const { detectMedia = true } = await chrome.storage.local.get("detectMedia");
  if (!detectMedia)
    return;

  const classification = classifyMedia(details);
  if (!classification)
    return;

  const contentLength = Number.parseInt(responseHeader(details.responseHeaders, "content-length"), 10);
  const candidate = {
    id: crypto.randomUUID(),
    url: details.url,
    name: candidateName(details.url),
    mime: responseHeader(details.responseHeaders, "content-type").split(";")[0].trim(),
    size: Number.isFinite(contentLength) && contentLength > 0 ? contentLength : 0,
    pageUrl: details.documentUrl || details.initiator || "",
    kind: classification.kind,
    downloadable: classification.downloadable,
    detectedAt: Date.now()
  };

  const items = await getMedia(details.tabId);
  const withoutDuplicate = items.filter(item => item.url !== candidate.url);
  withoutDuplicate.unshift(candidate);
  await setMedia(details.tabId, withoutDuplicate);
}

chrome.webRequest.onHeadersReceived.addListener(
  details => {
    rememberMedia(details).catch(error => {
      console.debug("Download Aja media detector:", error);
    });
  },
  { urls: ["<all_urls>"], types: ["media", "xmlhttprequest", "other"] },
  ["responseHeaders"]
);

chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
  if (changeInfo.status !== "loading")
    return;

  const key = `media:${tabId}`;
  chrome.storage.session.remove(key).catch(() => {});
  chrome.action.setBadgeText({ tabId, text: "" }).catch(() => {});
});

chrome.tabs.onRemoved.addListener(tabId => {
  chrome.storage.session.remove(`media:${tabId}`).catch(() => {});
});

async function getCookieHeader(url) {
  const { sendSessionCookies = false } = await chrome.storage.local.get("sendSessionCookies");
  if (!sendSessionCookies)
    return "";

  try {
    const cookies = await chrome.cookies.getAll({ url });
    return cookies
      .filter(cookie => cookie.name)
      .map(cookie => `${cookie.name}=${cookie.value}`)
      .join("; ");
  } catch (error) {
    console.warn("Download Aja tidak dapat membaca cookie untuk URL ini:", error);
    return "";
  }
}

async function sendToDesktop(url, referrer = "") {
  if (!url)
    throw new Error("URL kosong.");

  const cookieHeader = await getCookieHeader(url);
  const response = await chrome.runtime.sendNativeMessage(HOST, {
    type: "addDownload",
    url,
    referrer,
    userAgent: navigator.userAgent || "",
    cookieHeader
  });

  if (!response?.ok)
    throw new Error(response?.error || "Download Aja tidak merespons.");

  return response;
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  (async () => {
    if (message?.type === "getMediaForTab") {
      sendResponse({ ok: true, items: await getMedia(message.tabId) });
      return;
    }

    if (message?.type === "clearMediaForTab") {
      await setMedia(message.tabId, []);
      sendResponse({ ok: true });
      return;
    }

    if (message?.type === "downloadDetectedMedia") {
      const item = message.item;
      if (!item?.url || item.downloadable === false) {
        sendResponse({
          ok: false,
          error: "Stream HLS/DASH belum didukung oleh engine media."
        });
        return;
      }

      try {
        await sendToDesktop(item.url, item.pageUrl || "");
        sendResponse({ ok: true });
      } catch (error) {
        sendResponse({ ok: false, error: String(error?.message || error) });
      }
      return;
    }

    sendResponse({ ok: false, error: "Pesan extension tidak dikenal." });
  })().catch(error => {
    sendResponse({ ok: false, error: String(error?.message || error) });
  });

  return true;
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  const url = info.linkUrl || info.srcUrl;
  if (!url) return;

  try {
    await sendToDesktop(url, tab?.url || "");
  } catch (error) {
    console.error("Gagal mengirim download ke Download Aja:", error);
  }
});

chrome.downloads.onCreated.addListener(async item => {
  const { interceptDownloads = false } = await chrome.storage.local.get("interceptDownloads");
  if (!interceptDownloads || !item.url)
    return;

  try {
    await sendToDesktop(item.url, item.referrer || "");
    await chrome.downloads.cancel(item.id);
    await chrome.downloads.erase({ id: item.id });
  } catch (error) {
    console.error("Download tetap dilanjutkan oleh Chrome karena integrasi gagal:", error);
  }
});
