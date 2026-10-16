var AdminAgentsJs = (function () {

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
        $ApiRequest('AdminAgents/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ta-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ta-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ta-open');
        }, 3200);
    }

    function openNav() {
        el('ta-nav').classList.add('ta-open');
        el('ta-scrim').classList.add('ta-open');
    }

    function closeNav() {
        el('ta-nav').classList.remove('ta-open');
        el('ta-scrim').classList.remove('ta-open');
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
        el('ta-modal').classList.add('ta-open');
        times(el('ta-mbody'));
        var f = el('ta-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('ta-modal');
        if (m) {
            m.classList.remove('ta-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('ta-per')) {
            el('ta-per').value = k;
        }
        if (k !== 'range') {
            if (el('ta-from')) {
                el('ta-from').value = '';
            }
            if (el('ta-to')) {
                el('ta-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('ta-per') }, { key: 'pfrom', vlu: val('ta-from') }, { key: 'pto', vlu: val('ta-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('ta-main'));
        if (window.AdminAgentsJsloaded) {
            window.AdminAgentsJsloaded();
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
        if (!askBox || !askBox.classList.contains('ta-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ta-open');
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
        askBox.className = 'ta-ask';
        askBox.innerHTML = '<div class="ta-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ta-err" role="alert"></em><div class="ta-askacts"><button type="button" class="ta-btn ta-btn-ghost ta-btn-sm"></button><button type="button" class="ta-btn ta-btn-sm"></button></div></div>';
        (el('ta-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('ta-open')) {
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
        x.b[1].className = 'ta-btn ta-btn-sm' + (o.danger ? ' ta-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ta-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ta-open');
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

    var cur = 1;

    window.AdminAgentsJsloaded = function () {
        list(1);
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        cur = pg || cur || 1;
        api('List', [{ key: 'tq', vlu: val('ta-q') }, { key: 'tfarea', vlu: val('ta-tfarea') }, { key: 'tfst', vlu: val('ta-tfst') }, { key: 'pg', vlu: String(cur) }]);
    }

    function edit(id) {
        api('Form', [{ key: 'tid', vlu: id || '' }]);
    }

    function move(id, d) {
        api('Move', [{ key: 'tid', vlu: id }, { key: 'tdir', vlu: String(d) }]);
    }

    function checked(name) {
        var l = document.querySelectorAll('input[name="' + name + '"]:checked');
        var out = [];
        for (var i = 0; i < l.length; i++) {
            out.push(l[i].value);
        }
        return out.join(',');
    }

    function save(id) {
        var keys = ['tname', 'ttitle', 'tlang', 'tyears', 'tphone', 'tmail', 'tlic', 'tbio', 'tlogin', 'tpass'];
        var list2 = keys.map(function (k) {
            return { key: k, vlu: val('ta-' + k) };
        });
        var ph = document.querySelector('input[name="ta-tphoto"]:checked');
        list2.push({ key: 'tid', vlu: id || '' });
        list2.push({ key: 'tareas', vlu: checked('ta-tarea') });
        list2.push({ key: 'tpic', vlu: ph ? ph.value : '' });
        list2.push({ key: 'tactive', vlu: el('ta-tactive').checked ? '1' : '0' });
        list2.push({ key: 'tsignin', vlu: el('ta-tsignin') && el('ta-tsignin').checked ? '1' : '0' });
        api('Save', list2);
    }

    function upload(input) {
        var f = input.files && input.files[0];
        el('ta-e-upl').textContent = '';
        readImage(f, function (img) {
            el('ta-upst').textContent = 'Uploading...';
            api('Upload', [{ key: 'full', vlu: crop(img, 480, 600, 0.86) }, { key: 'thumb', vlu: crop(img, 240, 300, 0.82) }]);
            input.value = '';
        }, function (m) {
            el('ta-e-upl').textContent = m;
            input.value = '';
        });
    }

    function uploaded(src, msg) {
        el('ta-upst').textContent = '';
        if (!src) {
            el('ta-e-upl').textContent = msg;
            return;
        }
        var box = el('ta-ppick');
        box.insertAdjacentHTML('beforeend', '<label><input type="radio" name="ta-tphoto" value="' + src + '" checked><img src="' + src + '" alt="Uploaded photo"></label>');
        toast('Photo uploaded. Save to keep it.');
    }

    function remove(id, name) {
        ask({ title: 'Remove ' + name + '?', text: 'Their profile page goes away. Their sign-in, if any, stays without the link to this profile.', ok: 'Remove', no: 'Keep', danger: true }, function () {
            api('Delete', [{ key: 'tid', vlu: id }]);
        });
    }

    function done(msg) {
        closeModal();
        list(cur);
        toast(msg);
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
            var id = e.target && e.target.id ? e.target.id.replace('ta-', 'ta-e-') : '';
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
        move: move,
        save: save,
        upload: upload,
        uploaded: uploaded,
        remove: remove,
        done: done
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminAgentsJs.reveal);
} else {
    AdminAgentsJs.reveal();
}
