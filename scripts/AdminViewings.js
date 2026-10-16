var AdminViewingsJs = (function () {

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
        $ApiRequest('AdminViewings/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('vw-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('vw-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('vw-open');
        }, 3200);
    }

    function openNav() {
        el('vw-nav').classList.add('vw-open');
        el('vw-scrim').classList.add('vw-open');
    }

    function closeNav() {
        el('vw-nav').classList.remove('vw-open');
        el('vw-scrim').classList.remove('vw-open');
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
        el('vw-modal').classList.add('vw-open');
        times(el('vw-mbody'));
        var f = el('vw-mbody').querySelector('input:not([type="hidden"]):not([readonly]), select, textarea');
        if (f && window.innerWidth > 640) {
            f.focus();
        }
    }

    function closeModal() {
        var m = el('vw-modal');
        if (m) {
            m.classList.remove('vw-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('vw-per')) {
            el('vw-per').value = k;
        }
        if (k !== 'range') {
            if (el('vw-from')) {
                el('vw-from').value = '';
            }
            if (el('vw-to')) {
                el('vw-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('vw-per') }, { key: 'pfrom', vlu: val('vw-from') }, { key: 'pto', vlu: val('vw-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('vw-main'));
        if (window.AdminViewingsJsloaded) {
            window.AdminViewingsJsloaded();
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
        if (!askBox || !askBox.classList.contains('vw-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('vw-open');
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
        askBox.className = 'vw-ask';
        askBox.innerHTML = '<div class="vw-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="vw-err" role="alert"></em><div class="vw-askacts"><button type="button" class="vw-btn vw-btn-ghost vw-btn-sm"></button><button type="button" class="vw-btn vw-btn-sm"></button></div></div>';
        (el('vw-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('vw-open')) {
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
        x.b[1].className = 'vw-btn vw-btn-sm' + (o.danger ? ' vw-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('vw-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('vw-open');
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

    window.AdminViewingsJsloaded = function () {
        list(1);
        if (qs('lead')) {
            edit('', qs('lead'));
        } else if (qs('id')) {
            edit(qs('id'), '');
        }
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        api('List', [{ key: 'wq', vlu: val('vw-q') }, { key: 'wst', vlu: val('vw-wst') }, { key: 'wagent', vlu: val('vw-wagent') }, { key: 'pg', vlu: String(pg || 1) }].concat(period()));
    }

    function edit(id, lead) {
        api('Form', [{ key: 'wid', vlu: id || '' }, { key: 'wlead', vlu: lead || '' }]);
    }

    function save(id) {
        var keys = ['wlisting', 'wagent2', 'wday', 'wtime', 'wmin', 'wname', 'wphone', 'wmail', 'wnote', 'wlead2'];
        var list2 = keys.map(function (k) {
            return { key: k, vlu: val('vw-' + k) };
        });
        list2.push({ key: 'wid', vlu: id || '' });
        api('Save', list2);
    }

    function saved(msg) {
        closeModal();
        list(1);
        toast(msg);
        try {
            history.replaceState(null, '', 'AdminViewings');
        } catch (e) {
        }
    }

    function act(id, a, who) {
        if (a === 'cancelled') {
            ask({ title: 'Cancel the viewing with ' + who + '?', text: 'Let them know by phone or email. The time becomes free again.', value: '', place: 'Reason (optional)', ok: 'Cancel viewing', no: 'Keep it', danger: true }, function (why) {
                api('Act', [{ key: 'wid', vlu: id }, { key: 'wact', vlu: a }, { key: 'wwhy', vlu: why }]);
            });
            return;
        }
        if (a === 'noshow') {
            ask({ title: 'Mark ' + who + ' as a no-show?', text: 'Use this when nobody came and nobody called.', ok: 'Mark no-show', no: 'Back' }, function () {
                api('Act', [{ key: 'wid', vlu: id }, { key: 'wact', vlu: a }, { key: 'wwhy', vlu: '' }]);
            });
            return;
        }
        api('Act', [{ key: 'wid', vlu: id }, { key: 'wact', vlu: a }, { key: 'wwhy', vlu: '' }]);
    }

    function acted(msg) {
        closeModal();
        list(1);
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
            var id = e.target && e.target.id ? e.target.id.replace('vw-', 'vw-e-') : '';
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
        save: save,
        saved: saved,
        act: act,
        acted: acted
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminViewingsJs.reveal);
} else {
    AdminViewingsJs.reveal();
}
