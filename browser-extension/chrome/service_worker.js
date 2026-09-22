const HOST = "com.downloadaja.bridge";
const MAX_MEDIA_PER_TAB = 30;
const DIRECT_MEDIA_EXTENSIONS = new Set([
  "mp4", "m4v", "webm", "mov", "avi", "mkv", "wmv",
  "mp3", "m4a", "aac", "flac", "wav", "ogg", "opus", "wma"
]);
const STREAM_EXTENSIONS = new Set(["m3u8", "mpd"]);
const YOUTUBE_QUALITY_PROFILES = new Set([
  "best", "2160p", "1440p", "1080p", "720p", "480p", "360p"
]);

function normalizeYouTubeQuality(value) {
  const normalized = String(value || "").trim().toLowerCase();
  return YOUTUBE_QUALITY_PROFILES.has(normalized) ? normalized : "best";
}

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.contextMenus.removeAll();
  chrome.contextMenus.create({
    id: "downloadaja-link",
    title: "Download dengan Download Aja",
    contexts: ["link", "video", "audio"]
  });

  chrome.contextMenus.create({
    id: "downloadaja-youtube-page",
    title: "Download video YouTube dengan Download Aja",
    contexts: ["page"],
    documentUrlPatterns: [
      "*://*.youtube.com/watch*",
      "*://*.youtube.com/shorts/*",
      "*://*.youtube.com/live/*",
      "*://*.youtube.com/embed/*",
      "*://*.youtube.com/clip/*",
      "*://youtu.be/*",
      "*://*.youtube-nocookie.com/embed/*"
    ]
  });

  const current = await chrome.storage.local.get([
    "interceptDownloads",
    "sendSessionCookies",
    "detectMedia",
    "excludedHosts",
    "youtubeQuality"
  ]);

  const defaults = {};
  if (typeof current.interceptDownloads !== "boolean")
    defaults.interceptDownloads = false;
  if (typeof current.sendSessionCookies !== "boolean")
    defaults.sendSessionCookies = false;
  if (typeof current.detectMedia !== "boolean")
    defaults.detectMedia = true;
  if (!Array.isArray(current.excludedHosts))
    defaults.excludedHosts = [];
  if (!YOUTUBE_QUALITY_PROFILES.has(current.youtubeQuality))
    defaults.youtubeQuality = "best";

  if (Object.keys(defaults).length)
    await chrome.storage.local.set(defaults);
});

function isYouTubeUrl(value) {
  try {
    const host = new URL(value).hostname.toLowerCase();
    return host === "youtu.be"
      || host === "youtube.com"
      || host.endsWith(".youtube.com")
      || host === "youtube-nocookie.com"
      || host.endsWith(".youtube-nocookie.com");
  } catch {
    return false;
  }
}

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
      downloadable: true
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

function hostMatchesRule(host, rule) {
  const normalized = String(rule || "").trim().toLowerCase();
  if (!normalized)
    return false;

  const suffix = normalized.startsWith("*.") ? normalized.slice(2) : normalized;
  if (!suffix)
    return false;

  return host === suffix || host.endsWith("." + suffix);
}

async function isExcludedUrl(url) {
  try {
    const host = new URL(url).hostname.toLowerCase();
    const { excludedHosts = [] } = await chrome.storage.local.get("excludedHosts");
    return Array.isArray(excludedHosts)
      && excludedHosts.some(rule => hostMatchesRule(host, rule));
  } catch {
    return false;
  }
}

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

async function sendToDesktop(url, referrer = "", mediaKind = "", suggestedName = "", formatProfile = "") {
  if (!url)
    throw new Error("URL kosong.");

  // Mode YouTube 0.5.0 hanya untuk video publik; jangan kirim cookie sesi YouTube.
  const cookieHeader = isYouTubeUrl(url) ? "" : await getCookieHeader(url);
  const response = await chrome.runtime.sendNativeMessage(HOST, {
    type: "addDownload",
    url,
    referrer,
    userAgent: navigator.userAgent || "",
    cookieHeader,
    mediaKind,
    suggestedName,
    formatProfile: normalizeYouTubeQuality(formatProfile)
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

    if (message?.type === "downloadYouTubePage") {
      if (!isYouTubeVideoUrl(message.url)) {
        sendResponse({ ok: false, error: "Tab aktif bukan halaman video YouTube." });
        return;
      }

      try {
        const stored = await chrome.storage.local.get("youtubeQuality");
        const formatProfile = normalizeYouTubeQuality(
          message.formatProfile || stored.youtubeQuality
        );
        await chrome.storage.local.set({ youtubeQuality: formatProfile });
        await sendToDesktop(message.url, message.url || "", "", "", formatProfile);
        sendResponse({ ok: true });
      } catch (error) {
        sendResponse({ ok: false, error: String(error?.message || error) });
      }
      return;
    }

    if (message?.type === "downloadDetectedMedia") {
      const item = message.item;
      if (!item?.url) {
        sendResponse({ ok: false, error: "URL media tidak valid." });
        return;
      }

      try {
        await sendToDesktop(
          item.url,
          item.pageUrl || "",
          item.kind || "",
          item.name || ""
        );
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
  let url = "";

  if (info.menuItemId === "downloadaja-youtube-page") {
    if (info.pageUrl && isYouTubeVideoUrl(info.pageUrl))
      url = info.pageUrl;
  } else if (info.menuItemId === "downloadaja-link") {
    url = info.linkUrl || info.srcUrl || "";
  }

  if (!url)
    return;

  try {
    let formatProfile = "";
    if (info.menuItemId === "downloadaja-youtube-page" || isYouTubeVideoUrl(url)) {
      const stored = await chrome.storage.local.get("youtubeQuality");
      formatProfile = normalizeYouTubeQuality(stored.youtubeQuality);
    }

    await sendToDesktop(url, tab?.url || info.pageUrl || "", "", "", formatProfile);
  } catch (error) {
    console.error("Gagal mengirim download ke Download Aja:", error);
  }
});

chrome.downloads.onCreated.addListener(async item => {
  const { interceptDownloads = false } = await chrome.storage.local.get("interceptDownloads");
  if (!interceptDownloads || !item.url)
    return;

  if (await isExcludedUrl(item.url))
    return;

  try {
    await sendToDesktop(item.url, item.referrer || "");
    await chrome.downloads.cancel(item.id);
    await chrome.downloads.erase({ id: item.id });
  } catch (error) {
    console.error("Download tetap dilanjutkan oleh Chrome karena integrasi gagal:", error);
  }
});
