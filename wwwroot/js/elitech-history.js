(() => {
    const cfg = window.__ELITECH_HISTORY__ || {};
    const isAdmin = !!cfg.isAdmin;
    const baseUrl = cfg.baseUrl || "/";

    // Elements
    const nowText = document.getElementById('nowText');
    const statusEl = document.getElementById('status');

    const deviceGuidEl = document.getElementById('deviceGuid');
    const devicePickEl = document.getElementById('devicePick');

    const fromEl = document.getElementById('from');
    const toEl = document.getElementById('to');
    const quickEl = document.getElementById('quick');

    const tempChanEl = document.getElementById('tempChan');
    const humChanEl = document.getElementById('humChan');

    const tbody = document.getElementById('tbody');
    const apiMsg = document.getElementById('apiMsg');
    const loading = document.getElementById('loading');

    const pointCount = document.getElementById('pointCount');
    const rangeText = document.getElementById('rangeText');
    const deviceText = document.getElementById('deviceText');

    const avgTemp = document.getElementById('avgTemp');
    const maxTemp = document.getElementById('maxTemp');
    const minTemp = document.getElementById('minTemp');
    const lastHum = document.getElementById('lastHum');

    const btnFetch = document.getElementById('btnFetch');
    const btnClear = document.getElementById('btnClear');
    const btnExport = document.getElementById('btnExport');

    const autoToggle = document.getElementById('autoToggle');
    const autoInterval = document.getElementById('autoInterval');
    const autoCountdownEl = document.getElementById('autoCountdown');

    const zoomTempToggle = document.getElementById('zoomTempToggle');
    const zoomRhToggle = document.getElementById('zoomRhToggle');
    const zoomTempBadge = document.getElementById('zoomTempBadge');
    const zoomRhBadge = document.getElementById('zoomRhBadge');

    const toasty = document.getElementById('toasty');

    // State
    let inflightAbort = null;
    let autoTimer = null;
    let autoTicker = null;

    // client cache chống reload spam: key -> { at, payload }
    const memCache = new Map(); // key => { at:number, data:any }
    const CACHE_TTL_MS = 15_000;

    // debounce submit/change
    let debounceTimer = null;
    const DEBOUNCE_MS = 350;

    // Chart
    let chart = null;

    // Utils
    function tickNow() {
        if (!nowText) return;
        nowText.textContent = new Date().toLocaleString('vi-VN', { hour12: false });
    }
    tickNow();
    setInterval(tickNow, 1000);

    function setStatus(msg) { if (statusEl) statusEl.textContent = msg || ''; }
    function showLoading(b) {
        if (loading) loading.style.display = b ? 'inline-flex' : 'none';
        if (btnFetch) btnFetch.disabled = !!b;
    }

    function esc(v) {
        return (v ?? '').toString().replace(/[&<>"']/g, m => ({
            '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
        }[m]));
    }

    function toLocalInputValue(d) {
        const pad = n => n.toString().padStart(2, '0');
        return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }
    function parseLocalInputValue(s) { return s ? new Date(s) : null; }

    function setDefaultRange() {
        const end = new Date();
        const start = new Date(end.getTime() - 24 * 3600 * 1000);
        if (fromEl) fromEl.value = toLocalInputValue(start);
        if (toEl) toEl.value = toLocalInputValue(end);
    }

    function skeletonRows(n) {
        return Array.from({ length: n })
            .map(() => `<tr class="skeleton-row"><td colspan="10"></td></tr>`)
            .join('');
    }

    function num(v) {
        if (v == null) return null;
        if (typeof v === 'number') return Number.isFinite(v) ? v : null;
        const s = String(v).replace("℃", "").replace("%RH", "").trim();
        const x = parseFloat(s.replace(',', '.'));
        return Number.isFinite(x) ? x : null;
    }

    function unixToMs(v) {
        if (v == null) return null;
        if (typeof v !== 'number') {
            const n = parseFloat(String(v));
            if (!Number.isFinite(n)) return null;
            v = n;
        }
        return (v > 1e12) ? v : v * 1000;
    }

    function toast(msg, type = 'ok') {
        if (!toasty) return;
        const el = document.createElement('div');
        el.className = 'elx-toast';
        if (type === 'err') el.style.borderColor = 'rgba(239,68,68,.35)';
        if (type === 'warn') el.style.borderColor = 'rgba(245,158,11,.35)';
        el.textContent = msg;
        toasty.appendChild(el);
        setTimeout(() => { el.style.opacity = '0'; el.style.transform = 'translateY(6px)'; }, 2600);
        setTimeout(() => { el.remove(); }, 3200);
    }

    function minMaxNums(arr) {
        let mn = Infinity, mx = -Infinity, ok = false;
        for (const v of arr) {
            if (typeof v === 'number' && isFinite(v)) {
                ok = true;
                if (v < mn) mn = v;
                if (v > mx) mx = v;
            }
        }
        return ok ? { mn, mx } : null;
    }
    function fmt1(n) { return (Math.round(n * 10) / 10).toFixed(1); }

    function getTempChan() {
        const v = parseInt(tempChanEl?.value || '1', 10);
        return Number.isFinite(v) ? Math.min(4, Math.max(1, v)) : 1;
    }
    function getHumChan() {
        const v = parseInt(humChanEl?.value || '1', 10);
        return Number.isFinite(v) ? Math.min(2, Math.max(1, v)) : 1;
    }
    function pickField(it, prefix, idx) {
        if (!it) return null;
        return it[`${prefix}${idx}`];
    }

    // ✅ helper: choose time unit by range (ms)
    function chooseTimeUnit(rangeMs) {
        const hour = 3600 * 1000;
        const day = 24 * hour;
        const week = 7 * day;

        if (rangeMs <= 6 * hour) return { unit: 'minute', step: 15, fmt: 'HH:mm' };
        if (rangeMs <= 2 * day) return { unit: 'hour', step: 2, fmt: 'HH:mm' };
        if (rangeMs <= week) return { unit: 'day', step: 1, fmt: 'dd/MM' };
        return { unit: 'day', step: 2, fmt: 'dd/MM' };
    }

    // Chart create once + update
    function ensureChart() {
        if (chart) return chart;

        const canvas = document.getElementById('chartTemp');
        const ctx = canvas.getContext('2d');

        const dark = document.documentElement.getAttribute("data-elitech-theme") === "dark";
        const tickColor = dark ? 'rgba(255,255,255,.65)' : 'rgba(15,23,42,.65)';
        const gridColor = dark ? 'rgba(255,255,255,.08)' : 'rgba(2,6,23,.08)';
        const legendColor = dark ? 'rgba(255,255,255,.85)' : 'rgba(15,23,42,.85)';

        chart = new Chart(ctx, {
            type: 'line',
            data: {
                datasets: [
                    {
                        label: 'tmp1 (°C)',
                        data: [],
                        tension: .35,
                        pointRadius: 0,
                        borderWidth: 2,
                        borderColor: dark ? 'rgba(79,70,229,1)' : 'rgba(79,70,229,1)',
                        fill: false,
                        yAxisID: 'yTemp',
                        parsing: false
                    },
                    {
                        label: 'hum1 (%RH)',
                        data: [],
                        tension: .35,
                        pointRadius: 0,
                        borderWidth: 2,
                        borderColor: dark ? 'rgba(6,182,212,1)' : 'rgba(20,184,166,1)',
                        borderDash: [6, 4],
                        fill: false,
                        yAxisID: 'yHum',
                        parsing: false
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                plugins: {
                    legend: { labels: { color: legendColor } },
                    tooltip: {
                        enabled: true,
                        callbacks: {
                            title: (items) => {
                                // show time from x
                                if (!items?.length) return '';
                                const ms = items[0].parsed?.x;
                                if (!ms) return '';
                                return new Date(ms).toLocaleString('vi-VN', { hour12: false });
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        type: 'time',
                        time: {
                            tooltipFormat: 'MM/dd/yyyy HH:mm',
                            displayFormats: {
                                minute: 'HH:mm',
                                hour: 'HH:mm',
                                day: 'dd/MM',
                                month: 'MM/yyyy'
                            }
                        },
                        ticks: {
                            color: tickColor,
                            maxRotation: 0,
                            minRotation: 0,
                            autoSkip: true,
                            maxTicksLimit: 8,
                            callback: function (value) {
                                const ms = this.getLabelForValue(value); // time scale: value -> ms
                                const d = new Date(ms);

                                const hh = String(d.getHours()).padStart(2, '0');
                                const mm = String(d.getMinutes()).padStart(2, '0');
                                const time = `${hh}:${mm}`;

                                const day = String(d.getDate()).padStart(2, '0');
                                const mon = String(d.getMonth() + 1).padStart(2, '0');
                                const date = `${day}/${mon}`;

                                // ✅ chỉ in date khi tick là mốc đầu ngày (00:xx) hoặc tick đầu tiên
                                // nếu bạn muốn “rõ hơn nữa”, in date mỗi 6h cũng được
                                if (d.getHours() === 0 && d.getMinutes() === 0) return [time, date];

                                // ✅ hoặc: in date cho tick đầu tiên để biết đang ở ngày nào
                                // Chart.js không cho biết tick index ở đây dễ dàng, nên ta làm "mốc 00:00" là đủ rõ.
                                return time;
                            }
                        },
                        grid: { display: false }
                    },
                    yTemp: { position: 'left', grid: { color: gridColor }, ticks: { color: tickColor } },
                    yHum: { position: 'right', grid: { drawOnChartArea: false }, ticks: { color: tickColor } }
                }
            }
        });

        return chart;
    }

    function applyZoom(tempYs, humYs) {
        if (!chart) return;
        if (!zoomTempToggle || !zoomRhToggle) return;

        if (zoomTempToggle.checked) {
            const mmT = minMaxNums(tempYs);
            if (mmT) {
                const pad = 0.5;
                let ymin = mmT.mn - pad, ymax = mmT.mx + pad;
                if (ymax - ymin < 2) {
                    const mid = (ymin + ymax) / 2;
                    ymin = mid - 1; ymax = mid + 1;
                }
                const yMinFinal = Math.floor(ymin * 10) / 10;
                const yMaxFinal = Math.ceil(ymax * 10) / 10;
                chart.options.scales.yTemp.min = yMinFinal;
                chart.options.scales.yTemp.max = yMaxFinal;
                if (zoomTempBadge) zoomTempBadge.textContent = `°C ${fmt1(yMinFinal)}–${fmt1(yMaxFinal)}`;
            }
        } else {
            chart.options.scales.yTemp.min = undefined;
            chart.options.scales.yTemp.max = undefined;
            if (zoomTempBadge) zoomTempBadge.textContent = '°C Auto';
        }

        if (zoomRhToggle.checked) {
            const mmH = minMaxNums(humYs);
            if (mmH) {
                const pad = 2;
                const yMinFinal = Math.max(0, Math.floor((mmH.mn - pad)));
                const yMaxFinal = Math.min(100, Math.ceil((mmH.mx + pad)));
                chart.options.scales.yHum.min = yMinFinal;
                chart.options.scales.yHum.max = yMaxFinal;
                if (zoomRhBadge) zoomRhBadge.textContent = `%RH ${yMinFinal}–${yMaxFinal}`;
            }
        } else {
            chart.options.scales.yHum.min = undefined;
            chart.options.scales.yHum.max = undefined;
            if (zoomRhBadge) zoomRhBadge.textContent = '%RH Auto';
        }
    }

    function updateChart(tempPts, humPts, rangeMs) {
        const c = ensureChart();

        const tc = getTempChan();
        const hc = getHumChan();
        c.data.datasets[0].label = `tmp${tc} (°C)`;
        c.data.datasets[1].label = `hum${hc} (%RH)`;

        // ✅ time unit dynamic theo range
        const pick = chooseTimeUnit(rangeMs);
        c.options.scales.x.time.unit = pick.unit;
        c.options.scales.x.time.stepSize = pick.step;
        c.options.scales.x.time.displayFormats[pick.unit] = pick.fmt;

        c.data.datasets[0].data = tempPts;
        c.data.datasets[1].data = humPts;

        applyZoom(tempPts.map(p => p.y), humPts.map(p => p.y));
        c.update('none');
    }

    function updateStats(tempYs, humYs, count, guid, from, to) {
        if (deviceText) deviceText.textContent = guid || '—';
        if (pointCount) pointCount.textContent = (count || 0) + ' điểm';
        if (rangeText) rangeText.textContent = `${from.toLocaleString('vi-VN', { hour12: false })} → ${to.toLocaleString('vi-VN', { hour12: false })}`;

        const t = tempYs.filter(v => typeof v === 'number' && Number.isFinite(v));
        if (t.length) {
            const avg = t.reduce((a, b) => a + b, 0) / t.length;
            if (avgTemp) avgTemp.textContent = avg.toFixed(2) + ' °C';
            if (maxTemp) maxTemp.textContent = Math.max(...t).toFixed(2) + ' °C';
            if (minTemp) minTemp.textContent = Math.min(...t).toFixed(2) + ' °C';
        } else {
            if (avgTemp) avgTemp.textContent = '—';
            if (maxTemp) maxTemp.textContent = '—';
            if (minTemp) minTemp.textContent = '—';
        }

        const lastH = [...humYs].reverse().find(v => typeof v === 'number' && Number.isFinite(v));
        if (lastHum) lastHum.textContent = (lastH != null) ? lastH.toFixed(1) + ' %RH' : '—';
    }

    function buildUrl() {
        const guid = (deviceGuidEl?.value || '').trim();
        const f = parseLocalInputValue(fromEl?.value || '');
        const t = parseLocalInputValue(toEl?.value || '');
        const qh = parseInt(quickEl?.value || '24', 10);
        const quickHours = Number.isFinite(qh) ? qh : 24;

        let url = baseUrl + 'api/elitech/history?deviceGuid=' + encodeURIComponent(guid);
        if (f && t) url += `&from=${encodeURIComponent(f.toISOString())}&to=${encodeURIComponent(t.toISOString())}`;
        else url += `&lastHours=${quickHours}`;

        return { url, guid, f, t, quickHours };
    }

    function getCache(key) {
        const hit = memCache.get(key);
        if (!hit) return null;
        if (Date.now() - hit.at > CACHE_TTL_MS) {
            memCache.delete(key);
            return null;
        }
        return hit.data;
    }
    function setCache(key, data) {
        memCache.set(key, { at: Date.now(), data });
    }

    async function loadDeviceList() {
        try {
            const ep = isAdmin ? 'api/elitech/all-devices' : 'api/elitech/my-devices';
            const res = await fetch(baseUrl + ep, { credentials: 'include' });
            if (!res.ok) throw new Error('HTTP ' + res.status);

            const j = await res.json();
            const items = Array.isArray(j.data) ? j.data : [];

            devicePickEl.innerHTML = '';
            devicePickEl.appendChild(new Option(`— ${isAdmin ? 'All devices' : 'My devices'} (${items.length}) —`, ''));

            for (const it of items) {
                const label = (it.deviceName ? `${it.deviceName} — ` : '') + it.deviceGuid;
                devicePickEl.appendChild(new Option(label, it.deviceGuid));
            }

            const qGuid = (new URLSearchParams(location.search).get('deviceGuid') || '').trim();
            const pick = qGuid || (items[0]?.deviceGuid || '');

            if (pick) {
                deviceGuidEl.value = pick;
                devicePickEl.value = pick;
            }

            if (!isAdmin) deviceGuidEl.readOnly = true;
        } catch (err) {
            console.error(err);
            setStatus('❌ Lỗi load danh sách thiết bị');
            toast('Không load được danh sách thiết bị', 'err');
        }
    }

    async function fetchData({ silent } = { silent: false }) {
        const { url, guid, f, t, quickHours } = buildUrl();
        if (!guid) { toast('Chọn thiết bị trước!', 'warn'); return; }

        const cached = getCache(url);
        if (cached) {
            renderPayload(cached, { guid, f, t, quickHours }, { silent: true, fromCache: true });
            return;
        }

        if (inflightAbort) { inflightAbort.abort(); inflightAbort = null; }
        inflightAbort = new AbortController();

        if (!silent) {
            tbody.innerHTML = skeletonRows(8);
            if (apiMsg) apiMsg.textContent = '';
        }
        showLoading(true);

        try {
            const res = await fetch(url, { credentials: 'include', signal: inflightAbort.signal });
            if (!res.ok) {
                const text = await res.text();
                throw new Error(`HTTP ${res.status}: ${text}`);
            }
            const j = await res.json();
            setCache(url, j);
            renderPayload(j, { guid, f, t, quickHours }, { silent, fromCache: false });
        } catch (err) {
            if (err?.name === 'AbortError') return;
            console.error(err);
            if (apiMsg) apiMsg.textContent = 'Lỗi: ' + (err?.message || err);
            updateChart([], [], 0);
            updateStats([], [], 0, guid, new Date(), new Date());
            toast('Lỗi tải dữ liệu', 'err');
        } finally {
            showLoading(false);
            inflightAbort = null;
        }
    }

    function renderPayload(j, meta, opt) {
        const { guid, f, t, quickHours } = meta;
        const { silent, fromCache } = opt;

        if (j.code !== 0) {
            if (apiMsg) apiMsg.textContent = `API code=${j.code} message=${j.message || j.msg || j.error || 'error'}`;
            updateChart([], [], 0);
            updateStats([], [], 0, guid, f || new Date(Date.now() - quickHours * 3600 * 1000), t || new Date());
            return;
        }

        const items = Array.isArray(j.data) ? j.data : [];
        items.sort((a, b) => (a.monitorTime || 0) - (b.monitorTime || 0));

        tbody.innerHTML = '';

        const tc = getTempChan();
        const hc = getHumChan();

        const tempPts = [];
        const humPts = [];
        const tempYs = [];
        const humYs = [];

        let firstMs = null;
        let lastMs = null;

        for (const it of items) {
            const ms = unixToMs(it.monitorTime);
            if (ms == null) continue;

            if (firstMs == null) firstMs = ms;
            lastMs = ms;

            const tmpSel = pickField(it, 'tmp', tc);
            const humSel = pickField(it, 'hum', hc);

            const tt = num(tmpSel);
            const hh = num(humSel);

            if (tt != null) { tempPts.push({ x: ms, y: tt }); tempYs.push(tt); }
            if (hh != null) { humPts.push({ x: ms, y: hh }); humYs.push(hh); }

            // table show all channels
            const timeStr = new Date(ms).toLocaleString('vi-VN', { hour12: false });
            const tr = document.createElement('tr');
            tr.innerHTML = `
        <td>${esc(timeStr)}</td>
        <td>${esc(it.tmp1)}</td>
        <td>${esc(it.tmp2)}</td>
        <td>${esc(it.tmp3)}</td>
        <td>${esc(it.tmp4)}</td>
        <td>${esc(it.hum1)}</td>
        <td>${esc(it.hum2)}</td>
        <td>${esc(it.signal)}</td>
        <td>${esc(it.power)}</td>
        <td>${esc(it.address)}</td>`;
            tbody.appendChild(tr);
        }

        const fromDt = f || (firstMs != null ? new Date(firstMs) : new Date(Date.now() - quickHours * 3600 * 1000));
        const toDt = t || (lastMs != null ? new Date(lastMs) : new Date());
        const rangeMs = (firstMs != null && lastMs != null) ? Math.max(1, lastMs - firstMs) : (toDt - fromDt);

        updateChart(tempPts, humPts, rangeMs);
        updateStats(tempYs, humYs, items.length, guid, fromDt, toDt);

        if (apiMsg) apiMsg.textContent = `Nhận ${items.length} mẫu.${fromCache ? ' (cache)' : ''}`;
        if (!silent) toast(fromCache ? 'Đã cập nhật (cache)' : 'Đã cập nhật dữ liệu');
    }

    // Auto refresh
    function stopAuto() {
        if (autoTimer) clearInterval(autoTimer);
        if (autoTicker) clearInterval(autoTicker);
        autoTimer = null; autoTicker = null;
        if (autoCountdownEl) autoCountdownEl.textContent = '—';
        if (autoToggle) autoToggle.checked = false;
    }

    function startAuto() {
        const s = parseInt(autoInterval?.value || '0', 10);
        if (!Number.isFinite(s) || s <= 0) { stopAuto(); return; }

        if (autoToggle) autoToggle.checked = true;
        if (autoTimer) clearInterval(autoTimer);
        if (autoTicker) clearInterval(autoTicker);

        let left = s;
        if (autoCountdownEl) autoCountdownEl.textContent = left + 's';

        autoTicker = setInterval(() => {
            left--;
            if (left <= 0) left = s;
            if (autoCountdownEl) autoCountdownEl.textContent = left + 's';
        }, 1000);

        autoTimer = setInterval(() => fetchData({ silent: true }), s * 1000);
        toast('Đã bật tự động cập nhật');
    }

    function debouncedFetch(silent = false) {
        if (debounceTimer) clearTimeout(debounceTimer);
        debounceTimer = setTimeout(() => fetchData({ silent }), DEBOUNCE_MS);
    }

    // Events
    document.getElementById('qForm').addEventListener('submit', (e) => {
        e.preventDefault();
        fetchData({ silent: false });
    });

    devicePickEl.addEventListener('change', () => {
        const v = devicePickEl.value;
        if (v) {
            deviceGuidEl.value = v;
            debouncedFetch(false);
        }
    });

    deviceGuidEl.addEventListener('change', () => {
        const v = (deviceGuidEl.value || '').trim();
        if (v) devicePickEl.value = v;
    });

    quickEl.addEventListener('change', () => {
        if (fromEl) fromEl.value = '';
        if (toEl) toEl.value = '';
    });

    if (tempChanEl) tempChanEl.addEventListener('change', () => debouncedFetch(true));
    if (humChanEl) humChanEl.addEventListener('change', () => debouncedFetch(true));
    if (zoomTempToggle) zoomTempToggle.addEventListener('change', () => debouncedFetch(true));
    if (zoomRhToggle) zoomRhToggle.addEventListener('change', () => debouncedFetch(true));

    btnClear.addEventListener('click', () => {
        if (inflightAbort) { inflightAbort.abort(); inflightAbort = null; }
        stopAuto(); setStatus('');

        if (isAdmin) { deviceGuidEl.value = ''; }

        setDefaultRange();
        if (quickEl) quickEl.value = '24';

        tbody.innerHTML = '';
        updateChart([], [], 0);
        updateStats([], [], 0, '', new Date(), new Date());
        if (apiMsg) apiMsg.textContent = '';
    });

    autoToggle.addEventListener('change', (e) => e.target.checked ? startAuto() : stopAuto());
    autoInterval.addEventListener('change', () => { if (autoToggle.checked) startAuto(); });

    btnExport.addEventListener('click', () => {
        const rows = [['Time(UTC+7)', 'tmp1', 'tmp2', 'tmp3', 'tmp4', 'hum1', 'hum2', 'signal', 'power', 'address']];
        for (const tr of tbody.querySelectorAll('tr')) {
            const tds = [...tr.children].map(td => `"${(td.textContent || '').replace(/"/g, '""')}"`);
            rows.push(tds);
        }
        const csv = rows.map(r => r.join(',')).join('\r\n');
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = `elitech_history_${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.csv`;
        a.click();
    });

    // Init
    setDefaultRange();
    ensureChart();
    loadDeviceList().then(() => {
        if ((deviceGuidEl.value || '').trim()) fetchData({ silent: true });
    });

})();
