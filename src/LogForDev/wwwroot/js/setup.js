let currentStep = 1;

// ─── Toast System ───────────────────────────────────────
function showToast(message, type = 'info', duration = 3500) {
    const container = document.getElementById('toastContainer');
    const cfg = {
        success: { border: 'rgba(34,197,94,0.25)',  bar: '#22c55e', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="#22c55e" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="width:16px;height:16px;flex-shrink:0"><polyline points="20 6 9 17 4 12"/></svg>' },
        error:   { border: 'rgba(239,68,68,0.25)',  bar: '#ef4444', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="#ef4444" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="width:16px;height:16px;flex-shrink:0"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>' },
        warning: { border: 'rgba(234,179,8,0.25)',  bar: '#eab308', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="#eab308" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="width:16px;height:16px;flex-shrink:0"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>' },
        info:    { border: 'rgba(59,130,246,0.25)', bar: '#3b82f6', icon: '<svg viewBox="0 0 24 24" fill="none" stroke="#3b82f6" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="width:16px;height:16px;flex-shrink:0"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>' },
    };
    const c = cfg[type] || cfg.info;
    const el = document.createElement('div');
    el.className = 'toast-enter';
    el.style.cssText = `pointer-events:auto;display:flex;align-items:center;gap:10px;padding:12px 14px;background:#18181b;border:1px solid ${c.border};border-radius:10px;box-shadow:0 8px 32px rgba(0,0,0,0.55),0 2px 8px rgba(0,0,0,0.3);position:relative;overflow:hidden;`;
    el.innerHTML = `
        ${c.icon}
        <span style="font-size:13px;font-weight:500;color:#fafafa;line-height:1.45;flex:1;">${message}</span>
        <button onclick="this.parentElement.dispatchEvent(new Event('dismiss'))" style="color:#71717a;background:none;border:none;cursor:pointer;padding:2px;line-height:0;flex-shrink:0;" onmouseover="this.style.color='#fafafa'" onmouseout="this.style.color='#71717a'">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="width:13px;height:13px;"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
        </button>
        <div class="tp" style="position:absolute;bottom:0;left:0;height:2px;background:${c.bar};width:100%;opacity:0.5;"></div>
    `;
    function dismiss() { clearTimeout(timer); el.classList.replace('toast-enter', 'toast-exit'); setTimeout(() => el.remove(), 260); }
    el.addEventListener('dismiss', dismiss);
    el.querySelector('.tp').animate([{ width: '100%' }, { width: '0%' }], { duration, easing: 'linear' });
    container.appendChild(el);
    let timer = setTimeout(dismiss, duration);
    el.addEventListener('mouseenter', () => clearTimeout(timer));
    el.addEventListener('mouseleave', () => { timer = setTimeout(dismiss, 800); });
}

// ─── Field Validation ───────────────────────────────────
function showFieldError(fieldId, message) {
    const field = document.getElementById(fieldId);
    if (!field) return;
    clearFieldError(fieldId);
    field.classList.add('!border-destructive');
    field.classList.remove('border-border');
    const err = document.createElement('p');
    err.className = 'field-error text-xs text-destructive mt-1';
    err.textContent = message;
    field.closest('div').appendChild(err);
}

function clearFieldError(fieldId) {
    const field = document.getElementById(fieldId);
    if (!field) return;
    field.classList.remove('!border-destructive');
    field.classList.add('border-border');
    const wrapper = field.closest('div');
    const existing = wrapper.querySelector('.field-error');
    if (existing) existing.remove();
}

// ─── Password Toggle ────────────────────────────────────
function togglePasswordField(fieldId, btn) {
    const input = document.getElementById(fieldId);
    const eyeOpen = btn.querySelector('.eye-open');
    const eyeClosed = btn.querySelector('.eye-closed');
    if (input.type === 'password') {
        input.type = 'text';
        eyeOpen.classList.add('hidden');
        eyeClosed.classList.remove('hidden');
    } else {
        input.type = 'password';
        eyeOpen.classList.remove('hidden');
        eyeClosed.classList.add('hidden');
    }
}

// ─── Retention ──────────────────────────────────────────
function toggleRetentionInput() {
    const mode = document.getElementById('retentionMode').value;
    const wrap = document.getElementById('customRetentionWrap');
    wrap.classList.toggle('hidden', mode !== 'custom');
}

function getRetentionDays() {
    const mode = document.getElementById('retentionMode').value;
    if (mode === '0') return 0;
    if (mode === 'custom') return parseInt(document.getElementById('retentionDaysCustom').value) || 30;
    return parseInt(mode);
}

// ─── Step Navigation ────────────────────────────────────
function goToStep(step) {
    const direction = step >= currentStep ? 'forward' : 'back';
    currentStep = step;

    document.querySelectorAll('.step-panel').forEach(p => {
        p.classList.remove('active', 'slide-forward', 'slide-back');
    });
    const activePanel = document.getElementById('step' + step);
    activePanel.classList.add('active', direction === 'forward' ? 'slide-forward' : 'slide-back');

    document.getElementById('stepCounter').textContent = 'Ad\u0131m ' + step + ' / 6';
    updateStepper(step);

    if (step === 5) updateCurlExample();
}

function updateStepper(activeStep) {
    const checkSvg = '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>';

    for (let i = 1; i <= 6; i++) {
        const btn = document.getElementById('stepBtn' + i);
        const label = btn.parentElement.querySelector('span');
        const line = document.getElementById('line' + i);

        if (i < activeStep) {
            btn.className = 'stepper-circle w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold transition-all duration-300 bg-primary text-white stepper-complete-anim';
            btn.innerHTML = checkSvg;
            label.className = 'text-[10px] sm:text-xs text-primary font-medium mt-2 transition-colors';
            if (line) line.className = 'w-8 sm:w-12 h-0.5 mt-[18px] bg-primary transition-colors duration-300';
        } else if (i === activeStep) {
            btn.className = 'stepper-circle w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold transition-all duration-300 bg-primary text-white stepper-pulse ring-2 ring-primary/30 ring-offset-2 ring-offset-background';
            btn.innerHTML = String(i);
            label.className = 'text-[10px] sm:text-xs text-primary font-medium mt-2 transition-colors';
            if (line) line.className = 'w-8 sm:w-12 h-0.5 mt-[18px] bg-border transition-colors duration-300';
        } else {
            btn.className = 'stepper-circle w-9 h-9 rounded-full flex items-center justify-center text-xs font-bold transition-all duration-300 bg-secondary text-muted-foreground border border-border';
            btn.innerHTML = String(i);
            label.className = 'text-[10px] sm:text-xs text-muted-foreground font-medium mt-2 transition-colors';
            if (line) line.className = 'w-8 sm:w-12 h-0.5 mt-[18px] bg-border transition-colors duration-300';
        }
    }
}

// ─── Init ───────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {
    generateKey();

    ['adminEmail', 'adminPassword', 'projectName'].forEach(id => {
        document.getElementById(id)?.addEventListener('input', () => clearFieldError(id));
    });
});

// ─── curl Example ───────────────────────────────────────
function updateCurlExample() {
    const host = window.__setupBaseUrl || '';
    const key = document.getElementById('apiKey').value;
    const curl = `curl -X POST ${host}/api/logs \\\n  -H "Content-Type: application/json" \\\n  -H "X-API-Key: ${key}" \\\n  -d '{\n    "level": "info",\n    "message": "Hello from my app!",\n    "appName": "my-app",\n    "environment": "production"\n  }'`;
    document.getElementById('curlExample').textContent = curl;
}

// ─── Copy Helpers ───────────────────────────────────────
function copyCurl() {
    navigator.clipboard.writeText(document.getElementById('curlExample').textContent);
    showToast('curl komutu kopyaland\u0131', 'success');
}

function copyApiKey() {
    const text = document.getElementById('apiKey').value;
    if (text) {
        navigator.clipboard.writeText(text);
        showToast('API key kopyaland\u0131', 'success');
    }
}

function copyManualCode() {
    navigator.clipboard.writeText(document.getElementById('manualCode').value);
    showToast('Manuel kod kopyaland\u0131', 'success');
}

// ─── API: Test Connection ───────────────────────────────
async function testConnection() {
    const btn = document.getElementById('btnTestConn');
    const result = document.getElementById('connResult');
    btn.disabled = true;
    result.innerHTML = '<span class="spinner"></span>';

    try {
        const resp = await fetch('/api/setup/test-connection', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                host: document.getElementById('chHost').value,
                port: parseInt(document.getElementById('chPort').value),
                database: document.getElementById('chDatabase').value,
                username: document.getElementById('chUsername').value,
                password: document.getElementById('chPassword').value
            })
        });
        const data = await resp.json();
        if (data.success) {
            result.innerHTML = '<div class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium" style="background:rgba(34,197,94,0.08);border:1px solid rgba(34,197,94,0.2);color:#4ade80"><svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>' + data.message + '</div>';
        } else {
            result.innerHTML = '<div class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium" style="background:rgba(239,68,68,0.08);border:1px solid rgba(239,68,68,0.2);color:#f87171"><svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>' + data.message + '</div>';
        }
    } catch (e) {
        result.innerHTML = '<div class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium" style="background:rgba(239,68,68,0.08);border:1px solid rgba(239,68,68,0.2);color:#f87171"><svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>Ba\u011flant\u0131 hatas\u0131</div>';
    }
    btn.disabled = false;
}

// ─── API: Generate Key ──────────────────────────────────
async function generateKey() {
    try {
        const resp = await fetch('/api/setup/generate-key', { method: 'POST' });
        const data = await resp.json();
        document.getElementById('apiKey').value = data.key;
    } catch (e) {
        showToast('Anahtar olu\u015fturulamad\u0131', 'error');
    }
}

// ─── API: Send Test Log ─────────────────────────────────
async function sendTestLog() {
    const btn = document.getElementById('btnTestLog');
    const result = document.getElementById('testLogResult');
    btn.disabled = true;
    result.innerHTML = '<span class="spinner"></span>';

    try {
        const resp = await fetch('/api/setup/send-test-log', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ apiKey: document.getElementById('apiKey').value })
        });
        const data = await resp.json();
        if (data.success) {
            result.innerHTML = '<div class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium" style="background:rgba(34,197,94,0.08);border:1px solid rgba(34,197,94,0.2);color:#4ade80"><svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg>' + data.message + '</div>';
        } else {
            result.innerHTML = '<div class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium" style="background:rgba(239,68,68,0.08);border:1px solid rgba(239,68,68,0.2);color:#f87171"><svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>' + data.message + '</div>';
        }
    } catch (e) {
        result.innerHTML = '<div class="inline-flex items-center gap-2 px-3 py-1.5 rounded-lg text-xs font-medium" style="background:rgba(239,68,68,0.08);border:1px solid rgba(239,68,68,0.2);color:#f87171"><svg class="w-3.5 h-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/></svg>Log g\u00f6nderilemedi</div>';
    }
    btn.disabled = false;
}

// ─── API: Finalize Setup ────────────────────────────────
async function finalizeSetup() {
    const btn = document.getElementById('btnFinalize');
    const adminEmail = document.getElementById('adminEmail').value.trim();
    const adminPassword = document.getElementById('adminPassword').value;
    const projectName = document.getElementById('projectName').value.trim();

    if (!projectName) {
        showFieldError('projectName', 'Proje ad\u0131 bo\u015f olamaz');
        document.getElementById('projectName').focus();
        return;
    }

    if (!adminEmail || !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(adminEmail)) {
        showFieldError('adminEmail', 'Ge\u00e7erli bir e-posta adresi giriniz');
        goToStep(2);
        setTimeout(() => document.getElementById('adminEmail').focus(), 100);
        return;
    }

    if (!adminPassword || adminPassword.trim().length === 0) {
        showFieldError('adminPassword', '\u015eifre bo\u015f olamaz');
        goToStep(2);
        setTimeout(() => document.getElementById('adminPassword').focus(), 100);
        return;
    }

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span> Kaydediliyor...';

    try {
        const resp = await fetch('/api/setup/complete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                clickHouseHost: document.getElementById('chHost').value,
                clickHousePort: parseInt(document.getElementById('chPort').value),
                clickHouseDatabase: document.getElementById('chDatabase').value,
                clickHouseUsername: document.getElementById('chUsername').value,
                clickHousePassword: document.getElementById('chPassword').value,
                projectName: document.getElementById('projectName').value.trim(),
                apiKey: document.getElementById('apiKey').value,
                keyExpiryDays: parseInt(document.getElementById('keyExpiryDays').value) || 0,
                retentionDays: getRetentionDays(),
                adminEmail: adminEmail,
                adminPassword: adminPassword
            })
        });
        const data = await resp.json();
        if (data.success && data.qrCodeDataUri) {
            document.getElementById('qrCodeImage').src = data.qrCodeDataUri;
            document.getElementById('manualCode').value = data.totpSecret;
            goToStep(4);
            showToast('Kurulum ba\u015far\u0131yla tamamland\u0131', 'success');
        } else {
            showToast(data.message || 'Kurulum tamamlanamad\u0131', 'error');
            btn.disabled = false;
            btn.innerHTML = '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg> Kurulumu Ba\u015flat';
        }
    } catch (e) {
        showToast('Kurulum tamamlanamad\u0131: ' + e.message, 'error');
        btn.disabled = false;
        btn.innerHTML = '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"/><polyline points="22 4 12 14.01 9 11.01"/></svg> Kurulumu Ba\u015flat';
    }
}

function redirectToLogin() {
    window.location.href = '/login';
}
