/**
 * Small shared utilities used across pages.
 */

export function formatChatTimestamp(ts) {
    const d = new Date(ts);
    const now = new Date();
    const diffDays = Math.floor((now - d) / 86400000);
    const hh = d.getHours().toString().padStart(2, '0');
    const mm = d.getMinutes().toString().padStart(2, '0');
    if (diffDays === 0) return `${hh}:${mm}`;
    if (diffDays === 1) return `Yesterday ${hh}:${mm}`;
    return `${d.getDate()}/${d.getMonth() + 1}/${d.getFullYear()} ${hh}:${mm}`;
}

export function scrollElementToBottom(elementId) {
    const el = document.getElementById(elementId);
    if (el) el.scrollTop = el.scrollHeight;
}
