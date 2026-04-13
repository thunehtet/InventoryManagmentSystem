// ============================================================
//  SALE CREATE — card-based variant picker + cart
// ============================================================

(function () {
    'use strict';

    // ── State ──────────────────────────────────────────────────
    var allVariants = [];
    var cart = {}; // { [variantId]: { ...variant, qty, unitPrice } }

    // ── DOM refs (safe — page-specific) ───────────────────────
    var variantGrid   = document.getElementById('variantGrid');
    var variantSearch = document.getElementById('variantSearch');
    var variantCount  = document.getElementById('variantCount');
    var cartList      = document.getElementById('cartList');
    var emptyCart     = document.getElementById('emptyCart');
    var cartBadge     = document.getElementById('cartBadge');
    var hiddenInputs  = document.getElementById('hiddenInputs');
    var sumRevenue    = document.getElementById('sumRevenue');
    var sumFinal      = document.getElementById('sumFinal');
    var sumDiscount   = document.getElementById('sumDiscount');
    var discountRow   = document.getElementById('discountRow');
    var discountInput = document.getElementById('discountInput');
    var sumCost       = document.getElementById('sumCost');
    var sumProfit     = document.getElementById('sumProfit');
    var clientError   = document.getElementById('clientError');
    var saleForm      = document.getElementById('saleForm');

    if (!saleForm) return; // not on this page

    // ── Load variants from server ──────────────────────────────
    function loadVariants() {
        fetch('/Sales/GetVariants')
            .then(function (res) {
                if (!res.ok) throw new Error('Network error');
                return res.json();
            })
            .then(function (data) {
                allVariants = data;
                if (variantCount) variantCount.textContent = data.length;
                renderGrid(data);
            })
            .catch(function () {
                if (variantGrid) {
                    variantGrid.innerHTML =
                        '<div class="sc-loading"><i class="bi bi-exclamation-circle text-danger"></i>' +
                        '<span>Failed to load products. Please refresh.</span></div>';
                }
            });
    }

    // ── Render variant card grid ───────────────────────────────
    function renderGrid(variants) {
        if (!variantGrid) return;
        if (!variants.length) {
            variantGrid.innerHTML =
                '<div class="sc-loading"><i class="bi bi-search"></i><span>No products found</span></div>';
            return;
        }
        variantGrid.innerHTML = variants.map(function (v) {
            var inCart   = cart[v.id];
            var noStock  = v.stock <= 0;
            var initial  = v.productName.charAt(0).toUpperCase();
            var accent   = colorToHex(v.color);
            var qtyBadge = inCart ? '<span class="sc-qty-badge">' + inCart.qty + '</span>' : '';
            var stockCls = noStock ? 'sc-stock-none' : v.stock <= 5 ? 'sc-stock-low' : 'sc-stock-ok';
            var stockTxt = noStock ? 'Out of stock' : v.stock + ' left';
            var cardCls  = 'sc-card' +
                           (inCart  ? ' sc-card--selected' : '') +
                           (noStock ? ' sc-card--disabled'  : '');
            var onclick  = noStock ? '' : 'data-add="' + v.id + '"';

            return '<div class="' + cardCls + '" ' + onclick + ' data-id="' + v.id + '">' +
                '<div class="sc-avatar" style="--av-color:' + accent + '">' +
                    initial + qtyBadge +
                '</div>' +
                '<div class="sc-card-body">' +
                    '<div class="sc-card-name">' + escHtml(v.productName) + '</div>' +
                    '<div class="sc-card-meta">' +
                        '<span class="sc-tag">' + escHtml(v.sku)  + '</span>' +
                        '<span class="sc-tag">' + escHtml(v.size) + '</span>' +
                        '<span class="sc-dot" style="background:' + accent + '" title="' + escHtml(v.color) + '"></span>' +
                    '</div>' +
                    '<div class="sc-card-foot">' +
                        '<span class="sc-price">' + fmt(v.sellingPrice) + '</span>' +
                        '<span class="sc-stock ' + stockCls + '">' + stockTxt + '</span>' +
                    '</div>' +
                '</div>' +
            '</div>';
        }).join('');

        // Delegate click — no inline onclick
        variantGrid.querySelectorAll('[data-add]').forEach(function (card) {
            card.addEventListener('click', function () {
                addToCart(this.getAttribute('data-add'));
            });
        });
    }

    // ── Cart operations ────────────────────────────────────────
    function addToCart(variantId) {
        var v = allVariants.find(function (x) { return x.id === variantId; });
        if (!v) return;
        if (cart[variantId]) {
            if (cart[variantId].qty < v.stock) cart[variantId].qty++;
        } else {
            cart[variantId] = Object.assign({}, v, { qty: 1, unitPrice: v.sellingPrice });
        }
        refresh();
    }

    function removeFromCart(variantId) {
        delete cart[variantId];
        refresh();
    }

    function changeQty(variantId, delta) {
        if (!cart[variantId]) return;
        var v   = allVariants.find(function (x) { return x.id === variantId; });
        var max = v ? v.stock : 9999;
        var next = cart[variantId].qty + delta;
        if (next <= 0) { removeFromCart(variantId); return; }
        cart[variantId].qty = Math.min(next, max);
        refresh();
    }

    function setQty(variantId, val) {
        if (!cart[variantId]) return;
        var v   = allVariants.find(function (x) { return x.id === variantId; });
        var max = v ? v.stock : 9999;
        cart[variantId].qty = Math.max(1, Math.min(parseInt(val, 10) || 1, max));
        syncTotalsAndInputs();
    }

    function setPrice(variantId, val) {
        if (!cart[variantId]) return;
        cart[variantId].unitPrice = Math.max(0, parseInt(val, 10) || 0);
        syncTotalsAndInputs();
    }

    // ── Render cart ────────────────────────────────────────────
    function renderCart() {
        var items = Object.values(cart);
        if (cartBadge) cartBadge.textContent = items.length;

        if (!items.length) {
            if (emptyCart) emptyCart.style.display = 'flex';
            if (cartList)  cartList.innerHTML = '';
            syncTotalsAndInputs();
            return;
        }

        if (emptyCart) emptyCart.style.display = 'none';
        if (cartList) {
            cartList.innerHTML = items.map(function (item) {
                return '<div class="sc-cart-item">' +
                    '<div class="sc-ci-info">' +
                        '<span class="sc-ci-name">'  + escHtml(item.productName) + '</span>' +
                        '<span class="sc-ci-meta">'  + escHtml(item.sku) + ' · ' + escHtml(item.size) + ' · ' + escHtml(item.color) + '</span>' +
                    '</div>' +
                    '<div class="sc-ci-controls">' +
                        '<div class="sc-qty">' +
                            '<button type="button" class="sc-qty-btn" data-qty-dec="' + item.id + '"><i class="bi bi-dash"></i></button>' +
                            '<input type="number" class="sc-qty-inp" value="' + item.qty + '" min="1" max="' + item.stock + '" data-qty-inp="' + item.id + '" />' +
                            '<button type="button" class="sc-qty-btn" data-qty-inc="' + item.id + '"><i class="bi bi-plus"></i></button>' +
                        '</div>' +
                        '<div class="sc-price-wrap">' +
                            '<span class="sc-price-lbl">Price</span>' +
                            '<input type="number" class="sc-price-inp" value="' + item.unitPrice + '" min="0" data-price-inp="' + item.id + '" />' +
                        '</div>' +
                        '<span class="sc-line-total">' + fmt(item.qty * item.unitPrice) + '</span>' +
                        '<button type="button" class="sc-remove" data-remove="' + item.id + '"><i class="bi bi-x-lg"></i></button>' +
                    '</div>' +
                '</div>';
            }).join('');

            // Delegate events — no inline handlers
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
        }

        syncTotalsAndInputs();
    }

    // ── Totals + hidden form inputs ────────────────────────────
    function syncTotalsAndInputs() {
        var items    = Object.values(cart);
        var subtotal = items.reduce(function (s, i) { return s + i.qty * i.unitPrice; }, 0);
        var cost     = items.reduce(function (s, i) { return s + i.qty * i.costPrice; }, 0);
        var discount = discountInput ? Math.max(0, parseInt(discountInput.value, 10) || 0) : 0;
        var revenue  = subtotal - discount;
        var profit   = revenue - cost;

        if (sumRevenue) sumRevenue.textContent = fmt(subtotal);
        if (sumFinal)   sumFinal.textContent   = fmt(revenue);
        if (sumCost)    sumCost.textContent     = fmt(cost);

        if (discountRow && sumDiscount) {
            if (discount > 0) {
                sumDiscount.textContent = '- ' + fmt(discount);
                discountRow.style.display = '';
            } else {
                discountRow.style.display = 'none';
            }
        }

        if (sumProfit) {
            sumProfit.textContent = fmt(profit);
            sumProfit.className   = 'sc-total-val sc-profit ' +
                                    (profit >= 0 ? 'sc-profit--pos' : 'sc-profit--neg');
        }

        if (hiddenInputs) {
            hiddenInputs.innerHTML = items.map(function (item, i) {
                return '<input type="hidden" name="Items[' + i + '].ProductVariantId" value="' + escAttr(item.id) + '" />' +
                       '<input type="hidden" name="Items[' + i + '].Quantity"         value="' + item.qty       + '" />' +
                       '<input type="hidden" name="Items[' + i + '].UnitPrice"        value="' + item.unitPrice + '" />' +
                       '<input type="hidden" name="Items[' + i + '].CostPrice"        value="' + item.costPrice + '" />';
            }).join('');
        }
    }

    function refresh() {
        renderCart();
        renderGrid(filtered());
    }

    // ── Search filter ──────────────────────────────────────────
    function filtered() {
        var q = variantSearch ? variantSearch.value.toLowerCase().trim() : '';
        if (!q) return allVariants;
        return allVariants.filter(function (v) {
            return v.productName.toLowerCase().includes(q) ||
                   v.sku.toLowerCase().includes(q)         ||
                   v.color.toLowerCase().includes(q)       ||
                   v.size.toLowerCase().includes(q);
        });
    }

    if (variantSearch) {
        variantSearch.addEventListener('input', function () { renderGrid(filtered()); });
    }

    if (discountInput) {
        discountInput.addEventListener('input', syncTotalsAndInputs);
    }

    // ── Form submit guard ──────────────────────────────────────
    saleForm.addEventListener('submit', function (e) {
        if (!Object.keys(cart).length) {
            e.preventDefault();
            if (clientError) {
                clientError.textContent = 'Please add at least one product to the sale.';
                clientError.style.display = 'block';
                clientError.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        }
    });

    // ── Helpers ────────────────────────────────────────────────
    function fmt(n) {
        return Number(n).toLocaleString();
    }

    function escHtml(str) {
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function escAttr(str) {
        return String(str).replace(/"/g, '&quot;');
    }

    function colorToHex(name) {
        var map = {
            red:'#e74c3c', blue:'#3498db', green:'#27ae60', yellow:'#f1c40f',
            orange:'#e67e22', purple:'#9b59b6', pink:'#e91e63', brown:'#795548',
            grey:'#607d8b', gray:'#607d8b', black:'#2c3e50', white:'#bdc3c7',
            navy:'#1a237e', teal:'#00897b', maroon:'#880e4f', beige:'#d7ccc8',
            cream:'#efebe9', gold:'#ffc107', silver:'#9e9e9e', cyan:'#00bcd4',
            lime:'#8bc34a', indigo:'#3f51b5', violet:'#7c4dff', magenta:'#e91e63',
            khaki:'#c8b560', turquoise:'#1abc9c', coral:'#ff6b6b', lavender:'#b39ddb'
        };
        return map[(name || '').toLowerCase()] || '#6366f1';
    }

    // ── Init ───────────────────────────────────────────────────
    loadVariants();

}());
