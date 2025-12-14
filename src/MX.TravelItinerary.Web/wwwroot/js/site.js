(() => {
    const telemetry = {
        track(eventName, properties = {}) {
            if (!eventName || !window.appInsights || typeof window.appInsights.trackEvent !== 'function') {
                return;
            }

            try {
                window.appInsights.trackEvent({ name: eventName }, properties);
            } catch (error) {
                console.warn('Unable to emit telemetry', error);
            }
        }
    };

    const stayCategoryTypes = ['Hotel', 'Flat', 'House'];

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
        initTimelineReorder();
        initCurrencyPicker();
        initBookingRefundableToggle();
        reopenOffcanvasOnValidation();
        const initialItemType = document.getElementById('BookingInput_ItemType')?.value || '';
        toggleBookingStaySection(initialItemType);
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

                if (event.target.closest('[data-drag-handle]')) {
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

        const timelineRoot = document.querySelector('[data-timeline-root]');
        const tripDefaultCurrency = timelineRoot?.dataset.tripDefaultCurrency || '';

        document.querySelectorAll('[data-booking-action="add"]').forEach((button) => {
            button.addEventListener('click', (event) => {
                event.stopPropagation();
                openBookingForm({
                    mode: 'create',
                    parentType: button.dataset.parentType,
                    entryId: button.dataset.entryId,
                    categoryLabel: button.dataset.entryTypeLabel,
                    title: button.dataset.entryTitle || button.dataset.segmentTitle || '',
                    defaultCurrency: button.dataset.defaultCurrency || tripDefaultCurrency,
                    itemType: button.dataset.entryType,
                    includes: '',
                    cancelBy: ''
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
                cancelBy: detailPane.dataset.bookingCancellationBy,
                details: detailPane.dataset.bookingDetails,
                confirmationUrl: detailPane.dataset.bookingConfirmationUrl,
                checkIn: detailPane.dataset.bookingCheckIn,
                checkOut: detailPane.dataset.bookingCheckOut,
                room: detailPane.dataset.bookingRoom,
                includes: detailPane.dataset.bookingIncludes,
                itemType: detailPane.dataset.bookingType
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
        setInputValue('BookingInput_CancellationByDate', '');
        setInputValue('BookingInput_ConfirmationDetails', '');
        setInputValue('BookingInput_ConfirmationUrl', '');
        setInputValue('BookingInput_StayCheckInTime', '');
        setInputValue('BookingInput_StayCheckOutTime', '');
        setInputValue('BookingInput_StayRoomType', '');
        setMultiSelectValues('BookingInput_StayIncludes', []);
        const defaultItemType = normalizeItemType('');
        setInputValue('BookingInput_ItemType', defaultItemType);
        setBookingCategoryDisplay('—');
        toggleBookingStaySection(defaultItemType);

        const refundable = document.getElementById('BookingInput_IsRefundable');
        if (refundable) {
            refundable.checked = false;
        }
        toggleBookingRefundableSection(false);

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

    function setMultiSelectValues(id, values) {
        const select = document.getElementById(id);
        if (!select) {
            return;
        }

        const normalizedValues = Array.isArray(values)
            ? values
                .map((value) => (value ?? '').toString().trim().toLowerCase())
                .filter((value) => value.length > 0)
            : [];

        const lookup = new Set(normalizedValues);
        Array.from(select.options).forEach((option) => {
            const optionValue = (option.value ?? '').toString().trim().toLowerCase();
            option.selected = lookup.size > 0 && lookup.has(optionValue);
        });
    }

    function normalizeItemType(value) {
        const normalized = (value ?? '').toString().trim();
        return normalized.length > 0 ? normalized : 'Other';
    }

    function isStayItemType(value) {
        const normalized = normalizeItemType(value).toLowerCase();
        return stayCategoryTypes.some((type) => type.toLowerCase() === normalized);
    }

    function toggleStaySection(section, itemType) {
        if (!section) {
            return;
        }

        const shouldShow = isStayItemType(itemType);
        section.classList.toggle('d-none', !shouldShow);
    }

    function toggleBookingStaySection(itemType) {
        const section = document.querySelector('[data-booking-stay-section]');
        toggleStaySection(section, itemType);
    }

    function toggleBookingDetailStaySection(itemType) {
        const section = document.querySelector('[data-booking-detail-stay-section]');
        toggleStaySection(section, itemType);
    }

    function initBookingRefundableToggle() {
        const checkbox = document.getElementById('BookingInput_IsRefundable');
        if (!checkbox) {
            return;
        }

        checkbox.addEventListener('change', () => {
            toggleBookingRefundableSection(checkbox.checked);
        });

        toggleBookingRefundableSection(checkbox.checked);
    }

    function toggleBookingRefundableSection(isRefundable) {
        const section = document.querySelector('[data-booking-refundable-section]');
        if (!section) {
            return;
        }

        section.classList.toggle('d-none', !isRefundable);
    }

    function setEntryMetadataFields(dataset) {
        setInputValue('EntryInput_FlightMetadata_Airline', dataset.entryFlightAirline ?? '');
        setInputValue('EntryInput_FlightMetadata_FlightNumber', dataset.entryFlightNumber ?? '');
        setInputValue('EntryInput_FlightMetadata_DepartureAirport', dataset.entryFlightDepartureAirport ?? '');
        setInputValue('EntryInput_FlightMetadata_DepartureTime', dataset.entryFlightDepartureTime ?? '');
        setInputValue('EntryInput_FlightMetadata_ArrivalAirport', dataset.entryFlightArrivalAirport ?? '');
        setInputValue('EntryInput_FlightMetadata_ArrivalTime', dataset.entryFlightArrivalTime ?? '');

        setInputValue('EntryInput_StayMetadata_PropertyName', dataset.entryStayProperty ?? '');
        setInputValue('EntryInput_StayMetadata_PropertyLink', dataset.entryStayLink ?? '');
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
        const currencyValueRaw = options.currency ?? options.defaultCurrency ?? '';
        const currencyValue = currencyValueRaw ? currencyValueRaw.toString().trim().toUpperCase() : '';
        setInputValue('BookingInput_Currency', currencyValue);
        setInputValue('BookingInput_CancellationPolicy', options.cancellation ?? '');
        setInputValue('BookingInput_CancellationByDate', options.cancelBy ?? '');
        setInputValue('BookingInput_ConfirmationDetails', options.details ?? '');
        setInputValue('BookingInput_ConfirmationUrl', options.confirmationUrl ?? '');
        setBookingCategoryDisplay(options.categoryLabel || 'Linked itinerary entry');
        setInputValue('BookingInput_StayCheckInTime', options.checkIn ?? '');
        setInputValue('BookingInput_StayCheckOutTime', options.checkOut ?? '');
        setInputValue('BookingInput_StayRoomType', options.room ?? '');
        setMultiSelectValues('BookingInput_StayIncludes', parseBookingIncludes(options.includes));
        const itemTypeValue = normalizeItemType(options.itemType);
        setInputValue('BookingInput_ItemType', itemTypeValue);
        toggleBookingStaySection(itemTypeValue);

        const refundable = document.getElementById('BookingInput_IsRefundable');
        if (refundable) {
            refundable.checked = options.refundable === 'true';
            toggleBookingRefundableSection(refundable.checked);
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
        setBookingDetail(panel, 'checkin', dataset.bookingCheckIn || '—');
        setBookingDetail(panel, 'checkout', dataset.bookingCheckOut || '—');
        setBookingDetail(panel, 'room', dataset.bookingRoom || '—');
        setBookingDetail(panel, 'includes', formatBookingIncludes(dataset.bookingIncludes));
        setBookingDetail(panel, 'refundable', formatBooleanFlag(dataset.bookingRefundable));
        setBookingDetail(panel, 'paid', formatBooleanFlag(dataset.bookingPaid));
        const cancelByText = formatCancellationByDate(dataset.bookingCancellationBy);
        setBookingDetail(panel, 'cancelby', cancelByText);
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
        panel.dataset.bookingCheckIn = dataset.bookingCheckIn ?? '';
        panel.dataset.bookingCheckOut = dataset.bookingCheckOut ?? '';
        panel.dataset.bookingRoom = dataset.bookingRoom ?? '';
        panel.dataset.bookingIncludes = dataset.bookingIncludes ?? '';
        panel.dataset.bookingRefundable = dataset.bookingRefundable ?? '';
        panel.dataset.bookingPaid = dataset.bookingPaid ?? '';
        panel.dataset.bookingCancellationBy = dataset.bookingCancellationBy ?? '';
        panel.dataset.bookingCancellation = dataset.bookingCancellation ?? '';
        panel.dataset.bookingDetails = dataset.bookingDetails ?? '';
        panel.dataset.bookingConfirmationUrl = dataset.bookingConfirmationUrl ?? '';
        toggleBookingDetailStaySection(dataset.bookingType ?? '');

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

        syncEntryMetadata = () => {
            const currentType = typeSelect.value || '';
            sections.forEach((section) => {
                const target = section.dataset.entryMetadataSection;
                const showFlight = target === 'flight' && currentType === 'Flight';
                const showStay = target === 'stay' && stayCategoryTypes.includes(currentType);
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

    function formatCancellationByDate(value) {
        if (!value || value.trim().length === 0) {
            return '';
        }

        const parsed = new Date(value);
        if (Number.isNaN(parsed.getTime())) {
            return value;
        }

        return parsed.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
    }

    function parseBookingIncludes(value) {
        if (!value || typeof value !== 'string') {
            return [];
        }

        return value
            .split('|')
            .map((part) => part.trim())
            .filter((part) => part.length > 0);
    }

    function formatBookingIncludes(value) {
        const includes = parseBookingIncludes(value);
        return includes.length > 0 ? includes.join(', ') : '—';
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

    function initTimelineReorder() {
        const root = document.querySelector('[data-timeline-root][data-allow-reorder="true"]');
        if (!root || root.dataset.allowReorder !== 'true') {
            return;
        }

        const tripId = root.dataset.tripId || '';
        const dayContainers = Array.from(root.querySelectorAll('[data-day-sortable]'));
        if (!tripId || dayContainers.length === 0) {
            return;
        }

        const token = getAntiForgeryToken();
        if (!token) {
            console.warn('Timeline reorder disabled because no anti-forgery token was found.');
            return;
        }

        const reorderUrl = root.dataset.reorderUrl || '?handler=ReorderEntries';
        let draggedEntry;
        let originContainer;

        root.querySelectorAll('[data-sortable-entry]').forEach((entry) => {
            entry.addEventListener('dragstart', (event) => {
                draggedEntry = entry;
                originContainer = entry.closest('[data-day-sortable]') || null;
                entry.classList.add('is-dragging');
                if (event.dataTransfer) {
                    event.dataTransfer.effectAllowed = 'move';
                    event.dataTransfer.setData('text/plain', entry.dataset.entryId || '');
                }
            });

            entry.addEventListener('dragend', () => {
                if (!draggedEntry) {
                    return;
                }

                const currentEntry = draggedEntry;
                const container = originContainer;
                draggedEntry = null;
                originContainer = null;
                currentEntry.classList.remove('is-dragging');

                if (!container) {
                    return;
                }

                if (currentEntry.closest('[data-day-sortable]') !== container) {
                    restoreOrder(container, container.dataset.currentOrder || '');
                    return;
                }

                persistDayOrder(container);
            });
        });

        dayContainers.forEach((container) => {
            container.addEventListener('dragover', (event) => {
                if (!draggedEntry || originContainer !== container) {
                    return;
                }

                event.preventDefault();
                const target = event.target.closest('[data-sortable-entry]');
                if (!target) {
                    container.appendChild(draggedEntry);
                    return;
                }

                if (target === draggedEntry) {
                    return;
                }

                const rect = target.getBoundingClientRect();
                const shouldInsertAfter = (event.clientY - rect.top) > rect.height / 2;
                if (shouldInsertAfter) {
                    container.insertBefore(draggedEntry, target.nextElementSibling);
                } else {
                    container.insertBefore(draggedEntry, target);
                }
            });

            container.addEventListener('drop', (event) => {
                event.preventDefault();
            });
        });

        function persistDayOrder(container) {
            const entryIds = collectEntryIds(container);
            const nextOrder = entryIds.join(',');
            const previousOrder = container.dataset.currentOrder || '';
            const date = container.dataset.dayDate || '';

            if (!date || nextOrder === previousOrder) {
                return;
            }

            container.classList.add('is-saving');

            fetch(reorderUrl, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify({ tripId, date, entryIds })
            })
                .then((response) => {
                    if (!response.ok) {
                        telemetry.track('timeline:reorder:failed', {
                            tripId,
                            date,
                            entryCount: entryIds.length.toString(),
                            status: response.status.toString()
                        });
                        throw new Error('Failed to persist order.');
                    }
                    container.dataset.currentOrder = nextOrder;
                    telemetry.track('timeline:reorder:saved', {
                        tripId,
                        date,
                        entryCount: entryIds.length.toString()
                    });
                })
                .catch((error) => {
                    console.error(error);
                    restoreOrder(container, previousOrder);
                    window.alert('We could not save the new order. Please try again.');
                    telemetry.track('timeline:reorder:failed', {
                        tripId,
                        date,
                        entryCount: entryIds.length.toString(),
                        reason: error?.message || 'unknown'
                    });
                })
                .finally(() => {
                    container.classList.remove('is-saving');
                });
        }

        function collectEntryIds(container) {
            return Array.from(container.querySelectorAll('[data-sortable-entry]'))
                .map((entry) => entry.dataset.entryId)
                .filter((id) => !!id);
        }

        function restoreOrder(container, order) {
            if (!order) {
                return;
            }

            const entryIds = order.split(',').filter((id) => id);
            entryIds.forEach((entryId) => {
                const entry = container.querySelector(`[data-entry-id="${entryId}"]`);
                if (entry) {
                    container.appendChild(entry);
                }
            });
        }
    }

    function getAntiForgeryToken() {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input?.value || '';
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
            const currentType = document.getElementById('BookingInput_ItemType')?.value || '';
            toggleBookingStaySection(currentType);
        }
    }

    function initCurrencyPicker() {
        const inputs = Array.from(document.querySelectorAll('[data-currency-input]'));
        if (inputs.length === 0) {
            return;
        }

        const dataElement = document.getElementById('currencyCatalogData');
        if (!dataElement) {
            console.warn('Currency picker catalog data was not found.');
            return;
        }

        let catalogRaw;
        try {
            catalogRaw = JSON.parse(dataElement.textContent || '[]');
        } catch (error) {
            console.warn('Unable to parse currency catalog data.', error);
            return;
        }

        if (!Array.isArray(catalogRaw) || catalogRaw.length === 0) {
            return;
        }

        const catalog = catalogRaw
            .map((item) => normalizeCurrencyItem(item))
            .filter((item) => !!item);

        if (catalog.length === 0) {
            return;
        }

        const catalogByCode = new Map(catalog.map((item) => [item.code, item]));

        inputs.forEach((input) => setupCurrencyPicker(input, catalog, catalogByCode));
    }

    function normalizeCurrencyItem(item) {
        if (!item) {
            return null;
        }

        const rawCode = item.code ?? item.Code ?? '';
        const code = rawCode.toString().trim().toUpperCase();
        if (!code) {
            return null;
        }

        const rawDisplayName = item.displayName ?? item.DisplayName ?? item.name ?? item.Name ?? code;
        const displayName = rawDisplayName.toString().trim();
        const rawTerms = item.searchTerms ?? item.SearchTerms ?? [];
        const searchTerms = Array.isArray(rawTerms) ? rawTerms : [];
        const combined = [code, displayName, ...searchTerms]
            .map((term) => (term || '').toString().trim())
            .filter((term) => term.length > 0);

        return {
            code,
            name: displayName,
            search: combined.join(' ').toLowerCase()
        };
    }

    function setupCurrencyPicker(input, catalog, catalogByCode) {
        if (!input || input.dataset.currencyReady === 'true') {
            return;
        }

        input.dataset.currencyReady = 'true';
        input.setAttribute('autocomplete', 'off');
        input.setAttribute('spellcheck', 'false');

        const wrapper = document.createElement('div');
        wrapper.className = 'currency-picker';
        input.parentNode.insertBefore(wrapper, input);
        wrapper.appendChild(input);

        const dropdown = document.createElement('div');
        dropdown.className = 'currency-picker-dropdown';
        wrapper.appendChild(dropdown);

        let suggestions = [];
        let highlightedIndex = -1;

        const renderSuggestions = () => {
            suggestions = filterCurrencyCatalog(input.value, catalog, catalogByCode);
            dropdown.innerHTML = '';
            highlightedIndex = -1;

            if (suggestions.length === 0) {
                dropdown.classList.remove('is-open');
                return;
            }

            suggestions.forEach((option, index) => {
                const row = document.createElement('div');
                row.className = 'currency-picker-option';
                row.setAttribute('role', 'button');
                row.tabIndex = -1;

                const nameSpan = document.createElement('span');
                nameSpan.className = 'currency-picker-option-name';
                nameSpan.textContent = option.name;

                const codeSpan = document.createElement('span');
                codeSpan.className = 'currency-picker-option-code';
                codeSpan.textContent = option.code;

                row.appendChild(nameSpan);
                row.appendChild(codeSpan);

                row.addEventListener('mousedown', (event) => {
                    event.preventDefault();
                    selectSuggestion(option);
                });

                dropdown.appendChild(row);

                if (index === 0) {
                    row.classList.add('is-highlighted');
                    highlightedIndex = 0;
                }
            });

            dropdown.classList.add('is-open');
        };

        const selectSuggestion = (option) => {
            input.value = option.code;
            dropdown.classList.remove('is-open');
        };

        const moveHighlight = (direction) => {
            if (!dropdown.classList.contains('is-open') || suggestions.length === 0) {
                return;
            }

            highlightedIndex = (highlightedIndex + direction + suggestions.length) % suggestions.length;
            const rows = dropdown.querySelectorAll('.currency-picker-option');
            rows.forEach((row, index) => {
                const isActive = index === highlightedIndex;
                row.classList.toggle('is-highlighted', isActive);
                if (isActive) {
                    row.scrollIntoView({ block: 'nearest' });
                }
            });
        };

        const closeDropdown = () => {
            dropdown.classList.remove('is-open');
        };

        input.addEventListener('focus', () => {
            renderSuggestions();
        });

        input.addEventListener('input', () => {
            renderSuggestions();
        });

        input.addEventListener('keydown', (event) => {
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                moveHighlight(1);
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                moveHighlight(-1);
            } else if (event.key === 'Enter') {
                if (highlightedIndex >= 0 && suggestions[highlightedIndex]) {
                    event.preventDefault();
                    selectSuggestion(suggestions[highlightedIndex]);
                }
            } else if (event.key === 'Escape') {
                closeDropdown();
            }
        });

        input.addEventListener('blur', () => {
            setTimeout(() => {
                closeDropdown();
                input.value = coerceCurrencyValue(input.value, catalog, catalogByCode);
            }, 150);
        });

        document.addEventListener('click', (event) => {
            if (!wrapper.contains(event.target)) {
                closeDropdown();
            }
        });
    }

    function filterCurrencyCatalog(query, catalog, catalogByCode) {
        const trimmed = (query || '').trim();
        if (!trimmed) {
            return catalog.slice(0, 10);
        }

        const queryUpper = trimmed.toUpperCase();
        const queryLower = trimmed.toLowerCase();
        const directHit = catalogByCode.get(queryUpper);

        const ranked = catalog
            .map((item) => ({ item, score: scoreCurrencyMatch(item, queryLower, queryUpper) }))
            .filter((entry) => entry.score !== Number.POSITIVE_INFINITY)
            .sort((a, b) => {
                if (a.score === b.score) {
                    return a.item.name.localeCompare(b.item.name, undefined, { sensitivity: 'base' });
                }
                return a.score - b.score;
            })
            .map((entry) => entry.item);

        let results = ranked;
        if (results.length === 0) {
            results = catalog.slice(0, 10);
        }

        if (directHit) {
            results = [directHit, ...results.filter((item) => item.code !== directHit.code)];
        }

        return results.slice(0, 10);
    }

    function scoreCurrencyMatch(item, queryLower, queryUpper) {
        if (!queryLower) {
            return 50;
        }

        if (item.code === queryUpper) {
            return 0;
        }

        if (item.code.startsWith(queryUpper)) {
            return 1;
        }

        if (item.name.toLowerCase().startsWith(queryLower)) {
            return 2;
        }

        if (item.search.includes(queryLower)) {
            return 5;
        }

        return Number.POSITIVE_INFINITY;
    }

    function coerceCurrencyValue(value, catalog, catalogByCode) {
        const trimmed = (value || '').trim();
        if (!trimmed) {
            return '';
        }

        const upper = trimmed.toUpperCase();
        if (catalogByCode.has(upper)) {
            return upper;
        }

        const matches = filterCurrencyCatalog(trimmed, catalog, catalogByCode);
        const normalized = matches.find((match) => match.search.includes(trimmed.toLowerCase()));
        if (normalized) {
            return normalized.code;
        }

        return upper;
    }
})();
