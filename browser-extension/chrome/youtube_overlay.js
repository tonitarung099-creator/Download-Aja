(() => {
  const PANEL_ID = "download-aja-youtube-panel";
  const BUTTON_ID = "download-aja-youtube-overlay";
  const QUALITY_ID = "download-aja-youtube-quality";
  const QUALITY_OPTIONS = [
    ["best", "Best"],
    ["2160p", "2160p"],
    ["1440p", "1440p"],
    ["1080p", "1080p"],
    ["720p", "720p"],
    ["480p", "480p"],
    ["360p", "360p"]
  ];

  let lastUrl = location.href;
  let busy = false;

  function normalizeQuality(value) {
    const normalized = String(value || "").trim().toLowerCase();
    return QUALITY_OPTIONS.some(([key]) => key === normalized)
      ? normalized
      : "best";
  }

  function isYouTubeVideoUrl(value) {
    try {
      const url = new URL(value);
      const host = url.hostname.toLowerCase();

      if (host !== "youtube.com" && !host.endsWith(".youtube.com"))
        return false;

      if (url.pathname.toLowerCase() === "/watch")
        return Boolean(url.searchParams.get("v"));

      return /^\/(shorts|live|embed|v|clip)\/[^/]+/i.test(url.pathname);
    } catch {
      return false;
    }
  }

  function removePanel() {
    document.getElementById(PANEL_ID)?.remove();
  }

  function findHost() {
    return document.querySelector("#movie_player")
      || document.querySelector("ytd-player")
      || document.querySelector("#player");
  }

  async function syncQualitySelect(select) {
    const values = await chrome.storage.local.get("youtubeQuality");
    const profile = normalizeQuality(values.youtubeQuality);
    if (select.isConnected)
      select.value = profile;
  }

  async function ensurePanel() {
    if (!isYouTubeVideoUrl(location.href)) {
      removePanel();
      return;
    }

    if (document.getElementById(PANEL_ID))
      return;

    const host = findHost();
    if (!host)
      return;

    const computed = getComputedStyle(host);
    if (computed.position === "static")
      host.style.position = "relative";

    const panel = document.createElement("div");
    panel.id = PANEL_ID;

    Object.assign(panel.style, {
      position: "absolute",
      top: "12px",
      right: "12px",
      zIndex: "2147483647",
      display: "flex",
      alignItems: "stretch",
      gap: "6px",
      padding: "5px",
      border: "1px solid rgba(255,255,255,.30)",
      borderRadius: "10px",
      background: "rgba(15,23,42,.92)",
      boxShadow: "0 3px 14px rgba(0,0,0,.30)",
      backdropFilter: "blur(7px)",
      font: "600 13px system-ui, -apple-system, Segoe UI, sans-serif"
    });

    const select = document.createElement("select");
    select.id = QUALITY_ID;
    select.title = "Pilih kualitas maksimum YouTube";

    for (const [value, label] of QUALITY_OPTIONS) {
      const option = document.createElement("option");
      option.value = value;
      option.textContent = label;
      select.appendChild(option);
    }

    Object.assign(select.style, {
      border: "1px solid rgba(255,255,255,.28)",
      borderRadius: "7px",
      padding: "6px 8px",
      background: "rgba(30,41,59,.96)",
      color: "white",
      font: "600 12px system-ui, -apple-system, Segoe UI, sans-serif",
      outline: "none",
      cursor: "pointer"
    });

    select.addEventListener("click", event => event.stopPropagation());
    select.addEventListener("mousedown", event => event.stopPropagation());
    select.addEventListener("change", async event => {
      event.stopPropagation();
      const profile = normalizeQuality(select.value);
      select.value = profile;
      await chrome.storage.local.set({ youtubeQuality: profile });
    });

    const button = document.createElement("button");
    button.id = BUTTON_ID;
    button.type = "button";
    button.textContent = "⬇ Download";
    button.title = "Download video YouTube publik/non-DRM dengan Download Aja";

    Object.assign(button.style, {
      border: "1px solid rgba(255,255,255,.28)",
      borderRadius: "7px",
      padding: "6px 11px",
      background: "#2563eb",
      color: "white",
      font: "700 12px system-ui, -apple-system, Segoe UI, sans-serif",
      cursor: "pointer",
      whiteSpace: "nowrap"
    });

    button.addEventListener("mouseenter", () => {
      if (!busy)
        button.style.background = "#1d4ed8";
    });

    button.addEventListener("mouseleave", () => {
      if (!busy)
        button.style.background = "#2563eb";
    });

    button.addEventListener("click", async event => {
      event.preventDefault();
      event.stopPropagation();

      if (busy)
        return;

      busy = true;
      button.disabled = true;
      select.disabled = true;
      button.textContent = "Mengirim…";
      button.style.cursor = "default";

      try {
        const formatProfile = normalizeQuality(select.value);
        await chrome.storage.local.set({ youtubeQuality: formatProfile });

        const response = await chrome.runtime.sendMessage({
          type: "downloadYouTubePage",
          url: location.href,
          formatProfile
        });

        if (!response?.ok)
          throw new Error(response?.error || "Gagal mengirim ke Download Aja.");

        button.textContent = "✓ Terkirim";

        setTimeout(() => {
          if (!button.isConnected)
            return;

          busy = false;
          button.disabled = false;
          select.disabled = false;
          button.textContent = "⬇ Download";
          button.style.cursor = "pointer";
          button.style.background = "#2563eb";
        }, 1600);
      } catch (error) {
        busy = false;
        button.disabled = false;
        select.disabled = false;
        button.textContent = "Coba lagi";
        button.style.cursor = "pointer";
        button.style.background = "#b91c1c";
        button.title = String(error?.message || error);
      }
    });

    panel.addEventListener("click", event => event.stopPropagation());
    panel.addEventListener("dblclick", event => event.stopPropagation());
    panel.append(select, button);
    host.appendChild(panel);

    await syncQualitySelect(select);
  }

  function refresh() {
    if (location.href !== lastUrl) {
      lastUrl = location.href;
      busy = false;
      removePanel();
    }

    ensurePanel().catch(() => {});
  }

  const observer = new MutationObserver(() => refresh());
  observer.observe(document.documentElement, {
    childList: true,
    subtree: true
  });

  window.addEventListener("yt-navigate-finish", refresh);
  window.addEventListener("popstate", refresh);

  chrome.storage.onChanged.addListener((changes, areaName) => {
    if (areaName !== "local" || !changes.youtubeQuality)
      return;

    const select = document.getElementById(QUALITY_ID);
    if (select && !busy)
      select.value = normalizeQuality(changes.youtubeQuality.newValue);
  });

  refresh();
  setInterval(refresh, 1500);
})();
