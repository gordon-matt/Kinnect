function findMap(container) {
    const zoom = container.querySelector(".leaflet-control-zoom-in, .leaflet-control-zoom-out");
    if (!zoom) {
        return null;
    }

    const original = Function.prototype.call;
    let map = null;
    Function.prototype.call = function (context) {
        if (!map && context && context._map && typeof context._map.eachLayer === "function") {
            map = context._map;
            throw new Error("kinnect-map");
        }

        return original.apply(this, arguments);
    };

    try {
        zoom.dispatchEvent(new MouseEvent("click", { bubbles: true, cancelable: true }));
    } catch (error) {
        if (!error || error.message !== "kinnect-map") {
            throw error;
        }
    } finally {
        Function.prototype.call = original;
    }

    return map;
}

export function usePins(root) {
    if (typeof L === "undefined" || !root) {
        return;
    }

    const container = root.querySelector(".leaflet-container");
    const map = container && findMap(container);
    if (!map) {
        return;
    }

    const icon = L.icon({
        iconUrl: "/img/leaflet/marker-icon.png",
        iconRetinaUrl: "/img/leaflet/marker-icon-2x.png",
        shadowUrl: "/img/leaflet/marker-shadow.png",
        iconSize: [25, 41],
        iconAnchor: [12, 41],
        popupAnchor: [1, -34],
        shadowSize: [41, 41],
        shadowAnchor: [12, 41],
    });

    const circles = [];
    map.eachLayer(layer => {
        if (layer instanceof L.CircleMarker && !(layer instanceof L.Circle)) {
            circles.push(layer);
        }
    });

    for (const circle of circles) {
        const popup = circle.getPopup();
        const marker = L.marker(circle.getLatLng(), { icon });
        if (popup) {
            marker.bindPopup(popup.getContent());
        }

        marker.addTo(map);
        map.removeLayer(circle);
    }
}
