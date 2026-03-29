/**
 * Nominatim (OpenStreetMap) search URL and address formatting for stored labels.
 */

export function nominatimSearchUrl(q) {
    return `https://nominatim.openstreetmap.org/search?format=json&addressdetails=1&q=${encodeURIComponent(q)}&limit=5`;
}

/**
 * @param {object} address - Nominatim `address` object
 * @param {{ includePostcode?: boolean }} options
 */
export function formatNominatimAddress(address, options = {}) {
    if (!address || typeof address !== 'object') return '';
    const { includePostcode = true } = options;

    const place =
        address.city ||
        address.town ||
        address.village ||
        address.hamlet ||
        address.suburb ||
        address.neighbourhood ||
        address.municipality;

    const country = address.country || '';
    const isUk =
        country === 'United Kingdom' ||
        country === 'Great Britain' ||
        country === 'England' ||
        country === 'Scotland' ||
        country === 'Wales' ||
        country === 'Northern Ireland';

    const stateUsefulCountry = ['United States', 'Canada', 'Australia'].includes(country);

    let region = null;
    if (stateUsefulCountry && address.state) {
        region = address.state;
    } else if (isUk) {
        if (address.county) region = address.county;
    } else if (address.state) {
        region = address.state;
    } else if (address.county) {
        region = address.county;
    }

    if (region && place && region.trim().toLowerCase() === place.trim().toLowerCase()) {
        region = null;
    }

    const parts = [];

    if (address.road) {
        const roadPart = address.house_number
            ? `${address.house_number} ${address.road}`.trim()
            : address.road;
        parts.push(roadPart);
    }

    if (place) parts.push(place);
    if (region) parts.push(region);

    if (includePostcode && address.postcode && address.road) {
        parts.push(address.postcode);
    }

    if (country) parts.push(country);

    return parts.join(', ');
}

/** Short label for persisted/display fields after picking a Nominatim search hit */
export function storedLabelFromNominatimPlace(place) {
    if (!place) return '';
    const addr = place.address;
    if (addr && typeof addr === 'object') {
        const formatted = formatNominatimAddress(addr);
        if (formatted) return formatted;
    }
    return place.display_name || '';
}
