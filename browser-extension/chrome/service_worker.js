const HOST = "com.downloadaja.bridge";

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: "downloadaja-link",
    title: "Download dengan Download Aja",
    contexts: ["link", "video", "audio"]
  });
  chrome.storage.local.set({ interceptDownloads: false });
});

async function sendToDesktop(url, referrer = "") {
  if (!url) return;
  return chrome.runtime.sendNativeMessage(HOST, {
    type: "addDownload",
    url,
    referrer
  });
}

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  const url = info.linkUrl || info.srcUrl;
  if (!url) return;
  try {
    await sendToDesktop(url, tab?.url || "");
  } catch (error) {
    console.error("Download Aja native host belum tersedia:", error);
  }
});

chrome.downloads.onCreated.addListener(async item => {
  const { interceptDownloads = false } = await chrome.storage.local.get("interceptDownloads");
  if (!interceptDownloads || !item.url) return;

  try {
    await sendToDesktop(item.url, item.referrer || "");
    await chrome.downloads.cancel(item.id);
    await chrome.downloads.erase({ id: item.id });
  } catch (error) {
    console.error("Gagal mengalihkan download ke Download Aja:", error);
  }
});
