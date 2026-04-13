// ============================================================
//  LAYOUT — sidebar, topbar, user menu, bottom nav
// ============================================================

(function () {
    'use strict';

    const menuToggle   = document.getElementById('menuToggle');
    const sidebar      = document.getElementById('sidebar');
    const sidebarBrand = document.getElementById('sidebarBrand');
    const overlay      = document.getElementById('sidebarOverlay');
    const MOBILE_BP    = 900;

    if (!sidebar) return; // guard if layout element missing

    function isMobile() { return window.innerWidth <= MOBILE_BP; }

    function openSidebar() {
        sidebar.classList.remove('collapsed');
        if (isMobile() && overlay) overlay.classList.add('active');
    }

    function closeSidebar() {
        sidebar.classList.add('collapsed');
        if (overlay) overlay.classList.remove('active');
    }

    function toggleSidebar() {
        sidebar.classList.contains('collapsed') ? openSidebar() : closeSidebar();
    }

    if (menuToggle)   menuToggle.addEventListener('click', toggleSidebar);
    if (sidebarBrand) sidebarBrand.addEventListener('click', toggleSidebar);

    if (sidebarBrand) {
        sidebarBrand.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                toggleSidebar();
            }
        });
    }

    if (overlay) overlay.addEventListener('click', closeSidebar);

    // Close sidebar when a nav link is tapped on mobile
    document.querySelectorAll('.nav-link-modern').forEach(function (link) {
        link.addEventListener('click', function () {
            if (isMobile()) closeSidebar();
        });
    });

    function handleResize() {
        if (isMobile()) {
            closeSidebar();
        } else {
            sidebar.classList.remove('collapsed');
            if (overlay) overlay.classList.remove('active');
        }
    }

    window.addEventListener('resize', handleResize);
    handleResize();
    // Remove data-initial after first paint so transitions work for user interactions
    requestAnimationFrame(function () {
        sidebar.removeAttribute('data-initial');
    });

    // ── User dropdown ──────────────────────────────────────────
    window.toggleUserMenu = function () {
        var menu = document.getElementById('userMenu');
        if (!menu) return;
        // On mobile use fixed positioning anchored to the button's screen coordinates
        if (!menu.classList.contains('show') && window.innerWidth <= 768) {
            var btn = document.querySelector('.user-btn');
            if (btn) {
                var rect = btn.getBoundingClientRect();
                var menuWidth = 190;
                // Clamp so the menu never overflows the right edge (negative right)
                // and never overflows the left edge either
                var rightOffset = Math.max(0, window.innerWidth - rect.right);
                if (window.innerWidth - rightOffset - menuWidth < 8) {
                    rightOffset = window.innerWidth - menuWidth - 8;
                }
                menu.style.top   = (rect.bottom + 8) + 'px';
                menu.style.right = rightOffset + 'px';
                menu.style.left  = 'auto';
            }
        }
        menu.classList.toggle('show');
    };

    document.addEventListener('click', function (e) {
        if (!e.target.closest('.user-dropdown')) {
            var menu = document.getElementById('userMenu');
            if (menu) menu.classList.remove('show');
        }
    });

    // ── Context-aware topbar search ──────────────────────────���
    var searchInput = document.getElementById('topbarSearch');
    if (searchInput) {
        var ctrl   = (document.body.dataset.controller || '').toLowerCase();
        var act    = (document.body.dataset.action    || '').toLowerCase();

        var searchUrlMap = {
            'products':          '/Products/Index',
            'productvariants':   '/ProductVariants/Index',
            'stock':             act === 'inventory' ? '/Stock/Inventory' : '/Stock/Index',
            'sales':             '/Sales/Index',
            'textile':           '/Textile/Index',
            'cashtransaction':   '/CashTransaction/Index',
        };

        var searchUrl = searchUrlMap[ctrl];

        if (searchUrl) {
            searchInput.addEventListener('keydown', function (e) {
                if (e.key === 'Enter') {
                    var q = searchInput.value.trim();
                    window.location.href = q
                        ? searchUrl + '?search=' + encodeURIComponent(q)
                        : searchUrl;
                }
            });
            // Show a clear button when there is an active search value
            if (searchInput.value) {
                searchInput.addEventListener('input', function () {
                    if (!searchInput.value.trim()) {
                        window.location.href = searchUrl;
                    }
                });
            }
        }
    }

    // ── Bottom nav active state ────────────────────────────────
    var currentPath = window.location.pathname.toLowerCase();
    document.querySelectorAll('.mob-nav-item[href]').forEach(function (a) {
        var href = a.getAttribute('href').toLowerCase().replace('/index', '');
        if (href.length > 1 && currentPath.startsWith(href)) {
            a.classList.add('mob-nav-active');
        }
    });

}());
