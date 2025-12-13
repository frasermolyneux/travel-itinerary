(() => {
    let syncEntryBounds = () => { };
    let syncEntryMetadata = () => { };

    document.addEventListener('DOMContentLoaded', () => {
        initTimelineSelection();
        initFabButton();
        initEditButtons();
        initBookingButtons();
        initMultiDayToggle();
        initEntryDateBounds();
        initMetadataSections();
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

    function initFabButton() {
        const button = document.querySelector('[data-fab-open-entry]');
        if (!button) {
            return;
        }

        button.addEventListener('click', () => {
            resetEntryForm();
            openOffcanvas('entryFlyout');
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
                    categoryLabel: button.dataset.entryTypeLabel,
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
                categoryLabel: detailPane.dataset.bookingTypeLabel,
                vendor: detailPane.dataset.bookingVendor,
                reference: detailPane.dataset.bookingReference,
                cost: detailPane.dataset.bookingCost,
                currency: detailPane.dataset.bookingCurrency,
                refundable: detailPane.dataset.bookingRefundable,
                paid: detailPane.dataset.bookingPaid,
                cancellation: detailPane.dataset.bookingCancellation,
                details: detailPane.dataset.bookingDetails,
                confirmationUrl: detailPane.dataset.bookingConfirmationUrl
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
        setInputValue('EntryInput_ItemType', dataset.entryType ?? 'Tour');
        setInputValue('EntryInput_Details', dataset.entryDetails ?? '');
        setEntryMetadataFields(dataset);

        const multiDay = dataset.entryIsMultiDay === 'true';
        const multiDayToggle = document.getElementById('EntryInput_IsMultiDay');
        if (multiDayToggle) {
            multiDayToggle.checked = multiDay;
        }
        updateEntryEndDateVisibility();
        syncEntryBounds();
        syncEntryMetadata();

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
        setInputValue('EntryInput_ItemType', 'Tour');
        setInputValue('EntryInput_Details', '');
        setEntryMetadataFields({});

        const multiDayToggle = document.getElementById('EntryInput_IsMultiDay');
        if (multiDayToggle) {
            multiDayToggle.checked = false;
        }
        updateEntryEndDateVisibility();
        syncEntryBounds();
        syncEntryMetadata();

        const label = document.getElementById('entryFlyoutLabel');
        if (label) {
            label.textContent = 'Add itinerary entry';
        }
    }

    function resetBookingForm() {
        setInputValue('BookingInput_BookingId', '');
        setInputValue('BookingInput_EntryId', '');
        setInputValue('BookingInput_Vendor', '');
        setInputValue('BookingInput_Reference', '');
        setInputValue('BookingInput_Cost', '');
        setInputValue('BookingInput_Currency', '');
        setInputValue('BookingInput_CancellationPolicy', '');
        setInputValue('BookingInput_ConfirmationDetails', '');
        setInputValue('BookingInput_ConfirmationUrl', '');
        setBookingCategoryDisplay('—');

        const refundable = document.getElementById('BookingInput_IsRefundable');
        if (refundable) {
            refundable.checked = false;
        }

        const paid = document.getElementById('BookingInput_IsPaid');
        if (paid) {
            paid.checked = false;
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

    function setEntryMetadataFields(dataset) {
        setInputValue('EntryInput_FlightMetadata_Airline', dataset.entryFlightAirline ?? '');
        setInputValue('EntryInput_FlightMetadata_FlightNumber', dataset.entryFlightNumber ?? '');
        setInputValue('EntryInput_FlightMetadata_DepartureAirport', dataset.entryFlightDepartureAirport ?? '');
        setInputValue('EntryInput_FlightMetadata_DepartureTime', dataset.entryFlightDepartureTime ?? '');
        setInputValue('EntryInput_FlightMetadata_ArrivalAirport', dataset.entryFlightArrivalAirport ?? '');
        setInputValue('EntryInput_FlightMetadata_ArrivalTime', dataset.entryFlightArrivalTime ?? '');
        setInputValue('EntryInput_FlightMetadata_Seat', dataset.entryFlightSeat ?? '');
        setInputValue('EntryInput_FlightMetadata_ConfirmationNumber', dataset.entryFlightConfirmation ?? '');

        setInputValue('EntryInput_StayMetadata_PropertyName', dataset.entryStayProperty ?? '');
        setInputValue('EntryInput_StayMetadata_Address', dataset.entryStayAddress ?? '');
        setInputValue('EntryInput_StayMetadata_CheckInTime', dataset.entryStayCheckIn ?? '');
        setInputValue('EntryInput_StayMetadata_CheckOutTime', dataset.entryStayCheckOut ?? '');
        setInputValue('EntryInput_StayMetadata_RoomType', dataset.entryStayRoom ?? '');
        setInputValue('EntryInput_StayMetadata_ConfirmationNumber', dataset.entryStayConfirmation ?? '');
        setInputValue('EntryInput_StayMetadata_ContactInfo', dataset.entryStayContact ?? '');
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
        setInputValue('BookingInput_Vendor', options.vendor ?? '');
        setInputValue('BookingInput_Reference', options.reference ?? '');
        setInputValue('BookingInput_Cost', options.cost ?? '');
        setInputValue('BookingInput_Currency', options.currency ?? '');
        setInputValue('BookingInput_CancellationPolicy', options.cancellation ?? '');
        setInputValue('BookingInput_ConfirmationDetails', options.details ?? '');
        setInputValue('BookingInput_ConfirmationUrl', options.confirmationUrl ?? '');
        setBookingCategoryDisplay(options.categoryLabel || 'Linked itinerary entry');

        const refundable = document.getElementById('BookingInput_IsRefundable');
        if (refundable) {
            refundable.checked = options.refundable === 'true';
        }

        const paid = document.getElementById('BookingInput_IsPaid');
        if (paid) {
            paid.checked = options.paid === 'true';
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
        setBookingDetail(panel, 'refundable', formatBooleanFlag(dataset.bookingRefundable));
        setBookingDetail(panel, 'paid', formatBooleanFlag(dataset.bookingPaid));
        setBookingDetail(panel, 'cancellation', dataset.bookingCancellation || '—');
        setBookingDetail(panel, 'notes', dataset.bookingDetails || '—');
        setBookingLink(panel, dataset.bookingConfirmationUrl);

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
        panel.dataset.bookingPaid = dataset.bookingPaid ?? '';
        panel.dataset.bookingCancellation = dataset.bookingCancellation ?? '';
        panel.dataset.bookingDetails = dataset.bookingDetails ?? '';
        panel.dataset.bookingConfirmationUrl = dataset.bookingConfirmationUrl ?? '';

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

    function initEntryDateBounds() {
        const startInput = document.getElementById('EntryInput_Date');
        const endInput = document.getElementById('EntryInput_EndDate');
        if (!startInput || !endInput) {
            return;
        }

        const tripStart = startInput.getAttribute('min') || '';
        const tripEnd = startInput.getAttribute('max') || '';

        syncEntryBounds = () => {
            const currentStart = startInput.value || '';
            const minForEnd = currentStart || tripStart;

            if (minForEnd) {
                endInput.min = minForEnd;
            } else {
                endInput.removeAttribute('min');
            }

            if (tripStart) {
                startInput.min = tripStart;
            } else {
                startInput.removeAttribute('min');
            }

            if (startInput.value && tripStart && startInput.value < tripStart) {
                startInput.value = tripStart;
            }

            if (tripEnd) {
                startInput.max = tripEnd;
                endInput.max = tripEnd;
            } else {
                startInput.removeAttribute('max');
                endInput.removeAttribute('max');
            }

            if (endInput.value && endInput.min && endInput.value < endInput.min) {
                endInput.value = endInput.min;
            }

            if (endInput.value && endInput.max && endInput.value > endInput.max) {
                endInput.value = endInput.max;
            }

            if (startInput.value && startInput.max && startInput.value > startInput.max) {
                startInput.value = startInput.max;
            }
        };

        startInput.addEventListener('change', syncEntryBounds);
        syncEntryBounds();
    }

    function initMetadataSections() {
        const typeSelect = document.getElementById('EntryInput_ItemType');
        const sections = document.querySelectorAll('[data-entry-metadata-section]');
        if (!typeSelect || sections.length === 0) {
            return;
        }

        const stayTypes = ['Hotel', 'Flat', 'House'];
        syncEntryMetadata = () => {
            const currentType = typeSelect.value || '';
            sections.forEach((section) => {
                const target = section.dataset.entryMetadataSection;
                const showFlight = target === 'flight' && currentType === 'Flight';
                const showStay = target === 'stay' && stayTypes.includes(currentType);
                section.classList.toggle('d-none', !(showFlight || showStay));
            });
        };

        typeSelect.addEventListener('change', syncEntryMetadata);
        syncEntryMetadata();
    }

    function setBookingDetail(panel, name, value) {
        const target = panel.querySelector(`[data-booking-detail="${name}"]`);
        if (!target) {
            return;
        }

        target.textContent = value && value.trim().length > 0 ? value : '—';
    }

    function setBookingLink(panel, url) {
        const link = panel.querySelector('[data-booking-link]');
        const placeholder = panel.querySelector('[data-booking-link-placeholder]');
        if (!link || !placeholder) {
            return;
        }

        if (url && url.trim().length > 0) {
            link.href = url;
            link.textContent = url;
            link.classList.remove('d-none');
            placeholder.classList.add('d-none');
        } else {
            link.href = '#';
            link.textContent = '';
            link.classList.add('d-none');
            placeholder.classList.remove('d-none');
            placeholder.textContent = '—';
        }
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

    function formatBooleanFlag(value) {
        if (value === 'true') {
            return 'Yes';
        }

        if (value === 'false') {
            return 'No';
        }

        return '—';
    }

    function openOffcanvas(id) {
        const element = document.getElementById(id);
        if (!element) {
            return;
        }

        const instance = bootstrap.Offcanvas.getOrCreateInstance(element);
        instance.show();
    }

    function setBookingCategoryDisplay(label) {
        const target = document.getElementById('bookingCategoryDisplay');
        if (!target) {
            return;
        }

        target.textContent = label && label.trim().length > 0 ? label : '—';
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
            const entryInput = document.getElementById('BookingInput_EntryId');
            const entryId = entryInput?.value || '';
            if (entryId) {
                const addButton = document.querySelector(`[data-booking-action="add"][data-entry-id="${entryId}"]`);
                const viewButton = document.querySelector(`[data-booking-action="view"][data-parent-entry="${entryId}"]`);
                const label = addButton?.dataset.entryTypeLabel || viewButton?.dataset.bookingTypeLabel;
                if (label) {
                    setBookingCategoryDisplay(label);
                }
            }
            openOffcanvas('bookingFlyout');
        }
    }
})();
