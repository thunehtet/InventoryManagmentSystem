(function () {
    'use strict';

    var root = document.getElementById('htmlRoot');
    if (!root) {
        return;
    }

    var savedLanguage = localStorage.getItem('se-lang') || 'my';
    root.setAttribute('data-lang', savedLanguage);
    root.setAttribute('lang', savedLanguage === 'my' ? 'my-MM' : 'en');
}());
