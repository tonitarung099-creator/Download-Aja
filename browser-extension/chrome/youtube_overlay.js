(() => {
  const BUTTON_ID = "download-aja-youtube-overlay";
  let lastUrl = location.href;
  let busy = false;

  function isYouTubeVideoUrl(value) {
    try {
      const url = new URL(value);
      const host = url.hostname.toLowerCase();

      if (host !== "youtube.com" && !host.endsWith(".youtube.com"))
        return false;

      if (url.pathname === "/watch")
        return Boolean(url.searchParams.get("v"));

      return /^\/(shorts|live|embed)\/[^/]+/.test(url.pathname);
    } catch {
      return false;
    }
  }

  function removeButton() {
    document.getElementById(BUTTON_ID)?.remove();
  }

  function findHost() {
    return document.querySelector("#movie_player")
      || document.querySelector("ytd-player")
      || document.querySelector("#player");
  }

  function ensureButton() {
    if (!isYouTubeVideoUrl(location.href)) {
      removeButton();
      return;
    }

    if (document.getElementById(BUTTON_ID))
      return;

    const host = findHost();
    if (!host)
      return;

    const computed = getComputedStyle(host);
    if (computed.position === "static")
      host.style.position = "relative";

    const button = document.createElement("button");
    button.id = BUTTON_ID;
    button.type = "button";
    button.textContent = "⬇ Download Aja";
    button.title = "Download video YouTube publik/non-DRM dengan Download Aja";

    Object.assign(button.style, {
      position: "absolute",
      top: "12px",
      right: "12px",
      zIndex: "2147483647",
      border: "1px solid rgba(255,255,255,.35)",
      borderRadius: "8px",
      padding: "8px 12px",
      background: "rgba(15,23,42,.92)",
      color: "white",
      font: "600 13px system-ui, -apple-system, Segoe UI, sans-serif",
      boxShadow: "0 3px 12px rgba(0,0,0,.28)",
      cursor: "pointer",
      backdropFilter: "blur(6px)"
    });

    button.addEventListener("mouseenter", () => {
      if (!busy)
        button.style.background = "rgba(37,99,235,.95)";
    });

    button.addEventListener("mouseleave", () => {
      if (!busy)
        button.style.background = "rgba(15,23,42,.92)";
    });

    button.addEventListener("click", async event => {
      event.preventDefault();
      event.stopPropagation();

      if (busy)
        return;

      busy = true;
      button.disabled = true;
      button.textContent = "Mengirim…";
      button.style.cursor = "default";

      try {
        const response = await chrome.runtime.sendMessage({
          type: "downloadYouTubePage",
          url: location.href
        });

        if (!response?.ok)
          throw new Error(response?.error || "Gagal mengirim ke Download Aja.");

        button.textContent = "✓ Terkirim";
        setTimeout(() => {
          if (!button.isConnected)
            return;
          busy = false;
          button.disabled = false;
          button.textContent = "⬇ Download Aja";
          button.style.cursor = "pointer";
          button.style.background = "rgba(15,23,42,.92)";
        }, 1600);
      } catch (error) {
        busy = false;
        button.disabled = false;
        button.textContent = "Coba lagi";
        button.style.cursor = "pointer";
        button.style.background = "rgba(185,28,28,.95)";
        button.title = String(error?.message || error);
      }
    });

    host.appendChild(button);
  }

  function refresh() {
    if (location.href !== lastUrl) {
      lastUrl = location.href;
      busy = false;
      removeButton();
    }

    ensureButton();
  }

  const observer = new MutationObserver(() => refresh());
  observer.observe(document.documentElement, {
    childList: true,
    subtree: true
  });

  window.addEventListener("yt-navigate-finish", refresh);
  window.addEventListener("popstate", refresh);

  refresh();
  setInterval(refresh, 1500);
})();
