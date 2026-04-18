(() => {
    const btn         = document.getElementById('btnGenerateReceiptLink');
    const initArea    = document.getElementById('shareInitArea');
    const confirmArea = document.getElementById('shareConfirmArea');
    const revealArea  = document.getElementById('shareRevealArea');
    const limitArea   = document.getElementById('shareLimitArea');

    if (!btn) return;

    const existingUrl  = btn.dataset.existingUrl  || '';
    const hasLimit     = btn.dataset.hasLimit     === 'true';
    const limitReached = btn.dataset.limitReached === 'true';
    const generateUrl  = btn.dataset.generateUrl;
    const confirmMsg   = btn.dataset.confirmMsg;
    const limitMsg     = btn.dataset.limitMsg;

    function show(el)  { el.classList.remove('d-none'); }
    function hide(el)  { el.classList.add('d-none'); }

    function getCsrf() {
        return document.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    }

    function showReveal(url) {
        hide(initArea);
        hide(confirmArea);

        document.getElementById('shareLinkDisplay').textContent = url;
        document.getElementById('linkOpenReceipt').href = url;
        document.getElementById('btnShareReceipt').dataset.shareUrl = url;
        document.getElementById('btnCopyLink').dataset.copyText = url;
        document.getElementById('refreshCsrf').value = getCsrf();

        show(revealArea);
    }

    function showLocked(msg) {
        hide(initArea);
        hide(confirmArea);
        document.getElementById('shareLimitMsg').textContent = msg;
        show(limitArea);
    }

    async function fetchAndReveal() {
        btn.disabled = true;
        btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';

        try {
            const res  = await fetch(generateUrl, {
                method: 'POST',
                headers: { 'RequestVerificationToken': getCsrf() }
            });
            const data = await res.json();

            if (data.success) {
                if (data.max != null) {
                    const usageText = document.getElementById('shareUsageText');
                    if (usageText) usageText.textContent = `${data.used} / ${data.max}`;
                }
                showReveal(data.url);
            } else if (data.limitReached) {
                showLocked(limitMsg);
            }
        } catch {
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-share-fill me-1"></i>';
        }
    }

    btn.addEventListener('click', () => {
        if (existingUrl) {
            showReveal(existingUrl);
            return;
        }
        if (limitReached) {
            showLocked(limitMsg);
            return;
        }
        if (hasLimit) {
            hide(initArea);
            document.getElementById('shareConfirmMsg').textContent = confirmMsg;
            show(confirmArea);
            return;
        }
        fetchAndReveal();
    });

    document.getElementById('btnConfirmYes')?.addEventListener('click', () => {
        hide(confirmArea);
        fetchAndReveal();
    });

    document.getElementById('btnConfirmNo')?.addEventListener('click', () => {
        hide(confirmArea);
        show(initArea);
    });
})();
