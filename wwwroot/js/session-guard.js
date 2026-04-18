(function () {
    'use strict';

    var redirecting = false;

    function redirectToLogin() {
        if (redirecting) {
            return;
        }

        redirecting = true;
        var returnUrl = window.location.pathname + window.location.search + window.location.hash;
        window.location.replace('/Account/Login?returnUrl=' + encodeURIComponent(returnUrl));
    }

    async function checkSession() {
        try {
            var response = await fetch('/Account/Ping', {
                method: 'GET',
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });

            if (response.redirected) {
                redirectToLogin();
                return;
            }

            if (response.status === 401 || response.status === 403) {
                redirectToLogin();
            }
        } catch {
            // Ignore transient network failures; retry on the next resume event.
        }
    }

    window.addEventListener('pageshow', function (event) {
        if (event.persisted) {
            checkSession();
        }
    });

    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible') {
            checkSession();
        }
    });

    window.addEventListener('focus', checkSession);
}());
