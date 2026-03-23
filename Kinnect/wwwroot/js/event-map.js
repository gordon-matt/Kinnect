(async function () {
    const personId = window.eventMapPersonId;
    if (!personId) return;

    const map = L.map('eventMap').setView([20, 0], 2);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors',
        maxZoom: 19
    }).addTo(map);

    let events = [];
    try {
        const res = await fetch(`/api/people/${personId}/events`);
        if (!res.ok) return;
        const data = await res.json();
        events = data.value || data || [];
    } catch (err) {
        console.error('Failed to load events', err);
        return;
    }

    // Helper: sort key for an event (year * 10000 + month * 100 + day)
    function sortKey(ev) {
        if (!ev.year) return Infinity;
        return (ev.year ?? 0) * 10000 + (ev.month ?? 1) * 100 + (ev.day ?? 1);
    }

    // Build filter checkboxes from the event types actually present in the data
    const EVENT_TYPE_LABELS = {
        BIRT: 'Birth', DEAT: 'Death', BURI: 'Burial', CREM: 'Cremation',
        BAPM: 'Baptism', CHR: 'Christening', CONF: 'Confirmation', ADOP: 'Adoption',
        EMIG: 'Emigration', IMMI: 'Immigration', NATU: 'Naturalization',
        MARB: 'Marriage Banns', MARL: 'Marriage Licence', RESI: 'Residence', EVEN: 'Custom Event'
    };

    const presentTypes = [...new Set(events.map(ev => ev.eventType))].sort();
    const activeTypes = new Set(presentTypes);

    const filterContainer = document.getElementById('eventTypeChecks');
    if (filterContainer && presentTypes.length > 0) {
        presentTypes.forEach(code => {
            const label = EVENT_TYPE_LABELS[code] || code;
            const div = document.createElement('div');
            div.className = 'form-check form-check-sm';
            div.innerHTML = `
                <input class="form-check-input" type="checkbox" id="et_${code}" checked />
                <label class="form-check-label small" for="et_${code}">${label}</label>`;
            filterContainer.appendChild(div);
            div.querySelector('input').addEventListener('change', (e) => {
                if (e.target.checked) activeTypes.add(code);
                else activeTypes.delete(code);
                renderMarkers();
            });
        });
    }

    // Marker layer group so we can clear and re-render
    let markerLayer = L.layerGroup().addTo(map);
    let polylineLayer = L.layerGroup().addTo(map);

    function renderMarkers() {
        markerLayer.clearLayers();
        polylineLayer.clearLayers();

        const visible = events.filter(ev => activeTypes.has(ev.eventType));
        const withCoords = visible.filter(ev => ev.latitude != null && ev.longitude != null);
        const withoutCoords = visible.filter(ev => ev.latitude == null || ev.longitude == null);

        // Events with dates (and coords) connected by a line, sorted chronologically
        const datedWithCoords = withCoords
            .filter(ev => ev.year != null)
            .sort((a, b) => sortKey(a) - sortKey(b));

        const undatedWithCoords = withCoords.filter(ev => ev.year == null);

        const bounds = [];

        // Draw connecting polyline for dated events
        if (datedWithCoords.length >= 2) {
            const latlngs = datedWithCoords.map(ev => [ev.latitude, ev.longitude]);
            L.polyline(latlngs, { color: '#0d6efd', weight: 2, opacity: 0.6 }).addTo(polylineLayer);
        }

        // Add markers for events with coordinates
        for (const ev of [...datedWithCoords, ...undatedWithCoords]) {
            const lat = ev.latitude;
            const lng = ev.longitude;
            bounds.push([lat, lng]);

            const dateStr = ev.year
                ? (ev.month && ev.day
                    ? `${ev.year}-${String(ev.month).padStart(2,'0')}-${String(ev.day).padStart(2,'0')}`
                    : String(ev.year))
                : 'Date unknown';

            const popupHtml = `
                <strong>${ev.eventTypeLabel || ev.eventType}</strong>
                <div>${dateStr}</div>
                ${ev.place ? `<div><i class='bi bi-geo-alt'></i> ${ev.place}</div>` : ''}
                ${ev.description ? `<div class='text-muted'>${ev.description}</div>` : ''}
            `;

            const tooltipLabel = `${ev.eventTypeLabel || ev.eventType}${ev.year ? ' (' + ev.year + ')' : ''}`;
            L.marker([lat, lng])
                .addTo(markerLayer)
                .bindPopup(popupHtml)
                .bindTooltip(tooltipLabel, { permanent: false, direction: 'top' });
        }

        // Summarise events without coords below the map
        const noLocContainer = document.getElementById('noLocSummary');
        if (noLocContainer) {
            if (withoutCoords.length > 0) {
                noLocContainer.innerHTML = `
                    <p class="text-muted small mb-1"><i class="bi bi-info-circle"></i>
                    ${withoutCoords.length} event(s) have no location and are not shown on the map:</p>
                    <ul class="list-unstyled small text-muted ps-3">
                        ${withoutCoords.map(ev => {
                            const d = ev.year
                                ? (ev.month ? `${ev.year}-${String(ev.month).padStart(2,'0')}` : String(ev.year))
                                : '';
                            return `<li>${ev.eventTypeLabel || ev.eventType}${d ? ' (' + d + ')' : ''}${ev.place ? ' — ' + ev.place : ''}</li>`;
                        }).join('')}
                    </ul>`;
            } else {
                noLocContainer.innerHTML = '';
            }
        }
    }

    renderMarkers();

    // Fit to all coords on first render
    const allCoords = events
        .filter(ev => ev.latitude != null && ev.longitude != null)
        .map(ev => [ev.latitude, ev.longitude]);
    if (allCoords.length > 0) {
        map.fitBounds(allCoords, { padding: [40, 40] });
    }
})();
