(function () {
    'use strict';

    var selectors = document.querySelectorAll('select[data-searchable-picker]');
    if (!selectors.length) return;

    selectors.forEach(function (select) {
        if (select.dataset.searchablePickerReady === 'true') return;
        select.dataset.searchablePickerReady = 'true';

        var wrapper = document.createElement('div');
        wrapper.className = 'searchable-picker';

        var button = document.createElement('button');
        button.type = 'button';
        button.className = 'searchable-picker__trigger input-modern input-modern-lg';
        button.setAttribute('aria-haspopup', 'listbox');
        button.setAttribute('aria-expanded', 'false');

        var valueNode = document.createElement('span');
        valueNode.className = 'searchable-picker__value';

        var iconNode = document.createElement('span');
        iconNode.className = 'searchable-picker__icon';
        iconNode.innerHTML = '<i class="bi bi-chevron-down"></i>';

        button.appendChild(valueNode);
        button.appendChild(iconNode);

        var panel = document.createElement('div');
        panel.className = 'searchable-picker__panel';
        panel.hidden = true;

        var searchWrap = document.createElement('div');
        searchWrap.className = 'searchable-picker__search-wrap';

        var searchInput = document.createElement('input');
        searchInput.type = 'search';
        searchInput.className = 'searchable-picker__search';
        searchInput.placeholder = select.dataset.searchPlaceholder || 'Search...';

        searchWrap.appendChild(searchInput);

        var list = document.createElement('div');
        list.className = 'searchable-picker__list';
        list.setAttribute('role', 'listbox');

        var emptyState = document.createElement('div');
        emptyState.className = 'searchable-picker__empty';
        emptyState.textContent = select.dataset.emptyText || 'No matches found';
        emptyState.hidden = true;

        panel.appendChild(searchWrap);
        panel.appendChild(list);
        panel.appendChild(emptyState);

        select.classList.add('searchable-picker__native');
        select.parentNode.insertBefore(wrapper, select.nextSibling);
        wrapper.appendChild(button);
        wrapper.appendChild(panel);
        wrapper.appendChild(select);

        function optionItems() {
            return Array.prototype.slice.call(select.options)
                .filter(function (option) { return option.value !== ''; });
        }

        function selectedOption() {
            return select.options[select.selectedIndex] || null;
        }

        function selectedText() {
            var option = selectedOption();
            if (option && option.value) return option.text;
            return select.dataset.placeholder || 'Select an option';
        }

        function updateTrigger() {
            valueNode.textContent = selectedText();
            button.classList.toggle('is-placeholder', !select.value);
        }

        function closePanel() {
            wrapper.classList.remove('is-open');
            panel.hidden = true;
            button.setAttribute('aria-expanded', 'false');
        }

        function openPanel() {
            wrapper.classList.add('is-open');
            panel.hidden = false;
            button.setAttribute('aria-expanded', 'true');
            renderList(searchInput.value);
            window.setTimeout(function () {
                searchInput.focus();
                searchInput.select();
            }, 0);
        }

        function renderList(filterText) {
            var query = (filterText || '').trim().toLowerCase();
            var options = optionItems().filter(function (option) {
                return !query || option.text.toLowerCase().indexOf(query) >= 0;
            });

            list.innerHTML = '';
            emptyState.hidden = options.length !== 0;

            options.forEach(function (option) {
                var item = document.createElement('button');
                item.type = 'button';
                item.className = 'searchable-picker__option';
                item.textContent = option.text;
                item.dataset.value = option.value;
                item.setAttribute('role', 'option');
                item.setAttribute('aria-selected', option.selected ? 'true' : 'false');

                if (option.selected) {
                    item.classList.add('is-selected');
                }

                item.addEventListener('click', function () {
                    select.value = option.value;
                    select.dispatchEvent(new Event('change', { bubbles: true }));
                    updateTrigger();
                    closePanel();
                });

                list.appendChild(item);
            });
        }

        button.addEventListener('click', function () {
            if (wrapper.classList.contains('is-open')) {
                closePanel();
            } else {
                openPanel();
            }
        });

        searchInput.addEventListener('input', function () {
            renderList(searchInput.value);
        });

        select.addEventListener('change', updateTrigger);

        document.addEventListener('click', function (event) {
            if (!wrapper.contains(event.target)) {
                closePanel();
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && wrapper.classList.contains('is-open')) {
                closePanel();
                button.focus();
            }
        });

        updateTrigger();
    });
}());
