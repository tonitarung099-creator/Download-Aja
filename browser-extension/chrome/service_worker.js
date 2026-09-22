const HOST = "com.downloadaja.bridge";

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.contextMenus.removeAll();
  chrome.contextMenus.create({
    id: "downloadaja-link",
    title: "Download dengan Download Aja",
    contexts: ["link", "video", "audio"]
  });

  const current = await chrome.storage.local.get([
    "interceptDownloads",
    "sendSessionCookies"
  ]);

  const defaults = {};
  if (typeof current.interceptDownloads !== "boolean")
    defaults.interceptDownloads = false;
  if (typeof current.sendSessionCookies !== "boolean")
    defaults.sendSessionCookies = false;

  if (Object.keys(defaults).length)
    await chrome.storage.local.set(defaults);
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
