/**
 * Shared Knockout helpers and constants for profile edit (`profile.js`) and view (`view-profile.js`).
 */

export const TAGGED_FOLDER_KEY = '__tagged__';

export const MONTHS = [
    { v: null, l: '— Month —' },
    { v: 1, l: 'January' },
    { v: 2, l: 'February' },
    { v: 3, l: 'March' },
    { v: 4, l: 'April' },
    { v: 5, l: 'May' },
    { v: 6, l: 'June' },
    { v: 7, l: 'July' },
    { v: 8, l: 'August' },
    { v: 9, l: 'September' },
    { v: 10, l: 'October' },
    { v: 11, l: 'November' },
    { v: 12, l: 'December' }
];

export function daysInMonth(monthObs, yearObs) {
    return ko.computed(() => {
        const m = parseInt(ko.unwrap(monthObs)) || 0;
        const y = parseInt(ko.unwrap(yearObs)) || 2000;
        const count = m ? new Date(y, m, 0).getDate() : 31;
        return [null, ...Array.from({ length: count }, (_, i) => i + 1)];
    });
}

export const COPYABLE_TYPES = new Set(['EMIG', 'IMMI', 'RESI']);
export const SINGLE_INSTANCE_EVENT_TYPES = new Set(['BIRT', 'DEAT', 'CHR', 'BURI', 'CREM']);
export const TIMELINE_EVENT_TYPE_ORDER = [
    'BIRT',
    'DEAT',
    'BAPM',
    'CHR',
    'CONF',
    'ADOP',
    'EMIG',
    'IMMI',
    'NATU',
    'MARB',
    'MARL',
    'BURI',
    'CREM',
    'RESI',
    'EVEN'
];

/** Matches server PersonEventDto.DateDisplay logic for spouse-synthetic rows */
export function partialDateDisplay(year, month, day) {
    if (year == null) return null;
    if (month != null && day != null)
        return `${year}-${String(month).padStart(2, '0')}-${String(day).padStart(2, '0')}`;
    return String(year);
}
