class MapViewModel {
    constructor() {
        this.pins = ko.observableArray([]);
        this.loading = ko.observable(true);
    }

    loadPins = async () => {
        try {
            const response = await fetch('/api/people/map-pins');
            const result = await response.json();
            this.pins(result.value || result || []);
        } catch (err) {
            console.error('Error loading map pins:', err);
        } finally {
            this.loading(false);
        }
    };

    initMap = () => {
        const pins = this.pins();
        if (pins.length === 0 || typeof L === 'undefined') return;

        const center = [pins[0].latitude, pins[0].longitude];
        const map = L.map('familyMap').setView(center, 4);

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors',
            maxZoom: 19
        }).addTo(map);

        const bounds = L.latLngBounds([]);

        pins.forEach(pin => {
            const position = [pin.latitude, pin.longitude];
            bounds.extend(position);

            const infoContent = `
                <div style="text-align:center;padding:4px;">
                    ${pin.profileImagePath
                        ? `<img src="/uploads/${pin.profileImagePath}" style="width:60px;height:60px;border-radius:50%;object-fit:cover;" /><br/>`
                        : ''}
                    <strong>${pin.fullName}</strong><br/>
                    <a href="/Profile/View/${pin.personId}">View Profile</a>
                </div>`;

            L.marker(position)
                .addTo(map)
                .bindPopup(infoContent);
        });

        if (pins.length > 1) {
            map.fitBounds(bounds);
        }
    };
}

let mapViewModel;

document.addEventListener('DOMContentLoaded', async () => {
    mapViewModel = new MapViewModel();
    ko.applyBindings(mapViewModel);
    await mapViewModel.loadPins();
    mapViewModel.initMap();
});
