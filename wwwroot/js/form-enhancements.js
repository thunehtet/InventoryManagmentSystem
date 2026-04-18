(function () {
    'use strict';

    function initConfirmForms() {
        document.querySelectorAll('form[data-confirm-message]').forEach(function (form) {
            form.addEventListener('submit', function (event) {
                var message = form.getAttribute('data-confirm-message');
                if (!message) {
                    return;
                }

                if (!window.confirm(message)) {
                    event.preventDefault();
                }
            });
        });
    }

    function initToggleCards() {
        document.querySelectorAll('[data-toggle-target]').forEach(function (card) {
            card.addEventListener('click', function () {
                var targetId = card.getAttribute('data-toggle-target');
                if (!targetId) {
                    return;
                }

                var input = document.getElementById(targetId);
                if (input) {
                    input.click();
                }
            });
        });

        document.querySelectorAll('[data-toggle-stop]').forEach(function (element) {
            element.addEventListener('click', function (event) {
                event.stopPropagation();
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initConfirmForms();
        initToggleCards();
    });
}());
