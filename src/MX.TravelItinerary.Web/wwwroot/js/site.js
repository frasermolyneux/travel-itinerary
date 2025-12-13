(() => {
    document.addEventListener('DOMContentLoaded', () => {
        initTimelineSelection();
        initFabMenu();
        initEditButtons();
        initBookingButtons();
        initMultiDayToggle();
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

                if (formType === 'entry') {
                    resetEntryForm();
                    openOffcanvas('entryFlyout');
                }
            });
        });
    }

    function initEditButtons() {
        document.querySelectorAll('[data-edit-type="entry"]').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.stopPropagation();
                populateEntryForm(button.dataset);
                openOffcanvas('entryFlyout');
            });
        });
    }

    function initBookingButtons() {
        const addButtons = document.querySelectorAll('[data-booking-action]');
        if (addButtons.length === 0) {
            return;
        }

        document.querySelectorAll('[data-booking-action="add"]').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.stopPropagation();
                openBookingForm({
                    mode: 'create',
                    parentType: button.dataset.parentType,
                    entryId: button.dataset.entryId,
                    bookingType: button.dataset.bookingType,
                    title: button.dataset.entryTitle || button.dataset.segmentTitle || ''
                });
            });
        });

        document.querySelectorAll('[data-booking-action="view"]').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.stopPropagation();
                showBookingDetails(button.dataset);
            });
        });

        const editButton = document.getElementById('bookingDetailEditButton');
        editButton?.addEventListener('click', () => {
            const detailPane = document.getElementById('bookingDetailFlyout');
            if (!detailPane) {
                return;
            }

            closeOffcanvas('bookingDetailFlyout');
            openBookingForm({
                mode: 'edit',
                bookingId: detailPane.dataset.bookingId,
                parentType: detailPane.dataset.bookingParentType,
                entryId: detailPane.dataset.bookingEntryId,
                bookingType: detailPane.dataset.bookingType,
                vendor: detailPane.dataset.bookingVendor,
                reference: detailPane.dataset.bookingReference,
                cost: detailPane.dataset.bookingCost,
                currency: detailPane.dataset.bookingCurrency,
                refundable: detailPane.dataset.bookingRefundable,
                cancellation: detailPane.dataset.bookingCancellation,
                details: detailPane.dataset.bookingDetails
            });
        });
    }

    function populateEntryForm(dataset) {
        const form = document.getElementById('entry-form');
        if (!form) {
            return;
        }

        setInputValue('EntryInput_EntryId', dataset.entryId ?? '');
        setInputValue('EntryInput_Title', dataset.entryTitle ?? '');
        setInputValue('EntryInput_Date', dataset.entryDate ?? '');
        setInputValue('EntryInput_EndDate', dataset.entryEnd ?? '');
        setInputValue('EntryInput_ItemType', dataset.entryType ?? 'Activity');
        setInputValue('EntryInput_Details', dataset.entryDetails ?? '');

        const multiDay = dataset.entryIsMultiDay === 'true';
        const multiDayToggle = document.getElementById('EntryInput_IsMultiDay');
        if (multiDayToggle) {
            multiDayToggle.checked = multiDay;
        }
        updateEntryEndDateVisibility();

        const label = document.getElementById('entryFlyoutLabel');
        if (label) {
            label.textContent = 'Edit itinerary entry';
        }
    }

    function resetEntryForm() {
        setInputValue('EntryInput_EntryId', '');
        setInputValue('EntryInput_Title', '');
        setInputValue('EntryInput_Date', '');
        setInputValue('EntryInput_EndDate', '');
        setInputValue('EntryInput_ItemType', 'Activity');
        setInputValue('EntryInput_Details', '');

        const multiDayToggle = document.getElementById('EntryInput_IsMultiDay');
        if (multiDayToggle) {
            multiDayToggle.checked = false;
        }
        updateEntryEndDateVisibility();

        const label = document.getElementById('entryFlyoutLabel');
        if (label) {
            label.textContent = 'Add itinerary entry';
        }
    }

    function resetBookingForm() {
        setInputValue('BookingInput_BookingId', '');
        setInputValue('BookingInput_EntryId', '');
        setInputValue('BookingInput_BookingType', 'Other');
        setInputValue('BookingInput_Vendor', '');
        setInputValue('BookingInput_Reference', '');
        setInputValue('BookingInput_Cost', '');
        setInputValue('BookingInput_Currency', '');
        setInputValue('BookingInput_CancellationPolicy', '');
        setInputValue('BookingInput_ConfirmationDetails', '');

        const refundable = document.getElementById('BookingInput_IsRefundable');
        if (refundable) {
            refundable.checked = false;
        }

        const label = document.getElementById('bookingFlyoutLabel');
        if (label) {
            label.textContent = 'Add booking confirmation';
        }
    }

    function setInputValue(id, value) {
        const input = document.getElementById(id);
        if (!input) {
            return;
        }

        input.value = value ?? '';
    }

    function openBookingForm(options) {
        resetBookingForm();

        if (options.mode === 'edit') {
            const label = document.getElementById('bookingFlyoutLabel');
            if (label) {
                label.textContent = 'Edit booking confirmation';
            }
        }

        setInputValue('BookingInput_BookingId', options.bookingId ?? '');
        setInputValue('BookingInput_EntryId', options.entryId ?? '');
        if (options.bookingType) {
            setInputValue('BookingInput_BookingType', options.bookingType);
        }
        setInputValue('BookingInput_Vendor', options.vendor ?? '');
        setInputValue('BookingInput_Reference', options.reference ?? '');
        setInputValue('BookingInput_Cost', options.cost ?? '');
        setInputValue('BookingInput_Currency', options.currency ?? '');
        setInputValue('BookingInput_CancellationPolicy', options.cancellation ?? '');
        setInputValue('BookingInput_ConfirmationDetails', options.details ?? '');

        const refundable = document.getElementById('BookingInput_IsRefundable');
        if (refundable) {
            refundable.checked = options.refundable === 'true';
        }

        openOffcanvas('bookingFlyout');
    }

    function showBookingDetails(dataset) {
        const panel = document.getElementById('bookingDetailFlyout');
        if (!panel) {
            return;
        }

        const costText = formatBookingCost(dataset.bookingCost, dataset.bookingCurrency);
        setBookingDetail(panel, 'parent', dataset.bookingParentLabel || 'Linked item');
        const typeLabel = dataset.bookingTypeLabel || dataset.bookingType || '—';
        setBookingDetail(panel, 'type', typeLabel);
        setBookingDetail(panel, 'vendor', dataset.bookingVendor || '—');
        setBookingDetail(panel, 'reference', dataset.bookingReference || '—');
        setBookingDetail(panel, 'cost', costText);
        const refundableText = dataset.bookingRefundable === 'true'
            ? 'Yes'
            : dataset.bookingRefundable === 'false'
                ? 'No'
                : '—';
        setBookingDetail(panel, 'refundable', refundableText);
        setBookingDetail(panel, 'cancellation', dataset.bookingCancellation || '—');
        setBookingDetail(panel, 'notes', dataset.bookingDetails || '—');

        panel.dataset.bookingId = dataset.bookingId ?? '';
        panel.dataset.bookingParentType = dataset.bookingParentType ?? '';
        panel.dataset.bookingParentLabel = dataset.bookingParentLabel || '';
        panel.dataset.bookingEntryId = dataset.parentEntry ?? '';
        panel.dataset.bookingType = dataset.bookingType ?? '';
        panel.dataset.bookingTypeLabel = dataset.bookingTypeLabel ?? '';
        panel.dataset.bookingVendor = dataset.bookingVendor ?? '';
        panel.dataset.bookingReference = dataset.bookingReference ?? '';
        panel.dataset.bookingCost = dataset.bookingCost ?? '';
        panel.dataset.bookingCurrency = dataset.bookingCurrency ?? '';
        panel.dataset.bookingRefundable = dataset.bookingRefundable ?? '';
        panel.dataset.bookingCancellation = dataset.bookingCancellation ?? '';
        panel.dataset.bookingDetails = dataset.bookingDetails ?? '';

        const deleteInput = document.querySelector('#bookingDetailDeleteForm input[name="bookingId"]');
        if (deleteInput) {
            deleteInput.value = dataset.bookingId ?? '';
        }

        openOffcanvas('bookingDetailFlyout');
    }

    function initMultiDayToggle() {
        const checkbox = document.getElementById('EntryInput_IsMultiDay');
        if (!checkbox) {
            return;
        }

        checkbox.addEventListener('change', updateEntryEndDateVisibility);
        updateEntryEndDateVisibility();
    }

    function updateEntryEndDateVisibility() {
        const checkbox = document.getElementById('EntryInput_IsMultiDay');
        const endDateGroup = document.querySelector('[data-entry-end-date]');
        if (!checkbox || !endDateGroup) {
            return;
        }

        endDateGroup.classList.toggle('d-none', !checkbox.checked);
    }

    function setBookingDetail(panel, name, value) {
        const target = panel.querySelector(`[data-booking-detail="${name}"]`);
        if (!target) {
            return;
        }

        target.textContent = value && value.trim().length > 0 ? value : '—';
    }

    function formatBookingCost(amount, currency) {
        if (!amount || amount.trim().length === 0) {
            return '—';
        }

        if (currency) {
            return `${currency.toUpperCase()} ${amount}`;
        }

        return amount;
    }

    function openOffcanvas(id) {
        const element = document.getElementById(id);
        if (!element) {
            return;
        }

        const instance = bootstrap.Offcanvas.getOrCreateInstance(element);
        instance.show();
    }

    function closeOffcanvas(id) {
        const element = document.getElementById(id);
        if (!element) {
            return;
        }

        const instance = bootstrap.Offcanvas.getInstance(element);
        instance?.hide();
    }

    function reopenOffcanvasOnValidation() {
        const entryErrors = document.querySelector('#entry-form .validation-summary-errors');
        const bookingErrors = document.querySelector('#booking-form .validation-summary-errors');

        if (entryErrors) {
            openOffcanvas('entryFlyout');
        } else if (bookingErrors) {
            openOffcanvas('bookingFlyout');
        }
    }
})();
