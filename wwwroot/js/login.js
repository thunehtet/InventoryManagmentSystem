// ============================================================
//  LOGIN — password visibility toggle
// ============================================================

(function () {
    'use strict';

    var toggleBtn = document.getElementById('togglePw');
    var pwInput   = document.querySelector('input[name="Password"]');
    var eyeIcon   = document.getElementById('pwEyeIcon');

    if (!toggleBtn || !pwInput) return;

    toggleBtn.addEventListener('click', function () {
        var isText = pwInput.type === 'text';
        pwInput.type    = isText ? 'password' : 'text';
        eyeIcon.className = isText ? 'bi bi-eye' : 'bi bi-eye-slash';
    });

}());
