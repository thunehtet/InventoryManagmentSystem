(function () {
    'use strict';

    var root = document.getElementById('htmlRoot');
    if (!root) {
        return;
    }

    var savedLanguage = localStorage.getItem('se-lang') || 'en';
    root.setAttribute('data-lang', savedLanguage);
}());
