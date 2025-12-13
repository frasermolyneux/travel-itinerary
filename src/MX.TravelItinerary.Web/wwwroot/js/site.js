(() => {
    document.addEventListener('DOMContentLoaded', () => {
        initTimelineSelection();
        initFabMenu();
        initEditButtons();
        reopenOffcanvasOnValidation();
    });

    function initTimelineSelection() {
        const items = Array.from(document.querySelectorAll('[data-timeline-item]'));
        if (items.length === 0) {
            return;
        }

        let current;
        items.forEach((item) => {
            item.addEventListener('click', (event) => {
                if (event.target.closest('.timeline-actions')) {
                    return;
                }

                if (current === item) {
                    item.classList.toggle('is-selected');
                    if (!item.classList.contains('is-selected')) {
                        current = undefined;
                    }
                    return;
                }

                current?.classList.remove('is-selected');
                item.classList.add('is-selected');
                current = item;
            });
        });
    }

    function initFabMenu() {
        const fab = document.querySelector('[data-fab]');
        if (!fab) {
            return;
        }

        const toggle = fab.querySelector('[data-fab-toggle]');
        toggle?.addEventListener('click', () => {
            fab.classList.toggle('is-open');
        });

        document.addEventListener('click', (event) => {
            if (!fab.contains(event.target)) {
                fab.classList.remove('is-open');
            }
        });

        fab.querySelectorAll('[data-open-form]').forEach((button) => {
            button.addEventListener('click', () => {
                const formType = button.getAttribute('data-open-form');
                fab.classList.remove('is-open');

                if (formType === 'segment') {
                    resetSegmentForm();
                    openOffcanvas('segmentFlyout');
                } else if (formType === 'entry') {
                    resetEntryForm();
                    openOffcanvas('entryFlyout');
                }
            });
        });
    }

    function initEditButtons() {
        document.querySelectorAll('[data-edit-type="segment"]').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.stopPropagation();
                populateSegmentForm(button.dataset);
                openOffcanvas('segmentFlyout');
            });
        });

        document.querySelectorAll('[data-edit-type="entry"]').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.stopPropagation();
                populateEntryForm(button.dataset);
                openOffcanvas('entryFlyout');
            });
        });
    }

    function populateSegmentForm(dataset) {
        const form = document.getElementById('segment-form');
        if (!form) {
            return;
        }

        setInputValue('SegmentInput_SegmentId', dataset.segmentId ?? '');
        setInputValue('SegmentInput_Title', dataset.segmentTitle ?? '');
        setInputValue('SegmentInput_SegmentType', dataset.segmentType ?? 'travel');
        setInputValue('SegmentInput_StartDateTimeUtc', dataset.segmentStart ?? '');
        setInputValue('SegmentInput_EndDateTimeUtc', dataset.segmentEnd ?? '');
        setInputValue('SegmentInput_Description', dataset.segmentDescription ?? '');

        const label = document.getElementById('segmentFlyoutLabel');
        if (label) {
            label.textContent = 'Edit trip segment';
        }
    }

    function populateEntryForm(dataset) {
        const form = document.getElementById('entry-form');
        if (!form) {
            return;
        }

        setInputValue('EntryInput_EntryId', dataset.entryId ?? '');
        setInputValue('EntryInput_Title', dataset.entryTitle ?? '');
        setInputValue('EntryInput_Date', dataset.entryDate ?? '');
        setInputValue('EntryInput_Category', dataset.entryCategory ?? '');
        setInputValue('EntryInput_Details', dataset.entryDetails ?? '');

        const label = document.getElementById('entryFlyoutLabel');
        if (label) {
            label.textContent = 'Edit itinerary entry';
        }
    }

    function resetSegmentForm() {
        setInputValue('SegmentInput_SegmentId', '');
        setInputValue('SegmentInput_Title', '');
        setInputValue('SegmentInput_SegmentType', 'travel');
        setInputValue('SegmentInput_StartDateTimeUtc', '');
        setInputValue('SegmentInput_EndDateTimeUtc', '');
        setInputValue('SegmentInput_Description', '');

        const label = document.getElementById('segmentFlyoutLabel');
        if (label) {
            label.textContent = 'Add trip segment';
        }
    }

    function resetEntryForm() {
        setInputValue('EntryInput_EntryId', '');
        setInputValue('EntryInput_Title', '');
        setInputValue('EntryInput_Date', '');
        setInputValue('EntryInput_Category', '');
        setInputValue('EntryInput_Details', '');

        const label = document.getElementById('entryFlyoutLabel');
        if (label) {
            label.textContent = 'Add itinerary entry';
        }
    }

    function setInputValue(id, value) {
        const input = document.getElementById(id);
        if (!input) {
            return;
        }

        input.value = value ?? '';
    }

    function openOffcanvas(id) {
        const element = document.getElementById(id);
        if (!element) {
            return;
        }

        const instance = bootstrap.Offcanvas.getOrCreateInstance(element);
        instance.show();
    }

    function reopenOffcanvasOnValidation() {
        const segmentErrors = document.querySelector('#segment-form .validation-summary-errors');
        const entryErrors = document.querySelector('#entry-form .validation-summary-errors');

        if (segmentErrors) {
            openOffcanvas('segmentFlyout');
        } else if (entryErrors) {
            openOffcanvas('entryFlyout');
        }
    }
})();
