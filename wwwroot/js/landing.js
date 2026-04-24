(function () {
    'use strict';

    function setLanguage(language) {
        var root = document.getElementById('htmlRoot');
        if (!root) {
            return;
        }

        root.setAttribute('data-lang', language);
        root.setAttribute('lang', language === 'my' ? 'my-MM' : 'en');
        localStorage.setItem('se-lang', language);

        var culture = language === 'my' ? 'my-MM' : 'en';
        document.cookie = '.AspNetCore.Culture=c%3D' + culture + '%7Cuic%3D' + culture
            + '; path=/; max-age=31536000; SameSite=Strict';
    }

    function toggleLanguage() {
        var root = document.getElementById('htmlRoot');
        if (!root) {
            return;
        }

        var currentLanguage = root.getAttribute('data-lang') || 'my';
        setLanguage(currentLanguage === 'en' ? 'my' : 'en');
    }

    function initLanguageToggle() {
        var languageButton = document.getElementById('langBtn');
        if (languageButton) {
            languageButton.addEventListener('click', toggleLanguage);
        }
    }

    function initNavbarScrollEffect() {
        var navbar = document.getElementById('navbar');
        if (!navbar) {
            return;
        }

        function syncNavbarState() {
            navbar.classList.toggle('scrolled', window.scrollY > 40);
        }

        window.addEventListener('scroll', syncNavbarState, { passive: true });
        syncNavbarState();
    }

    function initMobileMenuClose() {
        document.querySelectorAll('.nav-mobile a').forEach(function (link) {
            link.addEventListener('click', function () {
                var toggle = document.getElementById('nav-toggle');
                if (toggle) {
                    toggle.checked = false;
                }
            });
        });
    }

    function initFadeUpAnimations() {
        var fadeElements = document.querySelectorAll('.fade-up');
        if (!fadeElements.length) {
            return;
        }

        if (!('IntersectionObserver' in window)) {
            fadeElements.forEach(function (element) {
                element.classList.add('visible');
            });
            return;
        }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) {
                    return;
                }

                entry.target.classList.add('visible');
                observer.unobserve(entry.target);
            });
        }, { threshold: 0.10, rootMargin: '0px 0px -40px 0px' });

        fadeElements.forEach(function (element) {
            observer.observe(element);
        });
    }

    function scrollToContactSection() {
        if (window.location.hash !== '#contact') {
            return;
        }

        var contactSection = document.getElementById('contact');
        if (!contactSection) {
            return;
        }

        requestAnimationFrame(function () {
            contactSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        initLanguageToggle();
        initNavbarScrollEffect();
        initMobileMenuClose();
        initFadeUpAnimations();
        scrollToContactSection();
    });
}());
