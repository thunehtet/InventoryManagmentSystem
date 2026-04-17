(function () {
    var dataElement = document.getElementById('financeTrendData');
    var chartElement = document.getElementById('financeTrendChart');

    if (!dataElement || !chartElement || typeof Chart === 'undefined') {
        return;
    }

    function parseJsonAttribute(name) {
        var value = dataElement.getAttribute(name);
        return value ? JSON.parse(value) : [];
    }

    var labels = parseJsonAttribute('data-labels');
    var cashIn = parseJsonAttribute('data-cash-in');
    var cashOut = parseJsonAttribute('data-cash-out');
    var currencyCode = dataElement.dataset.currency || '';
    var cashInLabel = dataElement.dataset.cashInLabel || 'Cash In';
    var cashOutLabel = dataElement.dataset.cashOutLabel || 'Cash Out';

    function fmtAxis(value) {
        var abs = Math.abs(value);
        if (abs >= 1000000) return (value / 1000000).toFixed(abs >= 10000000 ? 0 : 1) + 'M';
        if (abs >= 1000) return (value / 1000).toFixed(abs >= 10000 ? 0 : 1) + 'K';
        return value.toLocaleString();
    }

    function fmtTooltip(value) {
        return value.toLocaleString() + (currencyCode ? ' ' + currencyCode : '');
    }

    new Chart(chartElement.getContext('2d'), {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                {
                    label: cashInLabel,
                    data: cashIn,
                    backgroundColor: 'rgba(15, 118, 110, 0.78)',
                    borderRadius: 10
                },
                {
                    label: cashOutLabel,
                    data: cashOut,
                    backgroundColor: 'rgba(234, 88, 12, 0.72)',
                    borderRadius: 10
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: { position: 'bottom' },
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            return context.dataset.label + ': ' + fmtTooltip(context.parsed.y);
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: { display: false }
                },
                y: {
                    grid: { color: '#f1f5f9' },
                    ticks: {
                        callback: fmtAxis
                    }
                }
            }
        }
    });

    var financeMonth = document.getElementById('financeMonth');
    var financeYear = document.getElementById('financeYear');

    function reloadForPeriod() {
        if (!financeMonth || !financeYear) {
            return;
        }

        var params = new URLSearchParams(window.location.search);
        params.set('month', financeMonth.value);
        params.set('year', financeYear.value);
        window.location.search = params.toString();
    }

    if (financeMonth) {
        financeMonth.addEventListener('change', reloadForPeriod);
    }

    if (financeYear) {
        financeYear.addEventListener('change', reloadForPeriod);
    }
})();
