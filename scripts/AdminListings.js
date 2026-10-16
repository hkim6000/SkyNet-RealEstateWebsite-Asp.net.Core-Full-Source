var AdminListingsJs = (function () {

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
        $ApiRequest('AdminListings/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('li-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('li-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('li-open');
        }, 3200);
    }

    function openNav() {
        el('li-nav').classList.add('li-open');
        el('li-scrim').classList.add('li-open');
    }

    function closeNav() {
        el('li-nav').classList.remove('li-open');
        el('li-scrim').classList.remove('li-open');
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

    var stimer = null;

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('re_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('re_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        setCookie('re_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('li-modal').classList.add('li-open');
        times(el('li-mbody'));
        var f = el('li-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('li-modal');
        if (m) {
            m.classList.remove('li-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('li-per')) {
            el('li-per').value = k;
        }
        if (k !== 'range') {
            if (el('li-from')) {
                el('li-from').value = '';
            }
            if (el('li-to')) {
                el('li-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('li-per') }, { key: 'pfrom', vlu: val('li-from') }, { key: 'pto', vlu: val('li-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('li-main'));
        if (window.AdminListingsJsloaded) {
            window.AdminListingsJsloaded();
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
        if (!askBox || !askBox.classList.contains('li-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('li-open');
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
        askBox.className = 'li-ask';
        askBox.innerHTML = '<div class="li-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="li-err" role="alert"></em><div class="li-askacts"><button type="button" class="li-btn li-btn-ghost li-btn-sm"></button><button type="button" class="li-btn li-btn-sm"></button></div></div>';
        (el('li-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('li-open')) {
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
        x.b[1].className = 'li-btn li-btn-sm' + (o.danger ? ' li-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('li-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('li-open');
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
        m.layer.classList.toggle('li-far', m.s < (m.opts.far != null ? m.opts.far : 1.1));
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
            m.pop.classList.remove('li-open');
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
            m.pop.className = 'li-mpop';
            m.box.appendChild(m.pop);
            m.pop.addEventListener('pointerdown', function (e) {
                e.stopPropagation();
            });
        }
        m.pop.innerHTML = '<button type="button" class="li-mpx" aria-label="Close">&times;</button><a href="' + esc(p.h) + '">' + (p.t ? '<img src="' + esc(p.t) + '" alt="">' : '') + '<div><b>' + esc(p.r) + '</b><span>' + esc(p.f) + '</span><small>' + esc(p.a) + '</small></div></a>';
        m.pop.querySelector('.li-mpx').addEventListener('click', function () {
            mapClosePop(m);
        });
        m.popAt = p;
        m.pop.classList.add('li-open');
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
        var host = m.layer.querySelector('.li-pins');
        var html = '';
        m.data.forEach(function (p, i) {
            html += '<button type="button" class="li-pin' + (p.c ? ' ' + p.c : '') + '" style="left:' + (p.x / 10) + '%;top:' + (p.y / 7) + '%" data-i="' + i + '" data-id="' + esc(p.i) + '" aria-label="' + esc((p.r || '') + ' ' + (p.a || '')) + '">' + esc(p.p || '') + '</button>';
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
        var b = m.layer.querySelector('.li-pin[data-id="' + lid + '"]');
        if (b) {
            b.classList.toggle('li-hot', on);
        }
    }

    function mapInit(id, opts) {
        var box = el(id);
        if (!box || maps[id]) {
            return maps[id];
        }
        var m = { box: box, layer: box.querySelector('.li-mlayer'), s: 1, tx: 0, ty: 0, min: 1, moved: false, justDragged: false, opts: opts || {}, data: [], pts: {}, pop: null, popAt: null };
        maps[id] = m;
        if (m.opts.drag) {
            box.classList.add('li-drag');
        }
        if (m.opts.pick) {
            box.classList.add('li-pickable');
        }
        var start = null, pinch = null, dragged = false;
        box.addEventListener('pointerdown', function (e) {
            if (e.button !== 0 && e.pointerType === 'mouse') {
                return;
            }
            if (!m.opts.drag && e.pointerType !== 'mouse') {
                return;
            }
            if (e.target.closest('.li-mctl')) {
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
                box.classList.add('li-grab');
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
                box.classList.remove('li-grab');
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
            if (m.justDragged || e.target.closest('.li-mctl') || e.target.closest('.li-mpop')) {
                return;
            }
            var pin = e.target.closest('.li-pin');
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
            if (e.target.closest('.li-mctl') || e.target.closest('.li-mpop')) {
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

    function shrink(img, max, q) {
        var w = img.naturalWidth;
        var h = img.naturalHeight;
        var r = Math.min(1, max / Math.max(w, h));
        var c = document.createElement('canvas');
        c.width = Math.round(w * r);
        c.height = Math.round(h * r);
        c.getContext('2d').drawImage(img, 0, 0, c.width, c.height);
        return c.toDataURL('image/jpeg', q);
    }

    function crop(img, w, h, q) {
        var c = document.createElement('canvas');
        c.width = w;
        c.height = h;
        var s = Math.max(w / img.naturalWidth, h / img.naturalHeight);
        var dw = img.naturalWidth * s, dh = img.naturalHeight * s;
        c.getContext('2d').drawImage(img, (w - dw) / 2, (h - dh) / 2, dw, dh);
        return c.toDataURL('image/jpeg', q);
    }

    function readImage(f, done, fail) {
        if (!f || !/^image\/(jpeg|png|webp)$/.test(f.type)) {
            fail('Choose a JPG, PNG or WebP photo.');
            return;
        }
        if (f.size > 25 * 1024 * 1024) {
            fail('That photo is over 25 MB.');
            return;
        }
        var img = new Image();
        var url = URL.createObjectURL(f);
        img.onload = function () {
            URL.revokeObjectURL(url);
            if (img.naturalWidth < 400 || img.naturalHeight < 260) {
                fail('That photo is too small. Use one at least 400 pixels wide.');
                return;
            }
            done(img);
        };
        img.onerror = function () {
            URL.revokeObjectURL(url);
            fail('That image could not be read.');
        };
        img.src = url;
    }

    var photos = [];
    var opened = '';

    window.AdminListingsJsloaded = function () {
        list(1);
        var id = qs('id');
        if (id) {
            edit(id === 'new' ? '' : id);
        }
    };

    function loadArgs() {
        return [{ key: 'arg', vlu: qs('status') }];
    }

    function list(pg) {
        api('List', [{ key: 'lq', vlu: val('li-q') }, { key: 'ldeal', vlu: val('li-fdeal') }, { key: 'lstat', vlu: val('li-fstat') }, { key: 'larea', vlu: val('li-farea') }, { key: 'lagent', vlu: val('li-fagent') }, { key: 'lsort', vlu: val('li-fsort') }, { key: 'pg', vlu: String(pg || 1) }]);
    }

    function edit(id) {
        api('Form', [{ key: 'lid', vlu: id || '' }]);
    }

    function pane(k) {
        var tabs = document.querySelectorAll('#li-mbody .li-tab');
        for (var i = 0; i < tabs.length; i++) {
            tabs[i].classList.toggle('li-act', tabs[i].getAttribute('data-p') === k);
        }
        var ps = document.querySelectorAll('#li-mbody .li-panes > div');
        for (var j = 0; j < ps.length; j++) {
            ps[j].classList.toggle('li-act', ps[j].getAttribute('data-p') === k);
        }
        if (k === 'map') {
            var m = maps['li-pm'];
            if (m) {
                mapFit(m);
            }
        }
    }

    function opened2() {
        var d = el('li-ldata');
        opened = d ? d.getAttribute('data-id') : '';
        try {
            photos = JSON.parse(d.getAttribute('data-photos') || '[]');
        } catch (e) {
            photos = [];
        }
        drawPhotos();
        delete maps['li-pm'];
        var x = +val('li-lx'), y = +val('li-ly');
        mapInit('li-pm', {
            drag: true, wheel: true, far: 0, cx: x, cy: y, zoom: 1.8, pins: [{ i: 'pin', x: x, y: y, c: 'li-dot' }], pick: function (px, py) {
                el('li-lx').value = px;
                el('li-ly').value = py;
                el('li-lxy').textContent = px + ', ' + py;
                var m = maps['li-pm'];
                m.opts.cx = px;
                m.opts.cy = py;
                m.moved = true;
                mapPins('li-pm', [{ i: 'pin', x: px, y: py, c: 'li-dot' }]);
            }
        });
        dealView();
        openModal();
    }

    function dealView() {
        var b = el('li-lform');
        if (b) {
            b.classList.toggle('li-isrent', val('li-ldeal') === 'rent');
        }
    }

    function drawPhotos() {
        var box = el('li-photos');
        if (!box) {
            return;
        }
        if (!photos.length) {
            box.innerHTML = '<p class="li-none">No photos yet. Add some from the library or upload your own.</p>';
        } else {
            box.innerHTML = photos.map(function (p, i) {
                var t = p.replace('/full/', '/thumbs/');
                return '<div class="li-ph1">' + (i === 0 ? '<span class="li-pcov">Cover</span>' : '') + '<img src="' + esc(t) + '" alt="Photo ' + (i + 1) + '"><div class="li-pbtns"><button type="button" aria-label="Move earlier" onclick="AdminListingsJs.move(' + i + ',-1)"' + (i === 0 ? ' disabled' : '') + '><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 15l6-6 6 6"/></svg></button><button type="button" aria-label="Move later" onclick="AdminListingsJs.move(' + i + ',1)"' + (i === photos.length - 1 ? ' disabled' : '') + '><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M6 9l6 6 6-6"/></svg></button><button type="button" aria-label="Remove photo" onclick="AdminListingsJs.unpick(' + i + ')"><svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3"/></svg></button></div></div>';
            }).join('');
        }
        var n = el('li-pcount');
        if (n) {
            n.textContent = String(photos.length);
        }
    }

    function move(i, d) {
        var j = i + d;
        if (j < 0 || j >= photos.length) {
            return;
        }
        var t = photos[i];
        photos[i] = photos[j];
        photos[j] = t;
        drawPhotos();
    }

    function unpick(i) {
        photos.splice(i, 1);
        drawPhotos();
    }

    function pick(src) {
        if (photos.indexOf(src) >= 0) {
            toast('That photo is already in this listing.');
            return;
        }
        if (photos.length >= 24) {
            toast('A listing can have up to 24 photos.');
            return;
        }
        photos.push(src);
        drawPhotos();
    }

    function upload(input) {
        var f = input.files && input.files[0];
        var err = el('li-e-upl');
        err.textContent = '';
        readImage(f, function (img) {
            el('li-upbtn').disabled = true;
            el('li-upst').textContent = 'Uploading...';
            api('Upload', [{ key: 'full', vlu: shrink(img, 1600, 0.86) }, { key: 'thumb', vlu: crop(img, 640, 427, 0.82) }]);
            input.value = '';
        }, function (m) {
            err.textContent = m;
            input.value = '';
        });
    }

    function uploaded(src, msg) {
        el('li-upbtn').disabled = false;
        el('li-upst').textContent = '';
        if (!src) {
            el('li-e-upl').textContent = msg || 'Upload failed.';
            return;
        }
        var lib = el('li-lib');
        if (lib) {
            lib.insertAdjacentHTML('afterbegin', '<button type="button" onclick="AdminListingsJs.pick(\'' + src + '\')" aria-label="Add this photo"><img src="' + esc(src.replace('/full/', '/thumbs/')) + '" alt=""></button>');
        }
        pick(src);
        toast('Photo uploaded and added.');
    }

    function addOpen() {
        el('li-opens').insertAdjacentHTML('beforeend', el('li-ohtpl').innerHTML);
    }

    function dropRow(b) {
        b.parentNode.remove();
    }

    function save() {
        var opens = [];
        var rows = document.querySelectorAll('#li-opens .li-ohrow');
        for (var i = 0; i < rows.length; i++) {
            var x = rows[i].querySelectorAll('input');
            if (x[0].value) {
                opens.push(x[0].value + '|' + x[1].value + '|' + x[2].value);
            }
        }
        var keys = ['ltitle', 'lstreet', 'larea', 'ldeal', 'lkind', 'lstyle', 'lprice', 'lbeds', 'lbaths', 'lsqft', 'llot', 'lyear', 'lpark', 'lhoa', 'ltax', 'lavail', 'lagent', 'lstatus', 'lclosed', 'ldesc', 'lfeat', 'lx', 'ly'];
        var list2 = keys.map(function (k) {
            return { key: k, vlu: el('li-' + k) ? el('li-' + k).value : '' };
        });
        list2.push({ key: 'lid', vlu: opened });
        list2.push({ key: 'lfeatured', vlu: el('li-lfeatured') && el('li-lfeatured').checked ? '1' : '0' });
        list2.push({ key: 'lphotos', vlu: photos.join('|') });
        list2.push({ key: 'lopens', vlu: opens.join('~') });
        api('Save', list2);
    }

    function saved(id, isNew) {
        closeModal();
        list(1);
        toast(isNew ? 'Listing ' + id + ' added.' : 'Saved.');
        try {
            history.replaceState(null, '', 'AdminListings');
        } catch (e) {
        }
    }

    function fail(msg, p) {
        el('li-e-save').textContent = msg;
        if (p) {
            pane(p);
        }
    }

    function remove(id, name) {
        ask({ title: 'Delete ' + name + '?', text: 'The listing disappears from the website and the admin. Its leads and viewings stay. This cannot be undone.', ok: 'Delete listing', no: 'Keep it', danger: true }, function () {
            api('Delete', [{ key: 'lid', vlu: id }]);
        });
    }

    function deleted() {
        closeModal();
        list(1);
        toast('Listing deleted.');
    }

    function zoom(id, f) {
        mapZoom(id, f);
    }

    function fit(id) {
        mapReset(id);
    }

    function reveal() {
        if (!getCookie('re_tok')) {
            location.replace('AdminLogin');
            return;
        }
        api('Load', loadArgs());
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                closeModal();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('li-', 'li-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: closeModal,
        per: per,
        search: search,
        loaded: loaded,
        list: list,
        edit: edit,
        pane: pane,
        opened: opened2,
        dealView: dealView,
        move: move,
        unpick: unpick,
        pick: pick,
        upload: upload,
        uploaded: uploaded,
        addOpen: addOpen,
        dropRow: dropRow,
        save: save,
        saved: saved,
        fail: fail,
        remove: remove,
        deleted: deleted,
        zoom: zoom,
        fit: fit
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminListingsJs.reveal);
} else {
    AdminListingsJs.reveal();
}
