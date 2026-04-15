(() => {
    async function copyText(value) {
        if (!value) return;

        try {
            await navigator.clipboard.writeText(value);
            return true;
        } catch {
            const input = document.createElement("input");
            input.value = value;
            document.body.appendChild(input);
            input.select();
            document.execCommand("copy");
            document.body.removeChild(input);
            return true;
        }
    }

    async function shareUrl(url, title) {
        if (!url) return;

        if (navigator.share) {
            try {
                await navigator.share({ title: title || "Share", url });
                return;
            } catch {
                // Fall back to copy.
            }
        }

        await copyText(url);
    }

    document.addEventListener("click", async (event) => {
        const copyButton = event.target.closest("[data-copy-text]");
        if (copyButton) {
            const ok = await copyText(copyButton.getAttribute("data-copy-text"));
            if (ok) {
                copyButton.classList.add("is-copied");
                window.setTimeout(() => copyButton.classList.remove("is-copied"), 1200);
            }
            return;
        }

        const shareButton = event.target.closest("[data-share-url]");
        if (shareButton) {
            await shareUrl(
                shareButton.getAttribute("data-share-url"),
                shareButton.getAttribute("data-share-title"));
        }
    });
})();
