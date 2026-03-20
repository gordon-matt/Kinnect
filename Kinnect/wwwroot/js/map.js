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
        if (pins.length === 0 || !googleMapsApiKey) return;

        const center = pins.length > 0
            ? { lat: pins[0].latitude, lng: pins[0].longitude }
            : { lat: 0, lng: 0 };

        const map = new google.maps.Map(document.getElementById('familyMap'), {
            zoom: 4,
            center: center,
            styles: [{ featureType: 'poi', stylers: [{ visibility: 'off' }] }]
        });

        const bounds = new google.maps.LatLngBounds();

        pins.forEach(pin => {
            const position = { lat: pin.latitude, lng: pin.longitude };
            bounds.extend(position);

            const marker = new google.maps.Marker({
                position: position,
                map: map,
                title: pin.fullName
            });

            const infoContent = `
                <div style="text-align:center;padding:4px;">
                    ${pin.profileImagePath
                        ? `<img src="/uploads/${pin.profileImagePath}" style="width:60px;height:60px;border-radius:50%;object-fit:cover;" /><br/>`
                        : ''}
                    <strong>${pin.fullName}</strong><br/>
                    <a href="/Profile/View/${pin.personId}">View Profile</a>
                </div>`;

            const infoWindow = new google.maps.InfoWindow({ content: infoContent });
            marker.addListener('click', () => infoWindow.open(map, marker));
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

    if (typeof google !== 'undefined') {
        mapViewModel.initMap();
    }
});

function initMap() {
    if (mapViewModel && !mapViewModel.loading()) {
        mapViewModel.initMap();
    }
}
