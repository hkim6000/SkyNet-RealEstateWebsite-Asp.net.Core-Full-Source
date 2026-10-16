var ListingJs = (function () {

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
        $ApiRequest('Listing/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('lt-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('lt-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('lt-open');
        }, 3200);
    }

    function openNav() {
        el('lt-nav').classList.add('lt-open');
        el('lt-scrim').classList.add('lt-open');
    }

    function closeNav() {
        el('lt-nav').classList.remove('lt-open');
        el('lt-scrim').classList.remove('lt-open');
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
        m.layer.classList.toggle('lt-far', m.s < (m.opts.far != null ? m.opts.far : 1.1));
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
            m.pop.classList.remove('lt-open');
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
            m.pop.className = 'lt-mpop';
            m.box.appendChild(m.pop);
            m.pop.addEventListener('pointerdown', function (e) {
                e.stopPropagation();
            });
        }
        m.pop.innerHTML = '<button type="button" class="lt-mpx" aria-label="Close">&times;</button><a href="' + esc(p.h) + '">' + (p.t ? '<img src="' + esc(p.t) + '" alt="">' : '') + '<div><b>' + esc(p.r) + '</b><span>' + esc(p.f) + '</span><small>' + esc(p.a) + '</small></div></a>';
        m.pop.querySelector('.lt-mpx').addEventListener('click', function () {
            mapClosePop(m);
        });
        m.popAt = p;
        m.pop.classList.add('lt-open');
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
        var host = m.layer.querySelector('.lt-pins');
        var html = '';
        m.data.forEach(function (p, i) {
            html += '<button type="button" class="lt-pin' + (p.c ? ' ' + p.c : '') + '" style="left:' + (p.x / 10) + '%;top:' + (p.y / 7) + '%" data-i="' + i + '" data-id="' + esc(p.i) + '" aria-label="' + esc((p.r || '') + ' ' + (p.a || '')) + '">' + esc(p.p || '') + '</button>';
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
        var b = m.layer.querySelector('.lt-pin[data-id="' + lid + '"]');
        if (b) {
            b.classList.toggle('lt-hot', on);
        }
    }

    function mapInit(id, opts) {
        var box = el(id);
        if (!box || maps[id]) {
            return maps[id];
        }
        var m = { box: box, layer: box.querySelector('.lt-mlayer'), s: 1, tx: 0, ty: 0, min: 1, moved: false, justDragged: false, opts: opts || {}, data: [], pts: {}, pop: null, popAt: null };
        maps[id] = m;
        if (m.opts.drag) {
            box.classList.add('lt-drag');
        }
        if (m.opts.pick) {
            box.classList.add('lt-pickable');
        }
        var start = null, pinch = null, dragged = false;
        box.addEventListener('pointerdown', function (e) {
            if (e.button !== 0 && e.pointerType === 'mouse') {
                return;
            }
            if (!m.opts.drag && e.pointerType !== 'mouse') {
                return;
            }
            if (e.target.closest('.lt-mctl')) {
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
                box.classList.add('lt-grab');
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
                box.classList.remove('lt-grab');
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
            if (m.justDragged || e.target.closest('.lt-mctl') || e.target.closest('.lt-mpop')) {
                return;
            }
            var pin = e.target.closest('.lt-pin');
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
            if (e.target.closest('.lt-mctl') || e.target.closest('.lt-mpop')) {
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
        if (!askBox || !askBox.classList.contains('lt-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('lt-open');
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
        askBox.className = 'lt-ask';
        askBox.innerHTML = '<div class="lt-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="lt-err" role="alert"></em><div class="lt-askacts"><button type="button" class="lt-btn lt-btn-ghost lt-btn-sm"></button><button type="button" class="lt-btn lt-btn-sm"></button></div></div>';
        (el('lt-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('lt-open')) {
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
        x.b[1].className = 'lt-btn lt-btn-sm' + (o.danger ? ' lt-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('lt-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('lt-open');
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
        el('lt-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('lt-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
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
        var c = el('lt-scount');
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
        if (window.ListingJssaved) {
            window.ListingJssaved(id, on);
        }
    }

    function subscribe() {
        call('Subscribe', [{ key: 'nlmail', vlu: val('lt-nlmail') }]);
        return false;
    }

    function subscribed(msg) {
        el('lt-nlmail').value = '';
        toast(msg);
    }

    function zoom(id, f) {
        mapZoom(id, f);
    }

    function fit(id) {
        mapReset(id);
    }

    function fieldErr(id, msg) {
        var e = el('lt-e-' + id);
        if (e) {
            e.textContent = msg;
        }
        var f = el('lt-' + id);
        if (f && f.focus) {
            f.focus();
        }
    }

    var photos = [];
    var at = 0;
    var ptimer = null;
    var lbBack = null;
    var touchX = null;

    function lid() {
        return qs('id');
    }

    function paint() {
        var p = photos[at];
        if (!p) {
            return;
        }
        var img = el('lt-lbimg');
        img.src = p.f;
        img.alt = p.c;
        el('lt-lbn').textContent = (at + 1) + ' / ' + photos.length;
        el('lt-lbc').textContent = p.c;
        var list = el('lt-lbthumbs').children;
        for (var i = 0; i < list.length; i++) {
            list[i].classList.toggle('lt-act', i === at);
            list[i].setAttribute('aria-current', i === at ? 'true' : 'false');
        }
        if (list[at] && list[at].scrollIntoView) {
            list[at].scrollIntoView({ block: 'nearest', inline: 'center' });
        }
        var next = photos[(at + 1) % photos.length];
        if (next) {
            new Image().src = next.f;
        }
    }

    function show(i) {
        if (!photos.length) {
            return;
        }
        lbBack = document.activeElement;
        at = Math.max(0, Math.min(i || 0, photos.length - 1));
        var lb = el('lt-lb');
        lb.hidden = false;
        document.documentElement.classList.add('lt-lock');
        paint();
        lb.querySelector('.lt-lbx').focus();
    }

    function hide() {
        el('lt-lb').hidden = true;
        document.documentElement.classList.remove('lt-lock');
        if (lbBack && lbBack.focus) {
            lbBack.focus();
        }
    }

    function step(d) {
        at = (at + d + photos.length) % photos.length;
        paint();
    }

    function share() {
        var url = location.href;
        if (navigator.share) {
            navigator.share({ title: document.title, url: url }).catch(function () {
            });
            return;
        }
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(url).then(function () {
                toast('Link copied.');
            }, function () {
                toast(url);
            });
        } else {
            toast(url);
        }
    }

    function tab(k) {
        var f = el('lt-lform');
        f.setAttribute('data-kind', k);
        ['viewing', 'question'].forEach(function (x) {
            var b = el('lt-tab-' + x);
            if (b) {
                b.classList.toggle('lt-act', x === k);
                b.setAttribute('aria-selected', x === k ? 'true' : 'false');
            }
        });
        el('lt-lsend').textContent = k === 'viewing' ? 'Request this time' : 'Send question';
        el('lt-lmsg').placeholder = k === 'viewing' ? 'Anything we should know before the viewing?' : 'What would you like to know?';
    }

    function days() {
        var d = el('lt-lday');
        var t = el('lt-ltime');
        if (!d || !t) {
            return;
        }
        var o = d.options[d.selectedIndex];
        var keep = t.value;
        var html = '';
        (o ? o.getAttribute('data-t') || '' : '').split(',').forEach(function (s) {
            if (!s) {
                return;
            }
            var p = s.split('|');
            html += '<option value="' + esc(p[0]) + '"' + (p[0] === keep ? ' selected' : '') + '>' + esc(p[1]) + '</option>';
        });
        t.innerHTML = html;
    }

    function send() {
        var k = el('lt-lform').getAttribute('data-kind');
        el('lt-lsend').disabled = true;
        call('Send', [{ key: 'lid', vlu: lid() }, { key: 'lkind', vlu: k }, { key: 'lname', vlu: val('lt-lname') }, { key: 'lmail', vlu: val('lt-lmail') }, { key: 'lphone', vlu: val('lt-lphone') }, { key: 'lmsg', vlu: val('lt-lmsg') }, { key: 'lday', vlu: k === 'viewing' ? val('lt-lday') : '' }, { key: 'ltime', vlu: k === 'viewing' ? val('lt-ltime') : '' }, { key: 'lweb', vlu: val('lt-lweb') }]);
        return false;
    }

    function sent(ok, text) {
        el('lt-lsend').disabled = false;
        if (!ok) {
            return;
        }
        el('lt-lform').hidden = true;
        var t = document.querySelector('.lt-lside .lt-tabs');
        if (t) {
            t.hidden = true;
        }
        el('lt-ldone').innerHTML = '<div class="lt-ok-box" role="status"><b>Request sent</b><p></p></div><button type="button" class="lt-btn lt-btn-ghost lt-btn-sm lt-again" onclick="ListingJs.again()">Send another message</button>';
        el('lt-ldone').querySelector('p').textContent = text;
    }

    function again() {
        el('lt-ldone').innerHTML = '';
        el('lt-lform').hidden = false;
        var t = document.querySelector('.lt-lside .lt-tabs');
        if (t) {
            t.hidden = false;
        }
        el('lt-lmsg').value = '';
    }

    function pay() {
        clearTimeout(ptimer);
        ptimer = setTimeout(function () {
            call('Pay', [{ key: 'lid', vlu: lid() }, { key: 'mprice', vlu: val('lt-mprice') }, { key: 'mdown', vlu: val('lt-mdown') }, { key: 'mrate', vlu: val('lt-mrate') }, { key: 'mterm', vlu: val('lt-mterm') }]);
        }, 300);
    }

    function start() {
        var g = el('lt-gal');
        if (g) {
            try {
                photos = JSON.parse(g.getAttribute('data-photos') || '[]');
            } catch (e) {
                photos = [];
            }
            var th = '';
            photos.forEach(function (p, i) {
                th += '<button type="button" aria-label="Photo ' + (i + 1) + ': ' + esc(p.c) + '" onclick="ListingJs.go(' + i + ')"><img src="' + esc(p.t) + '" alt="" loading="lazy"></button>';
            });
            el('lt-lbthumbs').innerHTML = th;
        }
        var lb = el('lt-lb');
        if (lb) {
            lb.addEventListener('keydown', function (e) {
                if (e.key === 'Escape') {
                    e.stopPropagation();
                    hide();
                } else if (e.key === 'ArrowLeft') {
                    step(-1);
                } else if (e.key === 'ArrowRight') {
                    step(1);
                } else if (e.key === 'Tab') {
                    var f = lb.querySelectorAll('button');
                    var first = f[0], last = f[f.length - 1];
                    if (e.shiftKey && document.activeElement === first) {
                        e.preventDefault();
                        last.focus();
                    } else if (!e.shiftKey && document.activeElement === last) {
                        e.preventDefault();
                        first.focus();
                    }
                }
            });
            var stage = lb.querySelector('.lt-lbstage');
            stage.addEventListener('touchstart', function (e) {
                touchX = e.touches[0].clientX;
            }, { passive: true });
            stage.addEventListener('touchend', function (e) {
                if (touchX == null) {
                    return;
                }
                var dx = e.changedTouches[0].clientX - touchX;
                touchX = null;
                if (Math.abs(dx) > 40) {
                    step(dx < 0 ? 1 : -1);
                }
            });
            stage.addEventListener('click', function (e) {
                if (e.target === stage) {
                    hide();
                }
            });
        }
        var pins = readPins('lt-lmap');
        if (pins.length) {
            mapInit('lt-lmap', { pins: pins, cx: pins[0].x, cy: pins[0].y, zoom: 2.2, far: 0 });
        }
        days();
    }

    function goPhoto(i) {
        at = i;
        paint();
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
            var id = e.target && e.target.id ? e.target.id.replace('lt-', 'lt-e-') : '';
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
        show: show,
        hide: hide,
        step: step,
        go: goPhoto,
        share: share,
        tab: tab,
        days: days,
        send: send,
        sent: sent,
        again: again,
        pay: pay
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ListingJs.reveal);
} else {
    ListingJs.reveal();
}
