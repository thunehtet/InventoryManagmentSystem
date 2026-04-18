(function () {
    'use strict';

    var el = document.querySelector('#successModal[data-auto-show="true"]');
    if (!el || typeof bootstrap === 'undefined') {
        return;
    }

    new bootstrap.Modal(el, { backdrop: 'static', keyboard: false }).show();
}());
