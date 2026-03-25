(async function () {
    const personId = window.photoMapPersonId;
    if (!personId) return;

    const map = L.map('photoMap').setView([20, 0], 2);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors',
        maxZoom: 19
    }).addTo(map);

    let photos = [];
    try {
        const res = await fetch(`/api/photos/person/${personId}`);
        if (!res.ok) return;
        const data = await res.json();
        photos = data.value || data || [];
    } catch (err) {
        console.error('Failed to load photos', err);
        return;
    }

    const withCoords = photos.filter(p => p?.latitude != null && p?.longitude != null);
    const withoutCoords = photos.filter(p => p?.latitude == null || p?.longitude == null);

    const markerLayer = L.layerGroup().addTo(map);

    function buildPopupContent(photo) {
        const container = document.createElement('div');
        container.className = 'photo-popup';

        const title = document.createElement('div');
        title.className = 'fw-semibold mb-1';
        title.textContent = photo?.title || 'Photo';
        container.appendChild(title);

        const thumbPath = photo?.thumbnailPath || photo?.filePath;
        if (thumbPath) {
            const img = document.createElement('img');
            img.alt = photo?.title || '';
            img.loading = 'lazy';
            img.src = `/uploads/${thumbPath}`;
            container.appendChild(img);
        }

        const date = (photo?.dateTakenDisplay || '').trim();
        if (date) {
            const dateEl = document.createElement('div');
            dateEl.className = 'text-muted small mt-1';
            dateEl.textContent = date;
            container.appendChild(dateEl);
        }

        return container;
    }

    for (const p of withCoords) {
        const lat = p.latitude;
        const lng = p.longitude;
        const tooltip = p.title || 'Photo';

        L.marker([lat, lng])
            .addTo(markerLayer)
            .bindPopup(buildPopupContent(p))
            .bindTooltip(tooltip, { permanent: false, direction: 'top' });
    }

    const noLocContainer = document.getElementById('noLocPhotoSummary');
    if (noLocContainer) {
        if (withoutCoords.length > 0) {
            const count = withoutCoords.length;
            noLocContainer.innerHTML = `
                <p class="text-muted small mb-1"><i class="bi bi-info-circle"></i>
                ${count} photo(s) have no location and are not shown on the map.</p>`;
        } else {
            noLocContainer.innerHTML = '';
        }
    }

    if (withCoords.length > 0) {
        const bounds = withCoords.map(p => [p.latitude, p.longitude]);
        map.fitBounds(bounds, { padding: [40, 40] });
    }
})();
