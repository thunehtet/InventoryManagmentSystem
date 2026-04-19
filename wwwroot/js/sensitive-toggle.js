document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('[data-sensitive-btn]').forEach(btn => {
        const group = btn.dataset.sensitiveBtn;
        const targets = () => document.querySelectorAll(`[data-sensitive-group="${group}"]`);
        let revealed = false;

        btn.addEventListener('click', () => {
            revealed = !revealed;
            targets().forEach(el => {
                if (el.tagName === 'INPUT') {
                    el.value = revealed ? el.dataset.real : '\u2022\u2022\u2022\u2022\u2022\u2022';
                } else {
                    el.textContent = revealed ? el.dataset.real : '\u2022\u2022\u2022\u2022';
                }
            });
            btn.querySelector('i').className = revealed ? 'bi bi-eye-slash-fill' : 'bi bi-eye-fill';
            btn.title = revealed ? 'Hide' : 'Show';
        });
    });
});
