// ============================================================
//  SALE CREATE - server-filtered variant picker + cart
// ============================================================

(function () {
    'use strict';

    var filterTabs = document.getElementById('filterTabs');
    var variantGrid = document.getElementById('variantGrid');
    var variantSearch = document.getElementById('variantSearch');
    var variantCount = document.getElementById('variantCount');
    var cartList = document.getElementById('cartList');
    var emptyCart = document.getElementById('emptyCart');
    var cartBadge = document.getElementById('cartBadge');
    var hiddenInputs = document.getElementById('hiddenInputs');
    var sumRevenue = document.getElementById('sumRevenue');
    var sumFinal = document.getElementById('sumFinal');
    var sumDiscount = document.getElementById('sumDiscount');
    var discountRow = document.getElementById('discountRow');
    var discountInput = document.getElementById('discountInput');
    var sumCost = document.getElementById('sumCost');
    var sumProfit = document.getElementById('sumProfit');
    var clientError = document.getElementById('clientError');
    var saleForm = document.getElementById('saleForm');

    if (!saleForm) return;

    var str = {
        loadFailed: saleForm.dataset.loadFailed,
        all: saleForm.dataset.all,
        noProducts: saleForm.dataset.noProducts,
        outOfStock: saleForm.dataset.outOfStock,
        left: saleForm.dataset.left,
        price: saleForm.dataset.price,
        cartRequired: saleForm.dataset.cartRequired
    };
    var isStaff = saleForm.dataset.isStaff === 'true';

    var pageSize = 48;
    var currentPage = 1;
    var hasMore = false;
    var isLoading = false;
    var activeCategory = str.all || 'All';
    var categories = [];
    var currentItems = [];
    var variantMap = {};
    var cart = {};
    var searchDebounce = null;
    var lastActiveCartItemId = null;

    function loadVariants(reset) {
        if (isLoading) return;
        isLoading = true;

        if (reset) {
            currentPage = 1;
            hasMore = false;
            currentItems = [];
            if (variantGrid) {
                variantGrid.innerHTML =
                    '<div class="sc-loading"><div class="spinner-border spinner-border-sm" role="status"></div>' +
                    '<span>' + escHtml(str.loadingProducts || 'Loading products...') + '</span></div>';
            }
        }

        var params = new URLSearchParams();
        params.set('page', String(currentPage));
        params.set('pageSize', String(pageSize));

        var q = variantSearch ? variantSearch.value.trim() : '';
        if (q) params.set('q', q);
        if (activeCategory && activeCategory !== (str.all || 'All')) {
            params.set('category', activeCategory);
        }

        fetch('/Sales/GetVariants?' + params.toString())
            .then(function (res) {
                if (!res.ok) throw new Error('Network error');
                return res.json();
            })
            .then(function (data) {
                categories = data.categories || [];
                hasMore = !!data.hasMore;

                renderTabs();

                var items = data.items || [];
                items.forEach(function (item) {
                    variantMap[item.id] = item;
                });

                currentItems = reset ? items.slice() : currentItems.concat(items);
                if (variantCount) variantCount.textContent = String(data.total || 0);
                renderGrid();
            })
            .catch(function () {
                if (variantGrid) {
                    variantGrid.innerHTML =
                        '<div class="sc-loading"><i class="bi bi-exclamation-circle text-danger"></i>' +
                        '<span>' + escHtml(str.loadFailed || 'Failed to load products. Please refresh.') + '</span></div>';
                }
            })
            .finally(function () {
                isLoading = false;
            });
    }

    function renderTabs() {
        if (!filterTabs) return;

        var allLabel = str.all || 'All';
        var tabItems = [allLabel].concat(categories);

        filterTabs.innerHTML = tabItems.map(function (name) {
            return '<button type="button" class="sc-filter-tab' +
                (name === activeCategory ? ' active' : '') +
                '" data-filter="' + escAttr(name) + '">' +
                escHtml(name) + '</button>';
        }).join('');

        filterTabs.querySelectorAll('.sc-filter-tab').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var next = this.getAttribute('data-filter');
                if (next === activeCategory) return;
                activeCategory = next;
                loadVariants(true);
            });
        });
    }

    function renderGrid() {
        if (!variantGrid) return;

        if (!currentItems.length) {
            variantGrid.innerHTML =
                '<div class="sc-loading"><i class="bi bi-search"></i><span>' +
                escHtml(str.noProducts || 'No products found') + '</span></div>';
            return;
        }

        var cardsHtml = currentItems.map(function (v) {
            var inCart = cart[v.id];
            var noStock = v.stock <= 0;
            var initial = v.productName.charAt(0).toUpperCase();
            var accentClass = colorClass(v.color);
            var qtyBadge = inCart ? '<span class="sc-qty-badge">' + inCart.qty + '</span>' : '';
            var stockCls = noStock ? 'sc-stock-none' : v.stock <= 5 ? 'sc-stock-low' : 'sc-stock-ok';
            var stockTxt = noStock
                ? escHtml(str.outOfStock || 'Out of stock')
                : v.stock + ' ' + escHtml(str.left || 'left');
            var cardCls = 'sc-card' + (inCart ? ' sc-card--selected' : '') + (noStock ? ' sc-card--disabled' : '');
            var onclick = noStock ? '' : 'data-add="' + v.id + '"';

            return '<div class="' + cardCls + '" ' + onclick + ' data-id="' + v.id + '">' +
                '<div class="sc-avatar ' + accentClass + '">' +
                initial + qtyBadge +
                '</div>' +
                '<div class="sc-card-body">' +
                '<div class="sc-card-name">' + escHtml(v.productName) + '</div>' +
                '<div class="sc-card-meta">' +
                (v.category ? '<span class="sc-tag">' + escHtml(v.category) + '</span>' : '') +
                '<span class="sc-tag">' + escHtml(v.sku) + '</span>' +
                '<span class="sc-tag">' + escHtml(v.size) + '</span>' +
                '<span class="sc-dot ' + accentClass + '" title="' + escHtml(v.color) + '"></span>' +
                '</div>' +
                '<div class="sc-card-foot">' +
                '<span class="sc-price">' + fmt(v.sellingPrice) + '</span>' +
                '<span class="sc-stock ' + stockCls + '">' + stockTxt + '</span>' +
                '</div>' +
                '</div>' +
                '</div>';
        }).join('');

        var loadMoreHtml = hasMore
            ? '<div class="sc-grid-footer"><button type="button" class="sc-load-more" id="loadMoreVariants">' +
                escHtml(str.loadMore || 'Load more') +
              '</button></div>'
            : '';

        variantGrid.innerHTML = cardsHtml + loadMoreHtml;

        variantGrid.querySelectorAll('[data-add]').forEach(function (card) {
            card.addEventListener('click', function () {
                addToCart(this.getAttribute('data-add'));
            });
        });

        var loadMoreButton = document.getElementById('loadMoreVariants');
        if (loadMoreButton) {
            loadMoreButton.addEventListener('click', function () {
                if (isLoading || !hasMore) return;
                currentPage += 1;
                loadVariants(false);
            });
        }
    }

    function addToCart(variantId) {
        var v = variantMap[variantId];
        if (!v) return;

        if (cart[variantId]) {
            if (cart[variantId].qty < v.stock) cart[variantId].qty++;
        } else {
            cart[variantId] = {
                id: v.id,
                productName: v.productName,
                category: v.category,
                sku: v.sku,
                size: v.size,
                color: v.color,
                stock: v.stock,
                sellingPrice: v.sellingPrice,
                costPrice: v.costPrice,
                qty: 1,
                unitPrice: v.sellingPrice
            };
        }

        lastActiveCartItemId = String(variantId);
        refresh();
    }

    function removeFromCart(variantId) {
        delete cart[variantId];
        refresh();
    }

    function changeQty(variantId, delta) {
        if (!cart[variantId]) return;
        var max = cart[variantId].stock || 9999;
        var next = cart[variantId].qty + delta;
        if (next <= 0) {
            removeFromCart(variantId);
            return;
        }
        cart[variantId].qty = Math.min(next, max);
        refresh();
    }

    function setQty(variantId, val) {
        if (!cart[variantId]) return;
        var max = cart[variantId].stock || 9999;
        cart[variantId].qty = Math.max(1, Math.min(parseInt(val, 10) || 1, max));
        syncTotalsAndInputs();
    }

    function setPrice(variantId, val) {
        if (!cart[variantId]) return;
        cart[variantId].unitPrice = Math.max(0, parseInt(val, 10) || 0);
        syncTotalsAndInputs();
    }

    function renderCart() {
        var items = Object.values(cart);
        if (cartBadge) cartBadge.textContent = String(items.length);

        if (!items.length) {
            if (emptyCart) emptyCart.classList.remove('sc-hidden');
            if (cartList) cartList.innerHTML = '';
            syncTotalsAndInputs();
            return;
        }

        if (emptyCart) emptyCart.classList.add('sc-hidden');
        if (cartList) {
            var priceLbl = escHtml(str.price || 'Price');
            cartList.innerHTML = items.map(function (item) {
                var isActive = lastActiveCartItemId === String(item.id);
                return '<div class="sc-cart-item' + (isActive ? ' sc-cart-item--active' : '') + '" data-cart-item="' + escAttr(item.id) + '">' +
                    '<div class="sc-ci-info">' +
                    '<span class="sc-ci-name">' + escHtml(item.productName) + '</span>' +
                    '<span class="sc-ci-meta">' + escHtml(item.sku) + ' · ' + escHtml(item.size) + ' · ' + escHtml(item.color) + '</span>' +
                    '</div>' +
                    '<div class="sc-ci-controls">' +
                    '<div class="sc-qty">' +
                    '<button type="button" class="sc-qty-btn" data-qty-dec="' + item.id + '"><i class="bi bi-dash"></i></button>' +
                    '<input type="number" class="sc-qty-inp" value="' + item.qty + '" min="1" max="' + item.stock + '" data-qty-inp="' + item.id + '" />' +
                    '<button type="button" class="sc-qty-btn" data-qty-inc="' + item.id + '"><i class="bi bi-plus"></i></button>' +
                    '</div>' +
                    '<div class="sc-price-wrap">' +
                    '<span class="sc-price-lbl">' + priceLbl + '</span>' +
                    '<input type="number" class="sc-price-inp" value="' + item.unitPrice + '" min="0" data-price-inp="' + item.id + '" />' +
                    '</div>' +
                    '<span class="sc-line-total">' + fmt(item.qty * item.unitPrice) + '</span>' +
                    '<button type="button" class="sc-remove" data-remove="' + item.id + '"><i class="bi bi-x-lg"></i></button>' +
                    '</div>' +
                    '</div>';
            }).join('');

            cartList.querySelectorAll('[data-qty-dec]').forEach(function (btn) {
                btn.addEventListener('click', function () { changeQty(this.dataset.qtyDec, -1); });
            });
            cartList.querySelectorAll('[data-qty-inc]').forEach(function (btn) {
                btn.addEventListener('click', function () { changeQty(this.dataset.qtyInc, 1); });
            });
            cartList.querySelectorAll('[data-qty-inp]').forEach(function (inp) {
                inp.addEventListener('change', function () { setQty(this.dataset.qtyInp, this.value); });
            });
            cartList.querySelectorAll('[data-price-inp]').forEach(function (inp) {
                inp.addEventListener('change', function () { setPrice(this.dataset.priceInp, this.value); });
            });
            cartList.querySelectorAll('[data-remove]').forEach(function (btn) {
                btn.addEventListener('click', function () { removeFromCart(this.dataset.remove); });
            });

            if (lastActiveCartItemId) {
                var activeItem = cartList.querySelector('[data-cart-item="' + cssEscape(lastActiveCartItemId) + '"]');
                if (activeItem) {
                    activeItem.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
                }
                lastActiveCartItemId = null;
            }
        }

        syncTotalsAndInputs();
    }

    function syncTotalsAndInputs() {
        var items = Object.values(cart);
        var subtotal = items.reduce(function (s, i) { return s + i.qty * i.unitPrice; }, 0);
        var cost = items.reduce(function (s, i) { return s + i.qty * i.costPrice; }, 0);
        var discount = discountInput ? Math.max(0, parseInt(discountInput.value, 10) || 0) : 0;
        var revenue = subtotal - discount;
        var profit = revenue - cost;

        if (sumRevenue) sumRevenue.textContent = fmt(revenue);
        if (sumFinal) sumFinal.textContent = fmt(revenue);
        if (sumCost) sumCost.textContent = fmt(cost);

        if (discountRow && sumDiscount) {
            if (discount > 0) {
                sumDiscount.textContent = '- ' + fmt(discount);
                discountRow.classList.remove('sc-hidden');
            } else {
                discountRow.classList.add('sc-hidden');
            }
        }

        if (sumProfit) {
            sumProfit.textContent = fmt(profit);
            sumProfit.className = 'sc-total-val sc-profit ' + (profit >= 0 ? 'sc-profit--pos' : 'sc-profit--neg');
        }

        if (hiddenInputs) {
            hiddenInputs.innerHTML = items.map(function (item, i) {
                return '<input type="hidden" name="Items[' + i + '].ProductVariantId" value="' + escAttr(item.id) + '" />' +
                    '<input type="hidden" name="Items[' + i + '].Quantity" value="' + item.qty + '" />' +
                    '<input type="hidden" name="Items[' + i + '].UnitPrice" value="' + item.unitPrice + '" />' +
                    '<input type="hidden" name="Items[' + i + '].CostPrice" value="' + item.costPrice + '" />';
            }).join('');
        }
    }

    function refresh() {
        renderCart();
        renderGrid();
    }

    if (variantSearch) {
        variantSearch.addEventListener('input', function () {
            if (searchDebounce) window.clearTimeout(searchDebounce);
            searchDebounce = window.setTimeout(function () {
                loadVariants(true);
            }, 250);
        });
    }

    if (discountInput) {
        discountInput.addEventListener('input', syncTotalsAndInputs);
    }

    saleForm.addEventListener('submit', function (e) {
        if (!Object.keys(cart).length) {
            e.preventDefault();
            if (clientError) {
                clientError.textContent = str.cartRequired || 'Please add at least one product to the sale.';
                clientError.classList.remove('sc-hidden');
                clientError.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    });

    function fmt(n) {
        return Number(n).toLocaleString();
    }

    function escHtml(value) {
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function escAttr(value) {
        return String(value).replace(/"/g, '&quot;');
    }

    function cssEscape(value) {
        if (window.CSS && typeof window.CSS.escape === 'function') {
            return window.CSS.escape(String(value));
        }

        return String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    }

    function colorClass(name) {
        var normalized = (name || '').toLowerCase().replace(/[^a-z]+/g, '-').replace(/^-+|-+$/g, '');
        var supported = {
            red: true, blue: true, green: true, yellow: true, orange: true, purple: true,
            pink: true, brown: true, grey: true, gray: true, black: true, white: true,
            navy: true, teal: true, maroon: true, beige: true, cream: true, gold: true,
            silver: true, cyan: true, lime: true, indigo: true, violet: true, magenta: true,
            khaki: true, turquoise: true, coral: true, lavender: true
        };

        if (!supported[normalized]) {
            return 'sc-color-default';
        }

        return 'sc-color-' + normalized;
    }

    loadVariants(true);
}());
