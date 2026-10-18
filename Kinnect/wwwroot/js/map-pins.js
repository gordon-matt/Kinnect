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

    if (map) {
        map.setZoom(map.getZoom() - 1);
    }

    return map;
}

export function usePins(root, pins, route, pickRef) {
    if (typeof L === "undefined" || !root) {
        return;
    }

    const container = root.querySelector(".leaflet-container");
    const map = container && findMap(container);
    if (!map) {
        return;
    }

    map._kinnectPickRef = pickRef || null;
    if (pickRef && !map._kinnectPickBound) {
        map._kinnectPickBound = true;
        map.on("click", (event) => {
            if (map._kinnectPickRef) {
                map._kinnectPickRef.invokeMethodAsync("OnMapPicked", event.latlng.lat, event.latlng.lng);
            }
        });
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

    const previous = [];
    map.eachLayer(layer => {
        if (layer.options && layer.options.kinnectPin) {
            previous.push(layer);
        } else if (layer instanceof L.CircleMarker && !(layer instanceof L.Circle)) {
            previous.push(layer);
        }
    });

    for (const layer of previous) {
        map.removeLayer(layer);
    }

    if (map._kinnectRoute) {
        map.removeLayer(map._kinnectRoute);
        map._kinnectRoute = null;
    }

    const latlngs = [];
    for (const pin of pins || []) {
        const marker = L.marker([pin.lat, pin.lng], { icon, kinnectPin: true, draggable: !!pickRef });
        if (pin.html) {
            marker.bindPopup(pin.html);
        }

        if (pickRef) {
            marker.on("dragend", (event) => {
                const point = event.target.getLatLng();
                pickRef.invokeMethodAsync("OnMapPicked", point.lat, point.lng);
            });
        }

        marker.addTo(map);
        latlngs.push([pin.lat, pin.lng]);
    }

    if (Array.isArray(route) && route.length >= 2) {
        const color = getComputedStyle(document.documentElement).getPropertyValue("--nf-accent").trim() || "#0f766e";
        map._kinnectRoute = L.polyline(route, { color, weight: 2, opacity: 0.7, kinnectPin: true }).addTo(map);
    }

    if (latlngs.length === 1) {
        map.setView(latlngs[0], 12);
    } else if (latlngs.length > 1) {
        map.fitBounds(latlngs, { padding: [48, 48] });
    }

    setTimeout(() => map.invalidateSize(), 200);
}
