/**
 * LogForDev - Utility Functions
 */

// --- DOM Helpers ---
function escapeHtml(text) {
    if (!text) return '';
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

function truncate(str, len) {
    if (!str) return '';
    return str.length > len ? str.substring(0, len) + '...' : str;
}

// --- Date Formatting ---
function formatDate(dateStr) {
    const d = new Date(dateStr);
    return d.toLocaleString(undefined, {
        year: 'numeric',
        month: '2-digit',
        day: '2-digit',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit'
    });
}

function relativeTime(dateStr) {
    const now = new Date();
    const d = new Date(dateStr);
    const diff = Math.floor((now - d) / 1000);
    if (diff < 60) return `${diff}s ago`;
    if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
    if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
    return `${Math.floor(diff / 86400)}d ago`;
}

function toLocalISOString(date) {
    const pad = n => String(n).padStart(2, '0');
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

// --- Debounce ---
function debounce(func, wait) {
    let timeout;
    return function (...args) {
        clearTimeout(timeout);
        timeout = setTimeout(() => func(...args), wait);
    };
}

// --- Level Badges ---
const levelNames = ['Trace', 'Debug', 'Info', 'Warning', 'Error', 'Fatal'];

function levelBadge(level) {
    const levelStr = typeof level === 'number' ? (levelNames[level] || 'Unknown') : level;
    const map = {
        trace: 'bg-zinc-700/50 text-zinc-400',
        debug: 'bg-blue-500/20 text-blue-400',
        info: 'bg-green-500/20 text-green-400',
        warning: 'bg-yellow-500/20 text-yellow-400',
        error: 'bg-red-500/20 text-red-400',
        fatal: 'bg-red-600 text-white',
    };
    const cls = map[levelStr.toLowerCase()] || 'bg-zinc-700/50 text-zinc-400';
    return `<span class="inline-flex items-center rounded-md px-2 py-0.5 text-xs font-semibold uppercase ${cls}">${levelStr}</span>`;
}

function envBadge(env) {
    if (!env) return '<span class="text-muted-foreground text-xs">-</span>';
    const map = {
        production: 'bg-red-500/10 text-red-400 border-red-500/20',
        staging: 'bg-yellow-500/10 text-yellow-400 border-yellow-500/20',
        development: 'bg-green-500/10 text-green-400 border-green-500/20',
        dev: 'bg-green-500/10 text-green-400 border-green-500/20',
    };
    const cls = map[env.toLowerCase()] || 'bg-zinc-700/30 text-zinc-400 border-zinc-600/30';
    return `<span class="inline-flex items-center rounded px-1.5 py-0.5 text-[10px] font-medium border ${cls}">${escapeHtml(env)}</span>`;
}

// --- Metadata Formatting ---
function formatMetadata(metadata) {
    try {
        const parsed = typeof metadata === 'string' ? JSON.parse(metadata) : metadata;
        return escapeHtml(JSON.stringify(parsed, null, 2));
    } catch {
        return escapeHtml(metadata);
    }
}

// --- Toast Notifications ---
function showToast(message, type = 'info') {
    const container = document.getElementById('toastContainer');
    if (!container) return;

    const colors = {
        info: 'border-primary/50 bg-primary/10 text-blue-300',
        error: 'border-red-500/50 bg-red-500/10 text-red-300',
        success: 'border-green-500/50 bg-green-500/10 text-green-300'
    };

    const toast = document.createElement('div');
    toast.className = `rounded-lg border px-4 py-3 text-sm shadow-lg ${colors[type] || colors.info} transition-all duration-300 transform translate-x-0`;
    toast.textContent = message;
    container.appendChild(toast);

    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(100%)';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

// --- Clipboard ---
function copyToClipboard(text) {
    navigator.clipboard.writeText(text).then(() => showToast('Copied to clipboard', 'success'));
}

// Export for module usage (if needed)
if (typeof module !== 'undefined' && module.exports) {
    module.exports = {
        escapeHtml,
        truncate,
        formatDate,
        relativeTime,
        toLocalISOString,
        debounce,
        levelBadge,
        envBadge,
        formatMetadata,
        showToast,
        copyToClipboard,
        levelNames
    };
}
