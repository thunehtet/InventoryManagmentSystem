(function () {
    var dataElement = document.getElementById('salesTrendData');
    var chartElement = document.getElementById('salesTrendChart');

    if (!dataElement || !chartElement || typeof Chart === 'undefined') {
        return;
    }

    function parseJsonAttribute(name) {
        var value = dataElement.getAttribute(name);
        return value ? JSON.parse(value) : [];
    }

    var daily = {
        labels: parseJsonAttribute('data-daily-labels'),
        sales: parseJsonAttribute('data-daily-sales'),
        profit: parseJsonAttribute('data-daily-profit')
    };
    var monthly = {
        labels: parseJsonAttribute('data-monthly-labels'),
        sales: parseJsonAttribute('data-monthly-sales'),
        profit: parseJsonAttribute('data-monthly-profit')
    };

    var currencyCode = dataElement.dataset.currency || '';
    var salesLabel = dataElement.dataset.salesLabel || 'Sales';
    var profitLabel = dataElement.dataset.profitLabel || 'Profit';
    var dailySubtitle = dataElement.dataset.dailySubtitle || '';
    var monthlySubtitle = dataElement.dataset.monthlySubtitle || '';

    var currentMode = 'day';

    function fmtAxis(value) {
        var abs = Math.abs(value);
        if (abs >= 1000000) return (value / 1000000).toFixed(abs >= 10000000 ? 0 : 1) + 'M';
        if (abs >= 1000) return (value / 1000).toFixed(abs >= 10000 ? 0 : 1) + 'K';
        return value.toLocaleString();
    }

    function fmtTooltip(value) {
        return value.toLocaleString() + (currencyCode ? ' ' + currencyCode : '');
    }

    var chart = new Chart(chartElement.getContext('2d'), {
        type: 'line',
        data: {
            labels: daily.labels,
            datasets: [
                {
                    label: salesLabel,
                    data: daily.sales,
                    borderColor: '#6366f1',
                    backgroundColor: 'rgba(99,102,241,0.08)',
                    borderWidth: 2.5,
                    pointRadius: 3,
                    pointHoverRadius: 6,
                    tension: 0.35,
                    fill: true
                },
                {
                    label: profitLabel,
                    data: daily.profit,
                    borderColor: '#10b981',
                    backgroundColor: 'rgba(16,185,129,0.07)',
                    borderWidth: 2.5,
                    pointRadius: 3,
                    pointHoverRadius: 6,
                    tension: 0.35,
                    fill: true
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            return ' ' + context.dataset.label + ': ' + fmtTooltip(context.parsed.y);
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { display: false },
                    ticks: { color: '#94a3b8', font: { size: 11 }, maxRotation: 45, autoSkip: true, maxTicksLimit: 15 }
                },
                y: {
                    grid: { color: '#f1f5f9' },
                    ticks: {
                        color: '#94a3b8',
                        font: { size: 11 },
                        callback: fmtAxis
                    }
                }
            }
        }
    });

    var btnDaily = document.getElementById('btnDaily');
    var btnMonthly = document.getElementById('btnMonthly');
    var subtitle = document.getElementById('chartSubtitle');
    var trendMonth = document.getElementById('trendMonth');
    var trendYear = document.getElementById('trendYear');

    function setMode(mode) {
        currentMode = mode;
        var source = mode === 'month' ? monthly : daily;
        chart.data.labels = source.labels;
        chart.data.datasets[0].data = source.sales;
        chart.data.datasets[1].data = source.profit;
        chart.update();

        if (subtitle) {
            subtitle.textContent = mode === 'month' ? monthlySubtitle : dailySubtitle;
        }

        if (btnDaily) btnDaily.classList.toggle('trend-filter-active', mode === 'day');
        if (btnMonthly) btnMonthly.classList.toggle('trend-filter-active', mode === 'month');
    }

    var pendingRequest = null;

    function reloadForPeriod() {
        if (!trendMonth || !trendYear) return;

        var m = trendMonth.value;
        var y = trendYear.value;

        // Update the URL silently so a manual refresh restores the same selection
        var params = new URLSearchParams(window.location.search);
        params.set('month', m);
        params.set('year', y);
        history.replaceState(null, '', window.location.pathname + '?' + params.toString());

        // Cancel any in-flight request
        if (pendingRequest) pendingRequest.abort();
        pendingRequest = new AbortController();

        fetch('/Dashboard/TrendData?month=' + m + '&year=' + y, { signal: pendingRequest.signal })
            .then(function (r) { return r.json(); })
            .then(function (data) {
                pendingRequest = null;

                daily.labels  = data.dailyLabels;
                daily.sales   = data.dailySales;
                daily.profit  = data.dailyProfit;
                monthly.labels = data.monthlyLabels;
                monthly.sales  = data.monthlySales;
                monthly.profit = data.monthlyProfit;
                dailySubtitle   = data.dailySubtitle;
                monthlySubtitle = data.monthlySubtitle;

                // Re-apply whichever mode is currently active
                setMode(currentMode);
            })
            .catch(function (err) {
                if (err.name !== 'AbortError') console.error('TrendData fetch failed', err);
            });
    }

    if (btnDaily)   btnDaily.addEventListener('click',  function () { setMode('day'); });
    if (btnMonthly) btnMonthly.addEventListener('click', function () { setMode('month'); });
    if (trendMonth) trendMonth.addEventListener('change', reloadForPeriod);
    if (trendYear)  trendYear.addEventListener('change',  reloadForPeriod);
})();
