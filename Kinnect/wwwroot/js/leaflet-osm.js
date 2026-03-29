/**
 * Default OpenStreetMap raster tiles for Leaflet.
 * Requires global `L` (Leaflet) to be loaded before this module runs.
 *
 * @param {L.Map} map
 * @param {{ maxZoom?: number }} [options]
 * @returns {L.TileLayer}
 */
export function addOpenStreetMapTiles(map, options = {}) {
    const { maxZoom = 19 } = options;
    return L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '&copy; OpenStreetMap contributors',
        maxZoom
    }).addTo(map);
}
