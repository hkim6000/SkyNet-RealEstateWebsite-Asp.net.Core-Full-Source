var ContactJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Contact/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ct-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ct-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ct-open');
        }, 3200);
    }

    function openNav() {
        el('ct-nav').classList.add('ct-open');
        el('ct-scrim').classList.add('ct-open');
    }

    function closeNav() {
        el('ct-nav').classList.remove('ct-open');
        el('ct-scrim').classList.remove('ct-open');
    }

    function ago(d) {
        var s = (Date.now() - d.getTime()) / 1000;
        if (s < 60) {
            return 'Just now';
        }
        if (s < 3600) {
            var m = Math.floor(s / 60);
            return m + (m === 1 ? ' min ago' : ' mins ago');
        }
        if (s < 86400) {
            var h = Math.floor(s / 3600);
            return h + (h === 1 ? ' hour ago' : ' hours ago');
        }
        return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    }

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'hm') {
                t.textContent = d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    var maps = {};

    function mapBox(m) {
        return { w: m.box.clientWidth, h: m.box.clientHeight };
    }

    function mapMin(m) {
        var b = mapBox(m);
        return Math.max(Math.min(b.w / 1000, b.h / 700), b.w / 1600, b.h / 1200);
    }

    function mapApply(m) {
        var b = mapBox(m);
        m.min = mapMin(m);
        m.s = Math.min(Math.max(m.s, m.min), m.min * 6);
        m.tx = Math.min(300 * m.s, Math.max(b.w - 1300 * m.s, m.tx));
        m.ty = Math.min(250 * m.s, Math.max(b.h - 950 * m.s, m.ty));
        m.layer.style.transform = 'translate(' + m.tx + 'px,' + m.ty + 'px) scale(' + m.s + ')';
        m.layer.style.setProperty('--s', m.s);
        m.layer.classList.toggle('ct-far', m.s < (m.opts.far != null ? m.opts.far : 1.1));
        mapPlacePop(m);
    }

    function mapCenter(m, x, y, s) {
        var b = mapBox(m);
        m.s = s;
        m.tx = b.w / 2 - x * s;
        m.ty = b.h / 2 - y * s;
        mapApply(m);
    }

    function mapView(m) {
        return { x1: -m.tx / m.s, y1: -m.ty / m.s, x2: (-m.tx + mapBox(m).w) / m.s, y2: (-m.ty + mapBox(m).h) / m.s };
    }

    function mapFit(m) {
        var b = mapBox(m);
        if (!b.w || !b.h) {
            return;
        }
        var min = mapMin(m);
        var list = m.data || [];
        if (m.opts.cx != null) {
            mapCenter(m, m.opts.cx, m.opts.cy, min * (m.opts.zoom || 1));
            return;
        }
        if (!list.length) {
            mapCenter(m, 500, 350, min);
            return;
        }
        var x1 = 1e9, y1 = 1e9, x2 = -1e9, y2 = -1e9;
        list.forEach(function (p) {
            x1 = Math.min(x1, p.x);
            y1 = Math.min(y1, p.y);
            x2 = Math.max(x2, p.x);
            y2 = Math.max(y2, p.y);
        });
        var pad = 70;
        var s = Math.min((b.w - pad * 2) / Math.max(40, x2 - x1), (b.h - pad * 2) / Math.max(40, y2 - y1));
        mapCenter(m, (x1 + x2) / 2, (y1 + y2) / 2 - 10, Math.min(Math.max(s, min), min * 3));
    }

    function mapZoom(id, f, cx, cy) {
        var m = maps[id];
        if (!m) {
            return;
        }
        var b = mapBox(m);
        cx = cx == null ? b.w / 2 : cx;
        cy = cy == null ? b.h / 2 : cy;
        var ns = Math.min(Math.max(m.s * f, m.min), m.min * 6);
        var k = ns / m.s;
        m.tx = cx - (cx - m.tx) * k;
        m.ty = cy - (cy - m.ty) * k;
        m.s = ns;
        mapApply(m);
        mapMoved(m);
    }

    function mapReset(id) {
        var m = maps[id];
        if (m) {
            mapFit(m);
            m.moved = false;
            if (m.opts.onMove) {
                m.opts.onMove(false);
            }
        }
    }

    function mapMoved(m) {
        m.moved = true;
        if (m.opts.onMove) {
            m.opts.onMove(true);
        }
    }

    function mapClosePop(m) {
        if (m.pop) {
            m.pop.classList.remove('ct-open');
        }
        m.popAt = null;
    }

    function mapPlacePop(m) {
        if (!m.pop || !m.popAt) {
            return;
        }
        var b = mapBox(m);
        var px = m.tx + m.popAt.x * m.s;
        var py = m.ty + m.popAt.y * m.s;
        var w = m.pop.offsetWidth || 250;
        var h = m.pop.offsetHeight || 260;
        var left = Math.min(Math.max(8, px - w / 2), b.w - w - 8);
        var top = py - h - 34;
        if (top < 8) {
            top = Math.min(py + 12, b.h - h - 8);
        }
        m.pop.style.left = left + 'px';
        m.pop.style.top = Math.max(8, top) + 'px';
    }

    function esc(t) {
        return String(t == null ? '' : t).replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    function mapOpenPop(m, p) {
        if (!m.pop) {
            m.pop = document.createElement('div');
            m.pop.className = 'ct-mpop';
            m.box.appendChild(m.pop);
            m.pop.addEventListener('pointerdown', function (e) {
                e.stopPropagation();
            });
        }
        m.pop.innerHTML = '<button type="button" class="ct-mpx" aria-label="Close">&times;</button><a href="' + esc(p.h) + '">' + (p.t ? '<img src="' + esc(p.t) + '" alt="">' : '') + '<div><b>' + esc(p.r) + '</b><span>' + esc(p.f) + '</span><small>' + esc(p.a) + '</small></div></a>';
        m.pop.querySelector('.ct-mpx').addEventListener('click', function () {
            mapClosePop(m);
        });
        m.popAt = p;
        m.pop.classList.add('ct-open');
        mapPlacePop(m);
        var img = m.pop.querySelector('img');
        if (img) {
            img.addEventListener('load', function () {
                mapPlacePop(m);
            });
        }
    }

    function mapPins(id, list) {
        var m = maps[id];
        if (!m) {
            return;
        }
        m.data = list || [];
        var host = m.layer.querySelector('.ct-pins');
        var html = '';
        m.data.forEach(function (p, i) {
            html += '<button type="button" class="ct-pin' + (p.c ? ' ' + p.c : '') + '" style="left:' + (p.x / 10) + '%;top:' + (p.y / 7) + '%" data-i="' + i + '" data-id="' + esc(p.i) + '" aria-label="' + esc((p.r || '') + ' ' + (p.a || '')) + '">' + esc(p.p || '') + '</button>';
        });
        host.innerHTML = html;
        mapClosePop(m);
        if (!m.moved || m.opts.refit) {
            mapFit(m);
        }
    }

    function mapHot(id, lid, on) {
        var m = maps[id];
        if (!m) {
            return;
        }
        var b = m.layer.querySelector('.ct-pin[data-id="' + lid + '"]');
        if (b) {
            b.classList.toggle('ct-hot', on);
        }
    }

    function mapInit(id, opts) {
        var box = el(id);
        if (!box || maps[id]) {
            return maps[id];
        }
        var m = { box: box, layer: box.querySelector('.ct-mlayer'), s: 1, tx: 0, ty: 0, min: 1, moved: false, justDragged: false, opts: opts || {}, data: [], pts: {}, pop: null, popAt: null };
        maps[id] = m;
        if (m.opts.drag) {
            box.classList.add('ct-drag');
        }
        if (m.opts.pick) {
            box.classList.add('ct-pickable');
        }
        var start = null, pinch = null, dragged = false;
        box.addEventListener('pointerdown', function (e) {
            if (e.button !== 0 && e.pointerType === 'mouse') {
                return;
            }
            if (!m.opts.drag && e.pointerType !== 'mouse') {
                return;
            }
            if (e.target.closest('.ct-mctl')) {
                return;
            }
            m.pts[e.pointerId] = { x: e.clientX, y: e.clientY };
            var ids = Object.keys(m.pts);
            if (ids.length === 1) {
                start = { x: e.clientX, y: e.clientY, tx: m.tx, ty: m.ty };
                dragged = false;
            } else if (ids.length === 2) {
                var a = m.pts[ids[0]], c = m.pts[ids[1]];
                pinch = { d: Math.hypot(a.x - c.x, a.y - c.y), s: m.s };
            }
        });
        window.addEventListener('pointermove', function (e) {
            if (!m.pts[e.pointerId]) {
                return;
            }
            m.pts[e.pointerId] = { x: e.clientX, y: e.clientY };
            var ids = Object.keys(m.pts);
            if (ids.length === 2 && pinch) {
                var a = m.pts[ids[0]], c = m.pts[ids[1]];
                var r = box.getBoundingClientRect();
                var d = Math.hypot(a.x - c.x, a.y - c.y);
                mapZoom(id, (pinch.s * d / pinch.d) / m.s, (a.x + c.x) / 2 - r.left, (a.y + c.y) / 2 - r.top);
                dragged = true;
                return;
            }
            if (!start) {
                return;
            }
            var dx = e.clientX - start.x, dy = e.clientY - start.y;
            if (!dragged && Math.abs(dx) + Math.abs(dy) < 5) {
                return;
            }
            if (!dragged) {
                dragged = true;
                box.classList.add('ct-grab');
                try {
                    box.setPointerCapture(e.pointerId);
                } catch (x) {
                }
            }
            m.tx = start.tx + dx;
            m.ty = start.ty + dy;
            mapApply(m);
        });
        var up = function (e) {
            if (!m.pts[e.pointerId]) {
                return;
            }
            delete m.pts[e.pointerId];
            if (Object.keys(m.pts).length < 2) {
                pinch = null;
            }
            if (Object.keys(m.pts).length === 0) {
                box.classList.remove('ct-grab');
                if (dragged) {
                    m.justDragged = true;
                    setTimeout(function () {
                        m.justDragged = false;
                    }, 0);
                    mapMoved(m);
                }
                dragged = false;
                start = null;
            }
        };
        window.addEventListener('pointerup', up);
        window.addEventListener('pointercancel', up);
        box.addEventListener('click', function (e) {
            if (m.justDragged || e.target.closest('.ct-mctl') || e.target.closest('.ct-mpop')) {
                return;
            }
            var pin = e.target.closest('.ct-pin');
            if (pin && pin.hasAttribute('data-i')) {
                var p = m.data[+pin.getAttribute('data-i')];
                if (m.opts.onPin) {
                    m.opts.onPin(p);
                } else if (p && p.h) {
                    mapOpenPop(m, p);
                }
                return;
            }
            mapClosePop(m);
            if (m.opts.pick) {
                var r = box.getBoundingClientRect();
                var x = (e.clientX - r.left - m.tx) / m.s;
                var y = (e.clientY - r.top - m.ty) / m.s;
                m.opts.pick(Math.round(Math.min(Math.max(x, 0), 1000) * 10) / 10, Math.round(Math.min(Math.max(y, 0), 700) * 10) / 10);
            }
        });
        box.addEventListener('wheel', function (e) {
            if (!m.opts.wheel && !e.ctrlKey && !e.metaKey) {
                return;
            }
            e.preventDefault();
            var r = box.getBoundingClientRect();
            mapZoom(id, e.deltaY < 0 ? 1.18 : 1 / 1.18, e.clientX - r.left, e.clientY - r.top);
        }, { passive: false });
        box.addEventListener('dblclick', function (e) {
            if (e.target.closest('.ct-mctl') || e.target.closest('.ct-mpop')) {
                return;
            }
            var r = box.getBoundingClientRect();
            mapZoom(id, 1.6, e.clientX - r.left, e.clientY - r.top);
        });
        box.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                mapClosePop(m);
            }
        });
        var last = { w: 0, h: 0 };
        var resized = function () {
            var b = mapBox(m);
            if (!b.w || !b.h || (b.w === last.w && b.h === last.h)) {
                return;
            }
            var v = last.w ? mapView(m) : null;
            last = b;
            if (v && m.moved) {
                mapCenter(m, (v.x1 + v.x2) / 2, (v.y1 + v.y2) / 2, m.s);
            } else {
                mapFit(m);
            }
        };
        if (window.ResizeObserver) {
            new ResizeObserver(resized).observe(box);
        } else {
            window.addEventListener('resize', resized);
        }
        if (m.opts.pins) {
            mapPins(id, m.opts.pins);
        }
        resized();
        return m;
    }

    function readPins(id) {
        var b = el(id);
        if (!b) {
            return [];
        }
        try {
            return JSON.parse(b.getAttribute('data-pins') || '[]');
        } catch (e) {
            return [];
        }
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('ct-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ct-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        if (askCheck) {
            var m = askCheck(x.i.value.trim());
            if (m) {
                x.e.textContent = m;
                x.i.focus();
                return;
            }
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'ct-ask';
        askBox.innerHTML = '<div class="ct-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ct-err" role="alert"></em><div class="ct-askacts"><button type="button" class="ct-btn ct-btn-ghost ct-btn-sm"></button><button type="button" class="ct-btn ct-btn-sm"></button></div></div>';
        (el('ct-site') || document.body).appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () {
            askClose(false);
        });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () {
            x.e.textContent = '';
        });
        askBox.addEventListener('mousedown', function (e) {
            if (e.target === askBox) {
                askClose(false);
            }
        });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('ct-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) {
                    return n.offsetParent !== null;
                });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts();
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'ct-btn ct-btn-sm' + (o.danger ? ' ct-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ct-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ct-open');
        if (input) {
            x.i.focus();
            x.i.setSelectionRange(x.i.value.length, x.i.value.length);
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function leave(go) {
        ask({ title: 'Leave without saving?', text: 'Your changes since the last save will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, go);
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function applyTheme(theme) {
        el('ct-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('ct-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('re-theme', next);
        } catch (e) {
        }
    }

    function savedList() {
        try {
            var v = JSON.parse(localStorage.getItem('re-saved') || '[]');
            return Array.isArray(v) ? v.filter(function (x) {
                return typeof x === 'string' && /^[A-Za-z0-9-]{1,20}$/.test(x);
            }) : [];
        } catch (e) {
            return [];
        }
    }

    function savedStore(list) {
        try {
            localStorage.setItem('re-saved', JSON.stringify(list.slice(0, 200)));
            return true;
        } catch (e) {
            return false;
        }
    }

    function savedCount() {
        var n = savedList().length;
        var c = el('ct-scount');
        if (c) {
            c.textContent = n > 99 ? '99+' : String(n);
            c.hidden = n === 0;
        }
    }

    function hearts(root) {
        var s = savedList();
        var list = (root || document).querySelectorAll('[data-heart]');
        for (var i = 0; i < list.length; i++) {
            var on = s.indexOf(list[i].getAttribute('data-heart')) >= 0;
            list[i].setAttribute('aria-pressed', on ? 'true' : 'false');
            list[i].setAttribute('aria-label', on ? 'Remove from saved homes' : 'Save this home');
            list[i].title = on ? 'Saved' : 'Save';
        }
    }

    function heart(b) {
        var id = b.getAttribute('data-heart');
        var s = savedList();
        var at = s.indexOf(id);
        var on = at < 0;
        if (on) {
            s.unshift(id);
        } else {
            s.splice(at, 1);
        }
        if (!savedStore(s)) {
            toast('Saving needs browser storage, which is turned off here.');
            return;
        }
        hearts();
        savedCount();
        toast(on ? 'Saved. Find it under the heart at the top.' : 'Removed from saved homes.');
        call('Heart', [{ key: 'hid', vlu: id }, { key: 'hon', vlu: on ? '1' : '0' }]);
        if (window.ContactJssaved) {
            window.ContactJssaved(id, on);
        }
    }

    function subscribe() {
        call('Subscribe', [{ key: 'nlmail', vlu: val('ct-nlmail') }]);
        return false;
    }

    function subscribed(msg) {
        el('ct-nlmail').value = '';
        toast(msg);
    }

    function zoom(id, f) {
        mapZoom(id, f);
    }

    function fit(id) {
        mapReset(id);
    }

    function fieldErr(id, msg) {
        var e = el('ct-e-' + id);
        if (e) {
            e.textContent = msg;
        }
        var f = el('ct-' + id);
        if (f && f.focus) {
            f.focus();
        }
    }

    function send() {
        el('ct-csend').disabled = true;
        var list = [];
        ['ctopic', 'cname', 'cphone', 'cmail', 'cmsg', 'cweb'].forEach(function (k) {
            list.push({ key: k, vlu: val('ct-' + k) });
        });
        call('Send', list);
        return false;
    }

    function sent(ok, text) {
        el('ct-csend').disabled = false;
        if (!ok) {
            return;
        }
        el('ct-cf').hidden = true;
        el('ct-cdone').innerHTML = '<div class="ct-ok-box" role="status"><b>Message sent</b><p></p></div>';
        el('ct-cdone').querySelector('p').textContent = text;
    }

    function start() {
        mapInit('ct-cmap', { pins: readPins('ct-cmap'), cx: 548, cy: 470, zoom: 2.4, far: 0 });
    }

    function reveal() {
        var stored = '';
        try {
            stored = localStorage.getItem('re-theme') || '';
        } catch (e) {
        }
        if (stored) {
            applyTheme(stored);
        }
        times();
        hearts();
        savedCount();
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('ct-', 'ct-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
        start();
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        heart: heart,
        subscribe: subscribe,
        subscribed: subscribed,
        zoom: zoom,
        fit: fit,
        fieldErr: fieldErr,
        send: send,
        sent: sent
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ContactJs.reveal);
} else {
    ContactJs.reveal();
}
