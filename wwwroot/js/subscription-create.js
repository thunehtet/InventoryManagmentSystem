(function () {
    'use strict';

    var tenantSelect = document.getElementById('tenantSelect');
    var warning = document.getElementById('activeSubWarning');
    var planName = document.getElementById('activeSubPlan');
    var form = tenantSelect ? tenantSelect.closest('form') : null;

    if (!tenantSelect || !warning || !planName || !form) {
        return;
    }

    var activeSubs = {};

    try {
        activeSubs = JSON.parse(form.dataset.activeSubs || '{}');
    } catch (error) {
        activeSubs = {};
    }

    tenantSelect.addEventListener('change', function () {
        var selected = this.value;

        if (selected && activeSubs[selected]) {
            planName.textContent = activeSubs[selected];
            warning.classList.remove('d-none');
            return;
        }

        warning.classList.add('d-none');
    });
}());
